namespace ComunaClick.Admin.Models;

public sealed record ProviderEventSummary(
    string EventType,
    string Payload,
    DateTimeOffset ReceivedAt);

public sealed record PaymentTransactionSummary(
    Guid Id,
    string ExternalReference,
    string Provider,
    string ProviderToken,
    decimal Amount,
    string Currency,
    string Status,
    string? AuthorizationCode,
    string CustomerLabel,
    string SubscriptionPlan,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ProviderEventSummary> Events);

public sealed record PaymentSubscriptionSummary(
    Guid Id,
    string PlanName,
    string Provider,
    string Status,
    string CustomerLabel,
    string BusinessLabel,
    decimal Amount,
    string Currency,
    DateTimeOffset NextBillingAt);
