namespace ComunaClick.Shared.Auth;

public sealed record RefreshRequest(string RefreshToken, Guid? TenantId = null, Guid? PartnerId = null);
