using System.Net.Http.Headers;
using System.Net.Http.Json;
using ComunaClick.Admin.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ComunaClick.Admin.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentsAuthStateService _authState;
    private readonly AdminApiOptions _options;

    public AdminApiClient(HttpClient httpClient, IOptions<AdminApiOptions> options, PaymentsAuthStateService authState)
    {
        _httpClient = httpClient;
        _authState = authState;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public string PaymentsAppUrl => _options.PaymentsAppUrl;

    public string? AccessToken => _authState.Tokens?.AccessToken;

    public Task<AdminDashboardDto?> GetDashboardAsync(CancellationToken cancellationToken = default)
        => GetAsync<AdminDashboardDto>("v1/admin/dashboard", cancellationToken);

    public Task<List<AdminPartnerListItemDto>?> GetPartnersAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminPartnerListItemDto>>("v1/admin/partners", cancellationToken);

    public Task<AdminPartnerListItemDto?> UpdatePartnerAsync(Guid id, object request, CancellationToken cancellationToken = default)
        => PatchAsync<AdminPartnerListItemDto>($"v1/admin/partners/{id}", request, cancellationToken);

    public Task<List<AdminTenantListItemDto>?> GetTenantsAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminTenantListItemDto>>("v1/admin/tenants", cancellationToken);

    public Task<AdminTenantListItemDto?> UpdateTenantAsync(Guid id, object request, CancellationToken cancellationToken = default)
        => PatchAsync<AdminTenantListItemDto>($"v1/admin/tenants/{id}", request, cancellationToken);

    public Task<List<AdminCategoryListItemDto>?> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminCategoryListItemDto>>("v1/admin/categories", cancellationToken);

    public Task<AdminCategoryListItemDto?> UpdateCategoryAsync(Guid id, object request, CancellationToken cancellationToken = default)
        => PatchAsync<AdminCategoryListItemDto>($"v1/admin/categories/{id}", request, cancellationToken);

    public Task<List<AdminDeliveryProviderDto>?> GetDeliveryProvidersAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminDeliveryProviderDto>>("v1/admin/delivery-providers", cancellationToken);

    public Task<AdminDeliveryProviderDto?> CreateDeliveryProviderAsync(object request, CancellationToken cancellationToken = default)
        => PostAsync<AdminDeliveryProviderDto>("v1/admin/delivery-providers", request, cancellationToken);

    public Task<AdminDeliveryProviderDto?> UpdateDeliveryProviderAsync(Guid id, object request, CancellationToken cancellationToken = default)
        => PatchAsync<AdminDeliveryProviderDto>($"v1/admin/delivery-providers/{id}", request, cancellationToken);

    public Task<SiteContentDto?> GetSiteContentAsync(CancellationToken cancellationToken = default)
        => GetAsync<SiteContentDto>("v1/admin/site-content", cancellationToken);

    public Task<SiteContentDto?> SaveSiteContentAsync(SiteContentDto request, CancellationToken cancellationToken = default)
        => PutAsync<SiteContentDto>("v1/admin/site-content", request, cancellationToken);

    public Task<List<AdminAuditEventDto>?> GetAuditAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminAuditEventDto>>("v1/admin/audit", cancellationToken);

    public Task<List<AdminSellerFeeItemDto>?> GetSellerFeesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminSellerFeeItemDto>>("v1/admin/marketplace/fees", cancellationToken);

    // ── Orders ─────────────────────────────────────────────────────────────

    public Task<AdminPagedResult<AdminOrderListItemDto>?> GetOrdersAsync(
        string? status = null, Guid? tenantId = null, Guid? partnerId = null,
        string? search = null, DateTimeOffset? from = null, DateTimeOffset? to = null,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
        if (tenantId.HasValue) q["tenantId"] = tenantId.ToString();
        if (partnerId.HasValue) q["partnerId"] = partnerId.ToString();
        if (!string.IsNullOrWhiteSpace(search)) q["search"] = search;
        if (from.HasValue) q["from"] = from.Value.ToString("O");
        if (to.HasValue) q["to"] = to.Value.ToString("O");
        q["page"] = page.ToString();
        q["pageSize"] = pageSize.ToString();
        return GetAsync<AdminPagedResult<AdminOrderListItemDto>>(
            QueryHelpers.AddQueryString("v1/admin/orders", q), cancellationToken);
    }

    public Task<AdminOrderDetailDto?> GetOrderDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<AdminOrderDetailDto>($"v1/admin/orders/{id}", cancellationToken);

    public string BuildOrdersExportUrl(string? status, Guid? tenantId, string? search,
        DateTimeOffset? from, DateTimeOffset? to)
    {
        var q = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
        if (tenantId.HasValue) q["tenantId"] = tenantId.ToString();
        if (!string.IsNullOrWhiteSpace(search)) q["search"] = search;
        if (from.HasValue) q["from"] = from.Value.ToString("O");
        if (to.HasValue) q["to"] = to.Value.ToString("O");
        var relative = QueryHelpers.AddQueryString("v1/admin/orders/export.csv", q);
        return _httpClient.BaseAddress + relative;
    }

    // ── Delivery settlements ────────────────────────────────────────────────

    public Task<List<AdminDeliverySettlementDto>?> GetSettlementsAsync(
        string? status = null, Guid? courierId = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
        if (courierId.HasValue) q["courierId"] = courierId.ToString();
        if (from.HasValue) q["from"] = from.Value.ToString("O");
        if (to.HasValue) q["to"] = to.Value.ToString("O");
        return GetAsync<List<AdminDeliverySettlementDto>>(
            QueryHelpers.AddQueryString("v1/admin/delivery-settlements", q), cancellationToken);
    }

    public Task SettleAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync<object>($"v1/admin/delivery-settlements/{id}/settle", new { }, cancellationToken);

    // ── Couriers ────────────────────────────────────────────────────────────

    public Task<List<AdminCourierListItemDto>?> GetCouriersAsync(
        Guid? tenantId = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string?>();
        if (tenantId.HasValue) q["tenantId"] = tenantId.ToString();
        if (!string.IsNullOrWhiteSpace(search)) q["search"] = search;
        return GetAsync<List<AdminCourierListItemDto>>(
            QueryHelpers.AddQueryString("v1/admin/couriers", q), cancellationToken);
    }

    // ── Sellers / Marketplace ───────────────────────────────────────────────

    public Task<List<AdminSellerListItemDto>?> GetSellersAsync(
        Guid? tenantId = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string?>();
        if (tenantId.HasValue) q["tenantId"] = tenantId.ToString();
        if (!string.IsNullOrWhiteSpace(search)) q["search"] = search;
        return GetAsync<List<AdminSellerListItemDto>>(
            QueryHelpers.AddQueryString("v1/admin/sellers", q), cancellationToken);
    }

    public Task SyncMarketplacePaymentAsync(string paymentId, CancellationToken cancellationToken = default)
        => PostAsync<object>($"v1/admin/marketplace/payments/{paymentId}/sync", new { }, cancellationToken);

    // ── Payouts ─────────────────────────────────────────────────────────────

    public Task<List<AdminPayoutBatchListItemDto>?> GetPayoutBatchesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminPayoutBatchListItemDto>>("v1/admin/payouts/batches", cancellationToken);

    public Task<object?> UpdateGlobalFeeAsync(decimal percentageFee, decimal fixedFeeAmount = 0m, CancellationToken cancellationToken = default)
        => PutAsync<object>("v1/admin/marketplace/fees", new { percentageFee, fixedFeeAmount }, cancellationToken);

    public Task<object?> UpdateSellerFeeAsync(Guid sellerId, decimal percentageFee, decimal fixedFeeAmount = 0m, CancellationToken cancellationToken = default)
        => PutAsync<object>($"v1/admin/marketplace/fees/{sellerId}", new { percentageFee, fixedFeeAmount }, cancellationToken);

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<T?> PatchAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Patch, path);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<T?> PutAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, path);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<T?> PostAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var accessToken = _authState.Tokens?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException("No active admin session.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
