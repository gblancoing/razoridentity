namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProductDiscoverySubcategory
{
    public Guid ProductId { get; set; }
    public Guid SubcategoryId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ProductSubcategory Subcategory { get; set; } = null!;
}
