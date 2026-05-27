namespace ComunaClick.Api.Persistence.Entities;

public sealed class ServiceProfessional
{
    public Guid ServiceId { get; set; }
    public Guid ProfessionalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Service? Service { get; set; }
    public Professional? Professional { get; set; }
}
