namespace Payments.Gateway.Api.Contracts.Intents;

public sealed record PaymentIntentDto(
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
    DateTimeOffset UpdatedAt);
