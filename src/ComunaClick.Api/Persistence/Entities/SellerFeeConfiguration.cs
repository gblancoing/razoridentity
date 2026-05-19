namespace ComunaClick.Api.Persistence.Entities;

public sealed class SellerFeeConfiguration
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public decimal FixedFeeAmount { get; set; }
    public decimal PercentageFee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Seller Seller { get; set; } = null!;
}
