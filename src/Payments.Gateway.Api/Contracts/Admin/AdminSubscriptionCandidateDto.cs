namespace Payments.Gateway.Api.Contracts.Admin;

public sealed record AdminSubscriptionCandidateDto(
    string ExternalReference,
    string PlanName,
    string Provider,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset LastPaymentAt,
    DateTimeOffset NextBillingAt);
