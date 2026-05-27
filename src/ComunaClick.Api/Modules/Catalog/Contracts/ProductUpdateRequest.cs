namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    Guid? PartnerCatalogCategoryId,
    string? ImageUrl,
    decimal? Price,
    decimal? CostPrice,
    string? Currency,
    bool? IsActive);
