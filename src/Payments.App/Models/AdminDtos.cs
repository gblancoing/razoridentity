namespace Payments.App.Models;

public sealed record AdminProviderBreakdownDto(
    string Provider,
    int Count,
    decimal Amount);

public sealed record AdminAlertsDto(
    int StuckPendingCount,
    int FlaggedCount,
    int FailedLast7DaysCount,
    int ChargesWithoutAuthCount,
    int CoreNotifyFailingCount)
{
    public int Total => StuckPendingCount + FlaggedCount + FailedLast7DaysCount + ChargesWithoutAuthCount + CoreNotifyFailingCount;
}

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

public sealed record AdminProviderEventDto(
    Guid Id,
    string ProviderEventId,
    string EventType,
    string Payload,
    DateTimeOffset ReceivedAt);

public sealed record AdminPaymentIntentListItemDto(
    Guid Id,
    string ExternalReference,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? ProviderToken,
    string? AuthorizationCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPaymentIntentDetailDto(
    Guid Id,
    string ExternalReference,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? ProviderToken,
    string? AuthorizationCode,
    string RawResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminProviderEventDto> Events,
    string? ReviewStatus,
    string? ReviewNote,
    DateTimeOffset? CoreNotifiedAt,
    int CoreNotifyAttempts);

public sealed record AdminSubscriptionDto(
    Guid Id,
    string CustomerId,
    string ExternalReference,
    string? PlanName,
    string Provider,
    decimal Amount,
    string Currency,
    string BillingInterval,
    string Status,
    DateTimeOffset? NextChargeAt,
    DateTimeOffset? LastAttemptAt,
    string? LastAttemptStatus,
    DateTimeOffset CreatedAt);

public sealed record AdminSubscriptionAttemptDto(
    Guid Id,
    Guid? IntentId,
    Guid? ChargeId,
    string Status,
    string? ErrorMessage,
    DateTimeOffset AttemptedAt);

public sealed record AdminSubscriptionDetailDto(
    Guid Id,
    string CustomerId,
    Guid? CustomerTokenId,
    string ExternalReference,
    string? PlanName,
    string Provider,
    decimal Amount,
    string Currency,
    string BillingInterval,
    string Status,
    DateTimeOffset? NextChargeAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminSubscriptionAttemptDto> Attempts);

public sealed record AdminReconciliationRowDto(
    string Kind,
    Guid? IntentId,
    string? ExternalReference,
    string? Provider,
    string? Status,
    decimal? Amount,
    string? Currency,
    string Detail,
    DateTimeOffset OccurredAt);

public sealed record AdminDashboardSummaryModel(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers,
    decimal CapturedToday = 0,
    decimal CapturedThisWeek = 0,
    decimal CapturedThisMonth = 0,
    AdminAlertsDto? Alerts = null);
