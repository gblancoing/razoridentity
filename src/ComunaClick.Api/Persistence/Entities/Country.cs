namespace ComunaClick.Api.Persistence.Entities;

public sealed class Country
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Region> Regions { get; set; } = new List<Region>();
}
