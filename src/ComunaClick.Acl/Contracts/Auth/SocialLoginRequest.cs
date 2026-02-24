namespace ComunaClick.Acl.Contracts.Auth;

public sealed record SocialLoginRequest(
    string? IdToken,
    string? AccessToken,
    string? AuthorizationCode,
    Guid? TenantId,
    Guid? PartnerId,
    string? DeviceId,
    string? UserAgent
);
