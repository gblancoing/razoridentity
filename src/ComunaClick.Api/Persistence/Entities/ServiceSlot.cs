namespace ComunaClick.Api.Persistence.Entities;

public sealed class ServiceSlot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int Capacity { get; set; } = 1;
    public bool IsAvailable { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
