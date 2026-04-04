using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Payments.Common.Interfaces;
using Payments.Common.Models;

namespace Payments.Gateway.Api.Services.Providers;

public sealed class KhipuPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly KhipuProviderOptions _options;

    public KhipuPaymentProvider(HttpClient httpClient, IOptions<KhipuProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => "khipu";

    public async Task<PaymentProviderCreateResponse> CreatePaymentAsync(
        PaymentProviderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.Simulate || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var paymentId = $"khipu_{Guid.NewGuid():N}";
            return new PaymentProviderCreateResponse(
                Name,
                paymentId,
                $"{request.ReturnUrl}?payment_id={Uri.EscapeDataString(paymentId)}",
                "pending",
                JsonSerializer.Serialize(new { payment_id = paymentId, payment_url = request.ReturnUrl, mode = "simulated" }, JsonOptions));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CreatePaymentUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new KhipuCreateRequest(
            request.ExternalReference,
            request.Subject ?? request.ExternalReference,
            request.Amount,
            request.Currency,
            request.ReturnUrl,
            _options.NotifyUrl,
            request.BuyerEmail));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonSerializer.Deserialize<KhipuCreateResponse>(rawResponse, JsonOptions);
        return new PaymentProviderCreateResponse(
            Name,
            payload?.PaymentId,
            payload?.PaymentUrl,
            "pending",
            string.IsNullOrWhiteSpace(rawResponse) ? "{}" : rawResponse);
    }

    public Task<PaymentProviderCallbackResult?> ProcessCallbackAsync(
        PaymentProviderCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = ParsePayload(request.RawBody);
        var paymentId = ReadValue(request, "payment_id")
            ?? payload?.PaymentId;
        var transactionId = payload?.TransactionId ?? ReadValue(request, "transaction_id");

        if (string.IsNullOrWhiteSpace(paymentId) && string.IsNullOrWhiteSpace(transactionId))
        {
            return Task.FromResult<PaymentProviderCallbackResult?>(null);
        }

        var status = payload?.ConciliationDate is not null
            ? "captured"
            : payload?.Status?.Trim().ToLowerInvariant() switch
            {
                "done" or "ok" or "paid" or "conciliated" => "captured",
                "failed" or "error" => "failed",
                "cancelled" or "canceled" => "canceled",
                _ => "pending"
            };

        var rawPayload = string.IsNullOrWhiteSpace(request.RawBody)
            ? JsonSerializer.Serialize(new { paymentId, transactionId, source = request.CallbackType }, JsonOptions)
            : request.RawBody;

        return Task.FromResult<PaymentProviderCallbackResult?>(new PaymentProviderCallbackResult(
            Name,
            paymentId,
            transactionId,
            status,
            $"{Name}:{paymentId ?? transactionId}:{payload?.ConciliationDate?.ToUnixTimeSeconds().ToString() ?? status}",
            null,
            rawPayload,
            status == "captured" ? "Pago Khipu conciliado." : "Notificación Khipu registrada."));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record KhipuCreateRequest(
        [property: JsonPropertyName("transaction_id")] string TransactionId,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("return_url")] string ReturnUrl,
        [property: JsonPropertyName("notify_url")] string? NotifyUrl,
        [property: JsonPropertyName("payer_email")] string? PayerEmail);

    private sealed record KhipuCreateResponse(
        [property: JsonPropertyName("payment_id")] string? PaymentId,
        [property: JsonPropertyName("payment_url")] string? PaymentUrl);

    private sealed record KhipuCallbackPayload(
        [property: JsonPropertyName("payment_id")] string? PaymentId,
        [property: JsonPropertyName("transaction_id")] string? TransactionId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("conciliation_date")] DateTimeOffset? ConciliationDate);

    private static KhipuCallbackPayload? ParsePayload(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KhipuCallbackPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadValue(PaymentProviderCallbackRequest request, string key)
    {
        if (request.Form.TryGetValue(key, out var formValue) && !string.IsNullOrWhiteSpace(formValue))
        {
            return formValue;
        }

        return request.Query.TryGetValue(key, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue)
            ? queryValue
            : null;
    }
}
