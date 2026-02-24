namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    decimal? Price,
    string? Currency,
    bool? IsActive);
