using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Payments.App.Models;

namespace Payments.App.Services;

public sealed class PaymentGatewayAdminClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentDashboardStore _fallbackStore;
    private readonly PaymentsAuthStateService _authState;
    private readonly PaymentGatewayOptions _options;

    public PaymentGatewayAdminClient(
        HttpClient httpClient,
        IOptions<PaymentGatewayOptions> options,
        PaymentDashboardStore fallbackStore,
        PaymentsAuthStateService authState)
    {
        _fallbackStore = fallbackStore;
        _authState = authState;
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public string? AccessToken => _authState.Tokens?.AccessToken;

    public async Task<AdminDashboardSummaryModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await GetAsync<AdminDashboardSummaryDto>("v1/admin/dashboard", cancellationToken);
            if (dto is not null)
            {
                return new AdminDashboardSummaryModel(
                    dto.CapturedAmount,
                    dto.CapturedCount,
                    dto.PendingCount,
                    dto.FailedCount,
                    dto.ActiveSubscriptionCandidates,
                    dto.Providers,
                    dto.CapturedToday,
                    dto.CapturedThisWeek,
                    dto.CapturedThisMonth,
                    dto.Alerts);
            }
        }
        catch
        {
            if (!_options.UseMockFallback)
            {
                throw;
            }
        }

        return _fallbackStore.GetDashboard();
    }

    public async Task<AdminAlertsDto?> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<AdminAlertsDto>("v1/admin/alerts", cancellationToken);
        }
        catch
        {
            if (!_options.UseMockFallback)
            {
                throw;
            }

            return null;
        }
    }

    public async Task<IReadOnlyList<PaymentTransactionSummary>> ListTransactionsAsync(
        string? provider = null,
        string? status = null,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var path = BuildIntentsPath("v1/admin/payment-intents", provider, status, search, from, to, take: 200);
            var dto = await GetAsync<List<AdminPaymentIntentListItemDto>>(path, cancellationToken);
            if (dto is not null)
            {
                return dto.Select(MapTransaction).ToList();
            }
        }
        catch
        {
            if (!_options.UseMockFallback)
            {
                throw;
            }
        }

        return _fallbackStore.ListTransactions();
    }

    public string BuildTransactionsExportUrl(
        string? provider = null,
        string? status = null,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
        => new Uri(_httpClient.BaseAddress!, BuildIntentsPath("v1/admin/payment-intents/export.csv", provider, status, search, from, to, take: null)).ToString();

    public Task<AdminPaymentIntentDetailDto?> GetTransactionDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<AdminPaymentIntentDetailDto>($"v1/admin/payment-intents/{id}", cancellationToken);

    public Task NotifyCoreAsync(Guid intentId, CancellationToken cancellationToken = default)
        => PostAsync($"v1/admin/payment-intents/{intentId}/notify-core", null, cancellationToken);

    public Task ReviewAsync(Guid intentId, string status, string? note, CancellationToken cancellationToken = default)
        => PostAsync($"v1/admin/payment-intents/{intentId}/review", new { status, note }, cancellationToken);

    public async Task<IReadOnlyList<AdminSubscriptionDto>> ListSubscriptionsAsync(
        string? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        if (!string.IsNullOrWhiteSpace(search)) query["search"] = search;

        var dto = await GetAsync<List<AdminSubscriptionDto>>(
            QueryHelpers.AddQueryString("v1/admin/subscriptions", query), cancellationToken);
        return dto ?? [];
    }

    public Task<AdminSubscriptionDetailDto?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<AdminSubscriptionDetailDto>($"v1/admin/subscriptions/{id}", cancellationToken);

    public Task CancelSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"v1/admin/subscriptions/{id}/cancel", null, cancellationToken);

    public Task PauseSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"v1/admin/subscriptions/{id}/pause", null, cancellationToken);

    public Task ResumeSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"v1/admin/subscriptions/{id}/resume", null, cancellationToken);

    public async Task<IReadOnlyList<AdminReconciliationRowDto>> GetReconciliationAsync(CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<List<AdminReconciliationRowDto>>("v1/admin/reconciliation", cancellationToken);
        return dto ?? [];
    }

    private static string BuildIntentsPath(
        string basePath,
        string? provider,
        string? status,
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? take)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(provider)) query["provider"] = provider;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        if (!string.IsNullOrWhiteSpace(search)) query["search"] = search;
        if (from.HasValue) query["from"] = from.Value.UtcDateTime.ToString("O");
        if (to.HasValue) query["to"] = to.Value.UtcDateTime.ToString("O");
        if (take.HasValue) query["take"] = take.Value.ToString();
        return QueryHelpers.AddQueryString(basePath, query);
    }

    private static PaymentTransactionSummary MapTransaction(AdminPaymentIntentListItemDto dto)
        => new(
            dto.Id,
            dto.ExternalReference,
            dto.Provider,
            dto.ProviderToken ?? "-",
            dto.Amount,
            dto.Currency,
            dto.Status,
            dto.AuthorizationCode,
            "Cliente por vincular",
            BuildPlanName(dto.ExternalReference),
            dto.CreatedAt,
            Array.Empty<ProviderEventSummary>());

    private static string BuildPlanName(string externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return "Plan recurrente";
        }

        var parts = externalReference
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.Equals(x, "SUBS", StringComparison.OrdinalIgnoreCase))
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant());

        return string.Join(" ", parts);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task PostAsync(string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = _authState.Tokens?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException("No active admin session.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new UnauthorizedAccessException("Your session is no longer valid.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw new InvalidOperationException("Your account does not have permission to access the payments dashboard.");
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
