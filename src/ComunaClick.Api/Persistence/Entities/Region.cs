namespace ComunaClick.Api.Persistence.Entities;

public sealed class Region
{
    public Guid Id { get; set; }
    public Guid CountryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? LegacyRegionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Country? Country { get; set; }
    public ICollection<Comuna> Comunas { get; set; } = new List<Comuna>();
}
