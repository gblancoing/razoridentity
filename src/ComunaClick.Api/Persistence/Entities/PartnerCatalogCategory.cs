namespace ComunaClick.Api.Persistence.Entities;

public sealed class PartnerCatalogCategory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
}
