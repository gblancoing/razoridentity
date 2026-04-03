using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Payments.Common.Interfaces;
using Payments.Common.Models;

namespace Payments.Gateway.Api.Services.Providers;

public sealed class MercadoPagoPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly MercadoPagoProviderOptions _options;

    public MercadoPagoPaymentProvider(HttpClient httpClient, IOptions<MercadoPagoProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => "mercadopago";

    public async Task<PaymentProviderCreateResponse> CreatePaymentAsync(
        PaymentProviderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.Simulate || string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            var preferenceId = $"mp_{Guid.NewGuid():N}";
            return new PaymentProviderCreateResponse(
                Name,
                preferenceId,
                $"{request.ReturnUrl}?pref_id={Uri.EscapeDataString(preferenceId)}",
                "pending",
                JsonSerializer.Serialize(new { id = preferenceId, init_point = request.ReturnUrl, mode = "simulated" }, JsonOptions));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CreatePreferenceUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        if (!string.IsNullOrWhiteSpace(_options.PlatformId))
        {
            httpRequest.Headers.TryAddWithoutValidation("x-platform-id", _options.PlatformId);
        }

        httpRequest.Content = JsonContent.Create(new MercadoPagoPreferenceRequest(
            request.ExternalReference,
            true,
            "all",
            new MercadoPagoBackUrls(request.ReturnUrl, request.ReturnUrl, request.ReturnUrl),
            new[]
            {
                new MercadoPagoPreferenceItem(
                    request.ExternalReference,
                    request.Subject ?? request.ExternalReference,
                    request.Currency,
                    1,
                    request.Amount)
            }));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonSerializer.Deserialize<MercadoPagoPreferenceResponse>(rawResponse, JsonOptions);
        return new PaymentProviderCreateResponse(
            Name,
            payload?.Id,
            payload?.InitPoint ?? payload?.SandboxInitPoint,
            "pending",
            string.IsNullOrWhiteSpace(rawResponse) ? "{}" : rawResponse);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record MercadoPagoPreferenceRequest(
        [property: JsonPropertyName("external_reference")] string ExternalReference,
        [property: JsonPropertyName("binary_mode")] bool BinaryMode,
        [property: JsonPropertyName("auto_return")] string AutoReturn,
        [property: JsonPropertyName("back_urls")] MercadoPagoBackUrls BackUrls,
        [property: JsonPropertyName("items")] MercadoPagoPreferenceItem[] Items);

    private sealed record MercadoPagoBackUrls(
        [property: JsonPropertyName("success")] string Success,
        [property: JsonPropertyName("failure")] string Failure,
        [property: JsonPropertyName("pending")] string Pending);

    private sealed record MercadoPagoPreferenceItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("currency_id")] string CurrencyId,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("unit_price")] decimal UnitPrice);

    private sealed record MercadoPagoPreferenceResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("init_point")] string? InitPoint,
        [property: JsonPropertyName("sandbox_init_point")] string? SandboxInitPoint);
}
