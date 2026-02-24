namespace ComunaClick.Acl.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    AuthUserDto User);
