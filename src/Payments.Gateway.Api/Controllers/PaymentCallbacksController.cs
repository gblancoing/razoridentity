using System.Text.Json;
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
[Route("v1/payment-callbacks")]
public sealed class PaymentCallbacksController : ControllerBase
{
    private readonly PaymentsDbContext _db;
    private readonly IComunaClicNotifier _notifier;
    private readonly IPaymentProviderResolver _paymentProviderResolver;

    public PaymentCallbacksController(
        PaymentsDbContext db,
        IComunaClicNotifier notifier,
        IPaymentProviderResolver paymentProviderResolver)
    {
        _db = db;
        _notifier = notifier;
        _paymentProviderResolver = paymentProviderResolver;
    }

    [HttpGet("{provider}/return")]
    [HttpPost("{provider}/return")]
    public Task<ActionResult<PaymentIntentDto>> Return(string provider, CancellationToken cancellationToken)
        => ProcessAsync(provider, "return", cancellationToken);

    [HttpPost("{provider}/webhook")]
    public Task<ActionResult<PaymentIntentDto>> Webhook(string provider, CancellationToken cancellationToken)
        => ProcessAsync(provider, "webhook", cancellationToken);

    private async Task<ActionResult<PaymentIntentDto>> ProcessAsync(string provider, string callbackType, CancellationToken cancellationToken)
    {
        var providerClient = _paymentProviderResolver.Resolve(provider);
        var callbackRequest = new PaymentProviderCallbackRequest(
            providerClient.Name,
            callbackType,
            Request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            await ReadFormFieldsAsync(cancellationToken),
            await ReadRawBodyAsync(cancellationToken));

        var callbackResult = await providerClient.ProcessCallbackAsync(callbackRequest, cancellationToken);
        if (callbackResult is null)
        {
            return BadRequest(new { message = $"No callback payload could be resolved for provider '{providerClient.Name}'." });
        }

        var intent = await ResolvePaymentIntentAsync(callbackResult, cancellationToken);
        if (intent is null)
        {
            return NotFound(new
            {
                message = "Payment intent not found for callback payload.",
                provider = callbackResult.Provider,
                providerToken = callbackResult.ProviderToken,
                externalReference = callbackResult.ExternalReference
            });
        }

        var eventExists = await _db.ProviderEvents.AnyAsync(
            x => x.ProviderEventId == callbackResult.ProviderEventId,
            cancellationToken);
        if (!eventExists)
        {
            intent.Status = callbackResult.Status;
            intent.AuthorizationCode = callbackResult.AuthorizationCode ?? intent.AuthorizationCode;
            intent.RawResponse = callbackResult.RawPayload;
            intent.UpdatedAt = DateTimeOffset.UtcNow;

            _db.ProviderEvents.Add(new ProviderEvent
            {
                IntentId = intent.Id,
                ProviderEventId = callbackResult.ProviderEventId,
                EventType = callbackType,
                Payload = callbackResult.RawPayload,
                ReceivedAt = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyPaymentAsync(
                intent,
                intent.Status,
                callbackResult.ProviderEventId,
                callbackResult.RawPayload,
                cancellationToken);
        }

        return Ok(ToDto(intent));
    }

    private async Task<PaymentIntent?> ResolvePaymentIntentAsync(
        PaymentProviderCallbackResult callbackResult,
        CancellationToken cancellationToken)
    {
        var query = _db.PaymentIntents.AsQueryable()
            .Where(x => x.Provider == callbackResult.Provider);

        if (!string.IsNullOrWhiteSpace(callbackResult.ProviderToken))
        {
            return await query.FirstOrDefaultAsync(x => x.ProviderToken == callbackResult.ProviderToken, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(callbackResult.ExternalReference))
        {
            return await query
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(x => x.ExternalReference == callbackResult.ExternalReference, cancellationToken);
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<string, string?>> ReadFormFieldsAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        return form.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> ReadRawBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var formSnapshot = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
            return JsonSerializer.Serialize(new
            {
                form = formSnapshot,
                query = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString())
            });
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (!string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        if (Request.Query.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                query = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString())
            });
        }

        return "{}";
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
