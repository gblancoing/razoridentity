namespace ComunaClick.Api.Persistence.Entities;

public sealed class ShoppingCart
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "active";
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ShoppingCartItem> Items { get; set; } = new List<ShoppingCartItem>();
}
