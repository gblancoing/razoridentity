namespace ComunaClick.Acl.Contracts.Auth;

public sealed record RefreshRequest(
    string RefreshToken,
    Guid? TenantId,
    Guid? PartnerId);
