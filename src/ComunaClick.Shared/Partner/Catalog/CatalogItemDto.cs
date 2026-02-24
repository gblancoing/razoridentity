namespace ComunaClick.Shared.Partner.Catalog;

public sealed record CatalogItemDto(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Price,
    bool IsActive
);
