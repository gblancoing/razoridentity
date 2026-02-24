namespace ComunaClick.Api.Persistence.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = "transbank";
    public string ExternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string Status { get; set; } = "pending";
    public Guid? GatewayIntentId { get; set; }
    public string? ProviderToken { get; set; }
    public string? LastEventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
