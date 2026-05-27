namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record PartnerCatalogCategoryResponse(
    Guid Id,
    Guid PartnerId,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record PartnerCatalogCategoryCreateRequest(string Name, int? SortOrder);

public sealed record PartnerCatalogCategoryUpdateRequest(
    string? Name,
    int? SortOrder,
    bool? IsActive);
