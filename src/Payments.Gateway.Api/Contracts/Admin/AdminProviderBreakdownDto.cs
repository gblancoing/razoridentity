namespace Payments.Gateway.Api.Contracts.Admin;

public sealed record AdminProviderBreakdownDto(
    string Provider,
    int Count,
    decimal Amount);
