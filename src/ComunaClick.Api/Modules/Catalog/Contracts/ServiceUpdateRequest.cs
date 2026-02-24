namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ServiceUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    decimal? Price,
    string? Currency,
    int? DurationMinutes,
    bool? IsActive);
