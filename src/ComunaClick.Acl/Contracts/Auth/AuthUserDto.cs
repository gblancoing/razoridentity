namespace ComunaClick.Acl.Contracts.Auth;

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    Guid? TenantId,
    Guid? PartnerId);
