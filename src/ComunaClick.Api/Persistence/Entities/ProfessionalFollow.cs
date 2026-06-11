namespace ComunaClick.Api.Persistence.Entities;

public sealed class ProfessionalFollow
{
    public Guid Id { get; set; }
    public Guid FollowerProfessionalId { get; set; }
    public string FollowedType { get; set; } = string.Empty;
    public Guid FollowedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
