namespace Payments.Gateway.Api.Persistence.Entities;

public sealed class ProviderEvent
{
    public Guid Id { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public Guid IntentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset ReceivedAt { get; set; }

    public PaymentIntent Intent { get; set; } = null!;
}
