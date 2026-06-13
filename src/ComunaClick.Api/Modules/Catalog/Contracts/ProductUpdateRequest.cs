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
    bool? IsActive,
    string? ProductAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? DiscoverySubcategoryIds = null);
