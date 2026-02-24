using ComunaClick.Shared.Auth.Interfaces;

namespace ComunaClick.Shared.Auth.Stores;

public sealed class InMemoryTokenStore : ITokenStore
{
    private AuthTokens? _tokens;

    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens);
    }

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        _tokens = tokens;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _tokens = null;
        return Task.CompletedTask;
    }
}
