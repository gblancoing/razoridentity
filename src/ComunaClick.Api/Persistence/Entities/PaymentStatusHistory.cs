namespace ComunaClick.Api.Persistence.Entities;

public sealed class PaymentStatusHistory
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? RawPayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
