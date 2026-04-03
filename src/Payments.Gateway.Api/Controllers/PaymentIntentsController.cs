using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Common.Models;
using Payments.Gateway.Api.Contracts.Intents;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Persistence.Entities;
using Payments.Gateway.Api.Services;
using Payments.Gateway.Api.Services.Providers;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Route("v1/payment-intents")]
public sealed class PaymentIntentsController : ControllerBase
{
    private readonly PaymentsDbContext _db;
    private readonly IComunaClicNotifier _notifier;
    private readonly IPaymentProviderResolver _paymentProviderResolver;

    public PaymentIntentsController(
        PaymentsDbContext db,
        IComunaClicNotifier notifier,
        IPaymentProviderResolver paymentProviderResolver)
    {
        _db = db;
        _notifier = notifier;
        _paymentProviderResolver = paymentProviderResolver;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentIntentDto>> Get(Guid id)
    {
        var intent = await _db.PaymentIntents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (intent is null)
        {
            return NotFound();
        }

        return Ok(ToDto(intent));
    }

    [HttpPost]
    public async Task<ActionResult<PaymentIntentResponse>> Create(PaymentIntentCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            return BadRequest(new { message = "ExternalReference is required." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var providerClient = _paymentProviderResolver.Resolve(request.Provider);
        var provider = providerClient.Name;
        var exists = await _db.PaymentIntents.AnyAsync(x =>
            x.ExternalReference == request.ExternalReference && x.Provider == provider && x.Status == "pending");
        if (exists)
        {
            return Conflict(new { message = "Pending intent already exists for this reference." });
        }

        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl)
            ? $"{Request.Scheme}://{Request.Host}/v1/payment-intents/return"
            : request.ReturnUrl.Trim();
        var providerResponse = await providerClient.CreatePaymentAsync(
            new PaymentProviderCreateRequest(
                request.ExternalReference.Trim(),
                request.Amount,
                string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
                returnUrl,
                $"Pago ComunaClic {request.ExternalReference.Trim()}"),
            HttpContext.RequestAborted);

        var intent = new PaymentIntent
        {
            ExternalReference = request.ExternalReference.Trim(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            Status = providerResponse.Status,
            Provider = provider,
            ProviderToken = providerResponse.ProviderToken,
            RawResponse = string.IsNullOrWhiteSpace(providerResponse.RawResponse) ? "{}" : providerResponse.RawResponse,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.PaymentIntents.Add(intent);
        await _db.SaveChangesAsync();

        var response = new PaymentIntentResponse(intent.Id, intent.Status, intent.ProviderToken, providerResponse.RedirectUrl);
        return Created($"/v1/payment-intents/{intent.Id}", response);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<PaymentIntentDto>> UpdateStatus(Guid id, PaymentIntentStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var intent = await _db.PaymentIntents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (intent is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var providerEventId = string.IsNullOrWhiteSpace(request.ProviderEventId)
            ? Guid.NewGuid().ToString("N")
            : request.ProviderEventId.Trim();

        var exists = await _db.ProviderEvents.AnyAsync(x => x.ProviderEventId == providerEventId, cancellationToken);
        if (exists)
        {
            return Ok(ToDto(intent));
        }

        intent.Status = request.Status.Trim();
        if (!string.IsNullOrWhiteSpace(request.AuthorizationCode))
        {
            intent.AuthorizationCode = request.AuthorizationCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RawPayload))
        {
            intent.RawResponse = request.RawPayload;
        }

        intent.UpdatedAt = DateTimeOffset.UtcNow;

        var providerEvent = new ProviderEvent
        {
            IntentId = intent.Id,
            ProviderEventId = providerEventId,
            EventType = "status_change",
            Payload = string.IsNullOrWhiteSpace(request.RawPayload) ? "{}" : request.RawPayload,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _db.ProviderEvents.Add(providerEvent);
        await _db.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyPaymentAsync(intent, intent.Status, providerEventId, request.RawPayload, cancellationToken);

        return Ok(ToDto(intent));
    }

    private static PaymentIntentDto ToDto(PaymentIntent intent)
        => new(
            intent.Id,
            intent.ExternalReference,
            intent.Amount,
            intent.Currency,
            intent.Status,
            intent.Provider,
            intent.ProviderToken,
            intent.AuthorizationCode,
            intent.RawResponse,
            intent.CreatedAt,
            intent.UpdatedAt);
}
