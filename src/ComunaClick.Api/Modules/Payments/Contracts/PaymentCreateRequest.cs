namespace ComunaClick.Api.Modules.Payments.Contracts;

public sealed record PaymentCreateRequest(
    string ExternalReference,
    decimal Amount,
    string? Currency,
    string? Provider,
    Guid? GatewayIntentId,
    string? ProviderToken);
