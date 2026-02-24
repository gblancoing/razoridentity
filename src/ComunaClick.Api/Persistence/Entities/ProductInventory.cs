namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProductInventory
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
