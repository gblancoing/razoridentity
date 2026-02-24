namespace ComunaClick.Api.Persistence;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? PartnerId { get; private set; }

    public void Set(Guid? tenantId, Guid? partnerId)
    {
        TenantId = tenantId;
        PartnerId = partnerId;
    }
}
