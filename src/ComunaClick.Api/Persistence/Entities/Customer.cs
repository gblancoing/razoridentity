namespace ComunaClick.Api.Persistence.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    /// <summary>URL https de imagen de perfil (sin binarios en BD).</summary>
    public string? AvatarUrl { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
