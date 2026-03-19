namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProductSubcategory
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductCategory Category { get; set; } = null!;
    public ICollection<Partner> Partners { get; set; } = new List<Partner>();
}
