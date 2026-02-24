using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Contracts.Intents;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Persistence.Entities;
using Payments.Gateway.Api.Services;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Route("v1/payment-intents")]
public sealed class PaymentIntentsController : ControllerBase
{
    private readonly PaymentsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IComunaClicNotifier _notifier;

    public PaymentIntentsController(PaymentsDbContext db, IConfiguration configuration, IComunaClicNotifier notifier)
    {
        _db = db;
        _configuration = configuration;
        _notifier = notifier;
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

        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "transbank" : request.Provider.Trim();
        var exists = await _db.PaymentIntents.AnyAsync(x =>
            x.ExternalReference == request.ExternalReference && x.Provider == provider && x.Status == "pending");
        if (exists)
        {
            return Conflict(new { message = "Pending intent already exists for this reference." });
        }

        var providerToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var intent = new PaymentIntent
        {
            ExternalReference = request.ExternalReference.Trim(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            Status = "pending",
            Provider = provider,
            ProviderToken = providerToken,
            RawResponse = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.PaymentIntents.Add(intent);
        await _db.SaveChangesAsync();

        var redirectUrl = BuildRedirectUrl(providerToken);
        var response = new PaymentIntentResponse(intent.Id, intent.Status, intent.ProviderToken, redirectUrl);
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

    private string? BuildRedirectUrl(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var baseUrl = _configuration["Transbank:RedirectBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return baseUrl.Contains("{token}", StringComparison.OrdinalIgnoreCase)
            ? baseUrl.Replace("{token}", token, StringComparison.OrdinalIgnoreCase)
            : $"{baseUrl}?token_ws={token}";
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
