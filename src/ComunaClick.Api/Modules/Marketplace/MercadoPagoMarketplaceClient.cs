using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MercadoPagoMarketplaceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly MercadoPagoMarketplaceOptions _options;

    public MercadoPagoMarketplaceClient(HttpClient httpClient, IOptions<MercadoPagoMarketplaceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
    }

    public string BuildOAuthAuthorizationUrl(Guid sellerId, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["platform_id"] = "mp",
            ["state"] = state,
            ["redirect_uri"] = _options.RedirectUri
        };

        var queryString = string.Join("&", query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

        return $"{_options.OAuthAuthorizeUrl}?{queryString}";
    }

    public async Task<MercadoPagoOAuthTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri
            }!)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<MercadoPagoOAuthTokenResponse>(raw, JsonOptions)
               ?? throw new InvalidOperationException("Empty Mercado Pago OAuth response.");
    }

    public async Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = refreshToken
            }!)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<MercadoPagoOAuthTokenResponse>(raw, JsonOptions)
               ?? throw new InvalidOperationException("Empty Mercado Pago refresh response.");
    }

    public async Task<MercadoPagoUserInfoResponse?> GetUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MercadoPagoUserInfoResponse>(JsonOptions, cancellationToken);
    }

    public async Task<MercadoPagoPaymentCreateResponse> CreatePaymentAsync(
        string accessToken,
        MercadoPagoPaymentCreateRequest payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<MercadoPagoPaymentCreateResponse>(raw, JsonOptions)
               ?? throw new InvalidOperationException("Empty Mercado Pago payment response.");
    }

    public async Task<MercadoPagoPreferenceResponse> CreateCheckoutProPreferenceAsync(
        string accessToken,
        MercadoPagoPreferenceRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<MercadoPagoPreferenceResponse>(raw, JsonOptions)
               ?? throw new InvalidOperationException("Empty Mercado Pago preference response.");
    }

    public async Task<MercadoPagoPaymentDetailsResponse?> GetPaymentAsync(
        string accessToken,
        string paymentId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{paymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MercadoPagoPaymentDetailsResponse>(JsonOptions, cancellationToken);
    }

    public async Task<MercadoPagoPaymentDetailsResponse?> SearchPaymentByExternalReferenceAsync(
        string accessToken,
        string externalReference,
        CancellationToken cancellationToken)
    {
        var url = $"v1/payments/search?sort=date_created&criteria=desc&external_reference={Uri.EscapeDataString(externalReference)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<MercadoPagoPaymentSearchResponse>(JsonOptions, cancellationToken);
        return result?.Results?
            .OrderByDescending(r => string.Equals(r.Status, "approved", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    public bool ValidateWebhookSignature(
        HttpRequest request,
        string resourceId)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("x-signature", out var signatureHeader))
        {
            return false;
        }

        var parts = signatureHeader.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var provided))
        {
            return false;
        }

        var requestId = request.Headers.TryGetValue("x-request-id", out var requestIdValue)
            ? requestIdValue.ToString()
            : string.Empty;

        var manifest = $"id:{resourceId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        return string.Equals(computed, provided, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record MercadoPagoOAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("user_id")] long? UserId,
    [property: JsonPropertyName("public_key")] string? PublicKey);

public sealed record MercadoPagoUserInfoResponse(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("nickname")] string? Nickname,
    [property: JsonPropertyName("email")] string? Email);

public sealed record MercadoPagoPaymentCreateRequest(
    [property: JsonPropertyName("transaction_amount")] decimal TransactionAmount,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("installments")] int Installments,
    [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
    [property: JsonPropertyName("issuer_id")] string? IssuerId,
    [property: JsonPropertyName("payer")] MercadoPagoPayerRequest Payer,
    [property: JsonPropertyName("application_fee")] decimal ApplicationFee,
    [property: JsonPropertyName("external_reference")] string ExternalReference,
    [property: JsonPropertyName("statement_descriptor")] string StatementDescriptor,
    [property: JsonPropertyName("metadata")] object Metadata);

public sealed record MercadoPagoPayerRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("identification")] MercadoPagoIdentificationRequest? Identification);

public sealed record MercadoPagoIdentificationRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("number")] string Number);

public sealed record MercadoPagoPaymentCreateResponse(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("status_detail")] string? StatusDetail,
    [property: JsonPropertyName("payment_method_id")] string? PaymentMethodId,
    [property: JsonPropertyName("transaction_amount")] decimal? TransactionAmount,
    [property: JsonPropertyName("transaction_amount_refunded")] decimal? TransactionAmountRefunded,
    [property: JsonPropertyName("date_approved")] DateTimeOffset? DateApproved);

public sealed record MercadoPagoPreferenceRequest(
    [property: JsonPropertyName("external_reference")] string ExternalReference,
    [property: JsonPropertyName("marketplace_fee")] decimal MarketplaceFee,
    [property: JsonPropertyName("back_urls")] MercadoPagoBackUrls BackUrls,
    [property: JsonPropertyName("items")] IReadOnlyList<MercadoPagoPreferenceItem> Items,
    [property: JsonPropertyName("payer")] MercadoPagoPreferencePayer Payer,
    [property: JsonPropertyName("notification_url")] string NotificationUrl);

public sealed record MercadoPagoBackUrls(
    [property: JsonPropertyName("success")] string Success,
    [property: JsonPropertyName("failure")] string Failure,
    [property: JsonPropertyName("pending")] string Pending);

public sealed record MercadoPagoPreferenceItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("currency_id")] string CurrencyId,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice);

public sealed record MercadoPagoPreferencePayer(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string? Name);

public sealed record MercadoPagoPreferenceResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("init_point")] string? InitPoint,
    [property: JsonPropertyName("sandbox_init_point")] string? SandboxInitPoint);

public sealed record MercadoPagoPaymentDetailsResponse(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("status_detail")] string? StatusDetail,
    [property: JsonPropertyName("payment_method_id")] string? PaymentMethodId,
    [property: JsonPropertyName("transaction_amount")] decimal? TransactionAmount,
    [property: JsonPropertyName("transaction_amount_refunded")] decimal? TransactionAmountRefunded,
    [property: JsonPropertyName("fee_details")] IReadOnlyList<MercadoPagoFeeDetail>? FeeDetails,
    [property: JsonPropertyName("date_approved")] DateTimeOffset? DateApproved,
    [property: JsonPropertyName("external_reference")] string? ExternalReference);

public sealed record MercadoPagoFeeDetail(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("amount")] decimal? Amount);

public sealed record MercadoPagoPaymentSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<MercadoPagoPaymentDetailsResponse>? Results);
