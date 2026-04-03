using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Payments.Common.Interfaces;
using Payments.Common.Models;

namespace Payments.Gateway.Api.Services.Providers;

public sealed class TransbankPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly TransbankProviderOptions _options;

    public TransbankPaymentProvider(HttpClient httpClient, IOptions<TransbankProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => "transbank";

    public async Task<PaymentProviderCreateResponse> CreatePaymentAsync(
        PaymentProviderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.Simulate || string.IsNullOrWhiteSpace(_options.CommerceCode) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return BuildSimulatedResponse(request);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CreateTransactionUrl);
        httpRequest.Headers.TryAddWithoutValidation("Tbk-Api-Key-Id", _options.CommerceCode);
        httpRequest.Headers.TryAddWithoutValidation("Tbk-Api-Key-Secret", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new TransbankCreateRequest(
            request.ExternalReference,
            request.ExternalReference,
            request.Amount,
            request.ReturnUrl));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonSerializer.Deserialize<TransbankCreateResponse>(rawResponse, JsonOptions);
        return new PaymentProviderCreateResponse(
            Name,
            payload?.Token,
            BuildRedirectUrl(payload?.Url, payload?.Token),
            "pending",
            string.IsNullOrWhiteSpace(rawResponse) ? "{}" : rawResponse);
    }

    private PaymentProviderCreateResponse BuildSimulatedResponse(PaymentProviderCreateRequest request)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var redirectBaseUrl = string.IsNullOrWhiteSpace(_options.RedirectBaseUrl)
            ? request.ReturnUrl
            : _options.RedirectBaseUrl;

        return new PaymentProviderCreateResponse(
            Name,
            token,
            BuildRedirectUrl(redirectBaseUrl, token),
            "pending",
            JsonSerializer.Serialize(new
            {
                token,
                url = redirectBaseUrl,
                mode = "simulated"
            }, JsonOptions));
    }

    private static string? BuildRedirectUrl(string? baseUrl, string? token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return baseUrl;
        }

        return baseUrl.Contains("{token}", StringComparison.OrdinalIgnoreCase)
            ? baseUrl.Replace("{token}", Uri.EscapeDataString(token), StringComparison.OrdinalIgnoreCase)
            : $"{baseUrl}?token_ws={Uri.EscapeDataString(token)}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record TransbankCreateRequest(
        [property: JsonPropertyName("buy_order")] string BuyOrder,
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("return_url")] string ReturnUrl);

    private sealed record TransbankCreateResponse(
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("url")] string? Url);
}
