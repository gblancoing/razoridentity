namespace ComunaClick.Api.Persistence.Entities;

public sealed class ServiceImage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
