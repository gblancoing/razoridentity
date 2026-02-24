namespace ComunaClick.Api.Persistence.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Santiago";
    public string ConfigJson { get; set; } = "{}";
    public Guid? ComunaId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Partner> Partners { get; set; } = new List<Partner>();
    public Comuna? Comuna { get; set; }
}
