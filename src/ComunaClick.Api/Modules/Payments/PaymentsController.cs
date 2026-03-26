using ComunaClick.Api.Modules.Payments.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Payments;

[ApiController]
[Authorize(Policy = "tenant.admin")]
[Route("v1/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public PaymentsController(CoreDbContext db, IConfiguration configuration, ITenantContext tenantContext)
    {
        _db = db;
        _configuration = configuration;
        _tenantContext = tenantContext;
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

    [HttpPost("provider-notify")]
    [AllowAnonymous]
    public async Task<IActionResult> ProviderNotify(PaymentProviderNotifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderEventId))
        {
            return BadRequest(new { message = "ProviderEventId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var internalKey = _configuration["Payments:InternalWebhookKey"]
            ?? _configuration["ComunaClic:InternalWebhookKey"];
        if (!string.IsNullOrWhiteSpace(internalKey))
        {
            if (!Request.Headers.TryGetValue("X-Internal-Key", out var headerValue) || headerValue != internalKey)
            {
                return Unauthorized();
            }
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
        payment.Status = request.Status.Trim();
        payment.LastEventId = request.ProviderEventId.Trim();
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
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
