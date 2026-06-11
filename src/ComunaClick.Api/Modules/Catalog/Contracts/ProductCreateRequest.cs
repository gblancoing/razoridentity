namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    Guid? PartnerCatalogCategoryId,
    string? ImageUrl,
    decimal Price,
    decimal? CostPrice,
    string? Currency,
    bool? IsActive,
    int? InitialStock,
    string? ProductAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? DiscoverySubcategoryIds = null);
