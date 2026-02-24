namespace ComunaClick.Shared.Auth.Acl;

public sealed record SocialLoginRequestDto(
    string IdToken,
    string? AccessToken,
    string? AuthorizationCode,
    Guid? TenantId,
    Guid? PartnerId,
    string? DeviceId,
    string? UserAgent
);
