namespace ComunaClick.Shared.Auth;

public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    Guid? TenantId = null,
    Guid? PartnerId = null,
    string? DisplayName = null,
    string? Email = null
);
