namespace ComunaClick.Api.Persistence.Entities;

public sealed class PartnerStaff
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "staff";
    public DateTimeOffset CreatedAt { get; set; }
}
