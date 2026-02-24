namespace ComunaClick.Api.Persistence.Entities;

public sealed class PaymentEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset ReceivedAt { get; set; }
}
