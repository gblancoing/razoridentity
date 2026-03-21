namespace ComunaClick.Api.Persistence.Entities;

public sealed class Professional
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Specialty { get; set; }
    public string? Bio { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
