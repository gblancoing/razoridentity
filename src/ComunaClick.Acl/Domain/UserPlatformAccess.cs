namespace ComunaClick.Acl.Domain;

public sealed class UserPlatformAccess
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
}
