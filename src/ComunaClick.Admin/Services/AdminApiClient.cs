using System.Net.Http.Headers;
using System.Net.Http.Json;
using ComunaClick.Admin.Models;
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

    public Task<SiteContentDto?> GetSiteContentAsync(CancellationToken cancellationToken = default)
        => GetAsync<SiteContentDto>("v1/admin/site-content", cancellationToken);

    public Task<SiteContentDto?> SaveSiteContentAsync(SiteContentDto request, CancellationToken cancellationToken = default)
        => PutAsync<SiteContentDto>("v1/admin/site-content", request, cancellationToken);

    public Task<List<AdminAuditEventDto>?> GetAuditAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<AdminAuditEventDto>>("v1/admin/audit", cancellationToken);

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
