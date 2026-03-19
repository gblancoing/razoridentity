namespace ComunaClick.Api.Persistence.Entities;

public sealed class Comuna
{
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? LegacyComunaId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Region? Region { get; set; }
    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}
