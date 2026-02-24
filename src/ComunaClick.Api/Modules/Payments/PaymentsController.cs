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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var internalKey = _configuration["Payments:InternalWebhookKey"];
        if (!string.IsNullOrWhiteSpace(internalKey))
        {
            if (!Request.Headers.TryGetValue("X-Internal-Key", out var headerValue) || headerValue != internalKey)
            {
                return Unauthorized();
            }
        }

        var exists = await _db.PaymentEvents.AnyAsync(x =>
            x.TenantId == tenantId.Value &&
            x.ProviderEventId == request.ProviderEventId);
        if (exists)
        {
            return Ok(new { status = "duplicate" });
        }

        var paymentEvent = new PaymentEvent
        {
            TenantId = tenantId.Value,
            ProviderEventId = request.ProviderEventId,
            PaymentId = request.PaymentId,
            Payload = string.IsNullOrWhiteSpace(request.Payload) ? "{}" : request.Payload,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _db.PaymentEvents.Add(paymentEvent);

        Payment? payment = null;
        if (request.PaymentId.HasValue)
        {
            payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == request.PaymentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            payment = await _db.Payments.FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                x.ExternalReference == request.ExternalReference);
        }

        if (payment is not null)
        {
            payment.Status = request.Status.Trim();
            payment.LastEventId = request.ProviderEventId;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
