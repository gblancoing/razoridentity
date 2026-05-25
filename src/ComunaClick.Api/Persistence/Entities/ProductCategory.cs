namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProductCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>commerce = comercio (tipo A); service = empresa de servicios (tipo B).</summary>
    public string CatalogScope { get; set; } = "commerce";

    public ICollection<ProductSubcategory> Subcategories { get; set; } = new List<ProductSubcategory>();
}
