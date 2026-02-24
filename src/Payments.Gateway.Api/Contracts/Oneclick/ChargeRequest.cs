namespace Payments.Gateway.Api.Contracts.Oneclick;

public sealed record ChargeRequest(
    Guid CustomerTokenId,
    Guid IntentId,
    decimal Amount,
    string? Currency,
    string? ProviderRef,
    string? RawPayload);
