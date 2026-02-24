namespace ComunaClick.Acl.Contracts.Users;

public sealed record UserTenantScopeCreateRequest(Guid TenantId, Guid? PartnerId, string? ScopeType);
