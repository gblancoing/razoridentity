namespace Payments.Gateway.Api.Contracts.Admin;

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
