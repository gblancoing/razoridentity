namespace ComunaClick.Api.Persistence.Entities;

public sealed class PartnerCatalogCategory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Categoría padre (null = categoría raíz). Máximo dos niveles: categoría → subcategoría.</summary>
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
    public PartnerCatalogCategory? Parent { get; set; }
    public ICollection<PartnerCatalogCategory> Children { get; set; } = new List<PartnerCatalogCategory>();
}
