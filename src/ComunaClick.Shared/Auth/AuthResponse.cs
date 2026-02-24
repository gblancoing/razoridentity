namespace ComunaClick.Shared.Auth;

public sealed record AuthUserDto(
    Guid Id,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string>? Roles,
    Guid? TenantId,
    Guid? PartnerId
);

public sealed record AuthResponse(
    string? AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? TokenType,
    AuthUserDto? User
);
