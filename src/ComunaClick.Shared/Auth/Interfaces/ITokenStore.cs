namespace ComunaClick.Shared.Auth.Interfaces;

public interface ITokenStore
{
    Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
