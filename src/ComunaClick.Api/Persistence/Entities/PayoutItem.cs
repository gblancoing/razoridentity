namespace ComunaClick.Api.Persistence.Entities;

public sealed class PayoutItem
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid PartnerId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SubscriptionDeduction { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateTimeOffset CreatedAt { get; set; }

    public PayoutBatch Batch { get; set; } = null!;
}
