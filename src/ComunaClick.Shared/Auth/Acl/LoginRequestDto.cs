namespace ComunaClick.Shared.Auth.Acl;

public sealed record LoginRequestDto(
    string? Email,
    string? Password,
    Guid? TenantId,
    Guid? PartnerId
);
