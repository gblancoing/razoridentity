namespace Payments.Gateway.Api.Contracts.Oneclick;

public sealed record ChargeResponse(
    Guid ChargeId,
    string Status,
    string? ProviderRef,
    string? AuthorizationCode);
