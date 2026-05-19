namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    string? ImageUrl,
    decimal? Price,
    string? Currency,
    bool? IsActive);
