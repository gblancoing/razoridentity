namespace ComunaClick.Acl.Domain;

public sealed class UserTenantScope
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid TenantId { get; set; }
    public Guid? PartnerId { get; set; }
    public string ScopeType { get; set; } = "tenant";
    public DateTimeOffset CreatedAt { get; set; }
}
