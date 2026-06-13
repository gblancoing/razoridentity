namespace Payments.Gateway.Api.Contracts.Admin;

public sealed record AdminDashboardSummaryDto(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers,
    decimal CapturedToday,
    decimal CapturedThisWeek,
    decimal CapturedThisMonth,
    AdminAlertsDto? Alerts);
