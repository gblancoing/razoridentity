namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ServiceSlotUpdateRequest(bool? IsAvailable, int? Capacity);
