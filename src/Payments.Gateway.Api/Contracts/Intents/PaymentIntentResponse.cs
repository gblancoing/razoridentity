namespace Payments.Gateway.Api.Contracts.Intents;

public sealed record PaymentIntentResponse(
    Guid IntentId,
    string Status,
    string? ProviderToken,
    string? RedirectUrl);
