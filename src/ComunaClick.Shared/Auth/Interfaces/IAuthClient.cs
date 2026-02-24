namespace ComunaClick.Shared.Auth.Interfaces;

public interface IAuthClient
{
    Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> LoginExternalAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
