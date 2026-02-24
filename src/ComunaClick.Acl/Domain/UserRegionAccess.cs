namespace ComunaClick.Acl.Domain;

public sealed class UserRegionAccess
{
    public Guid UserId { get; set; }
    public Guid RegionId { get; set; }

    public User? User { get; set; }
}
