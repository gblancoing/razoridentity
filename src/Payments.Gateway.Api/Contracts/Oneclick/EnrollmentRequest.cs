namespace Payments.Gateway.Api.Contracts.Oneclick;

public sealed record EnrollmentRequest(
    string CustomerId,
    string? ProviderRef,
    string? RawPayload);
