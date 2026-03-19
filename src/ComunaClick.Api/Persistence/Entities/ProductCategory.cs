namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProductCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductSubcategory> Subcategories { get; set; } = new List<ProductSubcategory>();
}
