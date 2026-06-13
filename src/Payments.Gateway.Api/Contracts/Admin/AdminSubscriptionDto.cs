namespace Payments.Gateway.Api.Contracts.Admin;

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

public sealed record AdminSubscriptionAttemptDto(
    Guid Id,
    Guid? IntentId,
    Guid? ChargeId,
    string Status,
    string? ErrorMessage,
    DateTimeOffset AttemptedAt);
