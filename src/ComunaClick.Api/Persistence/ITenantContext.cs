namespace ComunaClick.Api.Persistence;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? PartnerId { get; }
    void Set(Guid? tenantId, Guid? partnerId);
}
