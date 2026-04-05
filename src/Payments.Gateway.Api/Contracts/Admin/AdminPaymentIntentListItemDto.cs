namespace Payments.Gateway.Api.Contracts.Admin;

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
