namespace ComunaClick.Shared.Auth.Acl;

public sealed record RefreshRequestDto(
    string? RefreshToken,
    Guid? TenantId,
    Guid? PartnerId
);
