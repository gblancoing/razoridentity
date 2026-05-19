namespace ComunaClick.Api.Persistence.Entities;

public sealed class Interaction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? PartnerId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
