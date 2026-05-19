namespace ComunaClick.Api.Persistence.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
