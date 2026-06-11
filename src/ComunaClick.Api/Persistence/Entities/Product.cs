namespace ComunaClick.Api.Persistence.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ProductAddress { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? PartnerCatalogCategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CostPrice { get; set; }
    public string Currency { get; set; } = "CLP";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ProductInventory? Inventory { get; set; }
    public PartnerCatalogCategory? PartnerCatalogCategory { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<ProductImageSnapshot> Images { get; set; } = Array.Empty<ProductImageSnapshot>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CatalogCategoryName { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<Guid> DiscoverySubcategoryIds { get; set; } = Array.Empty<Guid>();
}
