namespace ComunaClick.Api.Persistence.Entities;

public sealed class Lead
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProfessionalId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "new";
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
