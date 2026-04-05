namespace Payments.Gateway.Api.Contracts.Admin;

public sealed record AdminProviderEventDto(
    Guid Id,
    string ProviderEventId,
    string EventType,
    string Payload,
    DateTimeOffset ReceivedAt);
