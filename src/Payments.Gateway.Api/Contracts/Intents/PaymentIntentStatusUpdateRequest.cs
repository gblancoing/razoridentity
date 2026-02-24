namespace Payments.Gateway.Api.Contracts.Intents;

public sealed record PaymentIntentStatusUpdateRequest(
    string Status,
    string? ProviderEventId,
    string? AuthorizationCode,
    string? RawPayload);
