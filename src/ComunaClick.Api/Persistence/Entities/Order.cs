namespace ComunaClick.Api.Persistence.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid CustomerId { get; set; }
    public string? ExternalReference { get; set; }
    public string Status { get; set; } = "payment_pending";
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string? BuyerEmail { get; set; }
    public string? BuyerName { get; set; }
    public Guid? DeliveryProviderId { get; set; }
    public string? DeliveryProviderName { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
