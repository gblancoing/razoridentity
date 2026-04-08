namespace ComunaClick.Admin.Models;

public sealed record AdminProviderBreakdownDto(
    string Provider,
    int Count,
    decimal Amount);

public sealed record AdminDashboardSummaryDto(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers);

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
    IReadOnlyList<AdminProviderEventDto> Events);

public sealed record AdminSubscriptionCandidateDto(
    string ExternalReference,
    string PlanName,
    string Provider,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset LastPaymentAt,
    DateTimeOffset NextBillingAt);

public sealed record AdminDashboardSummaryModel(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers);
