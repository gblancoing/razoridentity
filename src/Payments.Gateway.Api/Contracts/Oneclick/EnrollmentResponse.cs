namespace Payments.Gateway.Api.Contracts.Oneclick;

public sealed record EnrollmentResponse(
    Guid TokenId,
    string CustomerId,
    string ProviderRef,
    string Status);
