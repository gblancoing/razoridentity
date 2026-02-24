namespace ComunaClick.Acl.Domain;

public sealed class UserTenantAccess
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    public User? User { get; set; }
}
