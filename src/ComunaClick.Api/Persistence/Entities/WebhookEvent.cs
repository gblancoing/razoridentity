namespace ComunaClick.Api.Persistence.Entities;

public sealed class WebhookEvent
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string? ResourceId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public bool SignatureValid { get; set; }
    public bool Processed { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
