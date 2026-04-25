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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
