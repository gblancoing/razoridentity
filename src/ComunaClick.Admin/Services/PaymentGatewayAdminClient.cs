using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using ComunaClick.Admin.Models;

namespace ComunaClick.Admin.Services;

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
                    dto.Providers);
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

    public async Task<IReadOnlyList<PaymentTransactionSummary>> ListTransactionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await GetAsync<List<AdminPaymentIntentListItemDto>>("v1/admin/payment-intents?take=100", cancellationToken);
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

    public async Task<PaymentTransactionSummary?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await GetAsync<AdminPaymentIntentDetailDto>($"v1/admin/payment-intents/{id}", cancellationToken);
            if (dto is not null)
            {
                return new PaymentTransactionSummary(
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
                    dto.Events.Select(x => new ProviderEventSummary(x.EventType, x.Payload, x.ReceivedAt)).ToList());
            }
        }
        catch
        {
            if (!_options.UseMockFallback)
            {
                throw;
            }
        }

        return _fallbackStore.GetTransaction(id);
    }

    public async Task<IReadOnlyList<PaymentSubscriptionSummary>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await GetAsync<List<AdminSubscriptionCandidateDto>>("v1/admin/subscriptions", cancellationToken);
            if (dto is not null)
            {
                return dto.Select(x => new PaymentSubscriptionSummary(
                    Guid.NewGuid(),
                    x.PlanName,
                    x.Provider,
                    x.Status,
                    "Cliente por vincular",
                    x.ExternalReference,
                    x.Amount,
                    x.Currency,
                    x.NextBillingAt)).ToList();
            }
        }
        catch
        {
            if (!_options.UseMockFallback)
            {
                throw;
            }
        }

        return _fallbackStore.ListSubscriptions();
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
        var accessToken = _authState.Tokens?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException("No active admin session.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("Your account does not have permission to access the payments dashboard.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
