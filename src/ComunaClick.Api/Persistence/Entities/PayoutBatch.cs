namespace ComunaClick.Api.Persistence.Entities;

public sealed class PayoutBatch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PayoutItem> Items { get; set; } = new List<PayoutItem>();
}
