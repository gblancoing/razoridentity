namespace ComunaClick.Api.Persistence.Entities;

public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "payment_pending";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string CancellationPolicy { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
