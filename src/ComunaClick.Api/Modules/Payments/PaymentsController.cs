using System.Security.Cryptography;
using System.Text;
using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Payments.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Payments;

[ApiController]
[Authorize(Policy = "tenant.admin")]
[Route("v1/payments")]
public sealed class PaymentsController : ControllerBase
{
    // Estados de pago aceptados desde el proveedor; consistente con
    // MercadoPagoWebhookService.NormalizeMercadoPagoStatus y OrderInventoryFulfillment.IsPaidStatus.
    private static readonly HashSet<string> AllowedPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "in_process",
        "authorized",
        "approved",
        "paid",
        "rejected",
        "cancelled",
        "refunded",
        "charged_back"
    };

    private const string PlaceholderWebhookKey = "CHANGE_ME";

    private readonly CoreDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly IProductInventoryService _inventoryService;
    private readonly IOrderNotificationService _orderNotificationService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        CoreDbContext db,
        IConfiguration configuration,
        ITenantContext tenantContext,
        IProductInventoryService inventoryService,
        IOrderNotificationService orderNotificationService,
        ILogger<PaymentsController> logger)
    {
        _db = db;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _inventoryService = inventoryService;
        _orderNotificationService = orderNotificationService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Payment>> Get(Guid id)
    {
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<Payment>> Create(PaymentCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var exists = await _db.Payments.AnyAsync(x =>
            x.TenantId == tenantId.Value &&
            x.Provider == (request.Provider ?? "transbank") &&
            x.ExternalReference == request.ExternalReference);
        if (exists)
        {
            return Conflict(new { message = "Payment already exists for this reference." });
        }

        var amount = request.ToMoney("CLP");
        var payment = new Payment
        {
            TenantId = tenantId.Value,
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? "transbank" : request.Provider.Trim(),
            ExternalReference = request.ExternalReference,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Status = "pending",
            GatewayIntentId = request.GatewayIntentId,
            ProviderToken = request.ProviderToken,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return Created($"/v1/payments/{payment.Id}", payment);
    }

    // Webhook interno para notificaciones del gateway propio (Payments.Gateway.Api).
    // El flujo de MercadoPago usa su webhook firmado propio (MercadoPagoWebhookController);
    // este endpoint se mantiene solo para integraciones internas y exige siempre X-Internal-Key.
    [HttpPost("provider-notify")]
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> ProviderNotify(PaymentProviderNotifyRequest request)
    {
        var internalKey = _configuration["Payments:InternalWebhookKey"]
            ?? _configuration["ComunaClic:InternalWebhookKey"];
        if (string.IsNullOrWhiteSpace(internalKey) ||
            string.Equals(internalKey, PlaceholderWebhookKey, StringComparison.Ordinal))
        {
            _logger.LogError(
                "Payments:InternalWebhookKey no está configurada (o es placeholder); provider-notify queda deshabilitado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Internal webhook is not configured." });
        }

        if (!Request.Headers.TryGetValue("X-Internal-Key", out var headerValue) ||
            !FixedTimeEquals(headerValue.ToString(), internalKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ProviderEventId))
        {
            return BadRequest(new { message = "ProviderEventId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var newPaymentStatus = request.Status.Trim().ToLowerInvariant();
        if (!AllowedPaymentStatuses.Contains(newPaymentStatus))
        {
            return BadRequest(new { message = $"Status \"{request.Status}\" is not a valid payment status." });
        }

        if (request.PaymentId is null && string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            return BadRequest(new { message = "PaymentId or ExternalReference is required." });
        }

        var payment = await ResolvePaymentAsync(request);
        if (payment is null)
        {
            return NotFound(new { message = "Payment could not be resolved from provider notification." });
        }

        if (request.Amount.HasValue && payment.Amount > 0 && request.Amount.Value != payment.Amount)
        {
            _logger.LogWarning(
                "provider-notify rechazado: monto notificado {NotifiedAmount} no coincide con el pago {PaymentId} ({ExpectedAmount}).",
                request.Amount.Value, payment.Id, payment.Amount);
            return BadRequest(new { message = "Notified amount does not match the payment amount." });
        }

        var exists = await _db.PaymentEvents.IgnoreQueryFilters().AnyAsync(x =>
            x.TenantId == payment.TenantId &&
            x.ProviderEventId == request.ProviderEventId);
        if (exists)
        {
            return Ok(new { status = "duplicate" });
        }

        var paymentEvent = new PaymentEvent
        {
            TenantId = payment.TenantId,
            ProviderEventId = request.ProviderEventId.Trim(),
            PaymentId = payment.Id,
            Payload = string.IsNullOrWhiteSpace(request.Payload) ? "{}" : request.Payload,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _db.PaymentEvents.Add(paymentEvent);
        payment.Status = newPaymentStatus;
        payment.LastEventId = request.ProviderEventId.Trim();
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        if (payment.OrderId.HasValue && OrderInventoryFulfillment.IsPaidStatus(newPaymentStatus))
        {
            var order = await _db.Orders
                .IgnoreQueryFilters()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == payment.OrderId.Value);
            if (order is not null)
            {
                if (!OrderInventoryFulfillment.IsPaidStatus(order.Status) &&
                    OrderStatusMachine.CanTransition(order.Status, OrderStatusMachine.Paid, out _))
                {
                    order.Status = OrderStatusMachine.Paid;
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync();
                }

                await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(
                    _db,
                    _inventoryService,
                    order,
                    "paid");

                await _orderNotificationService.NotifyPartnerOrderPaidAsync(order.Id);
            }
        }

        return Ok();
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private Task<Payment?> ResolvePaymentAsync(PaymentProviderNotifyRequest request)
    {
        if (request.PaymentId.HasValue)
        {
            return _db.Payments.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId.Value);
        }

        var externalReference = request.ExternalReference!.Trim();
        return _db.Payments.IgnoreQueryFilters()
            .Where(x => x.ExternalReference == externalReference)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
