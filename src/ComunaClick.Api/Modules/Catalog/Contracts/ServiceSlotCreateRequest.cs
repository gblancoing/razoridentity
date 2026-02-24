namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ServiceSlotCreateRequest(
    Guid PartnerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int? Capacity);
