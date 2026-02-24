namespace Payments.Gateway.Api.Contracts.Intents;

public sealed record PaymentIntentCreateRequest(
    string ExternalReference,
    decimal Amount,
    string? Currency,
    string? Provider,
    string? ReturnUrl);
