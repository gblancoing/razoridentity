using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;

namespace ComunaClick.SharedUI.Services;

public sealed class AuthStateService
{
    private readonly ITokenStore _tokenStore;

    public AuthStateService(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public event Action? OnChange;

    public AuthTokens? Tokens { get; private set; }

    public bool IsAuthenticated => Tokens is not null && Tokens.ExpiresAt > DateTimeOffset.UtcNow;
    public bool IsPersonalAccount => IsAuthenticated && (!PartnerId.HasValue || PartnerId == Guid.Empty);
    public bool IsPartnerAccount => IsAuthenticated && PartnerId.HasValue && PartnerId != Guid.Empty;

    public Guid? TenantId => Tokens?.TenantId;
    public Guid? PartnerId => Tokens?.PartnerId;
    public string? DisplayName => Tokens?.DisplayName;
    public string? Email => Tokens?.Email;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Tokens = await _tokenStore.GetAsync(cancellationToken);
        OnChange?.Invoke();
    }

    public async Task SetTokensAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        Tokens = tokens;
        await _tokenStore.SaveAsync(tokens, cancellationToken);
        OnChange?.Invoke();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Tokens = null;
        await _tokenStore.ClearAsync(cancellationToken);
        OnChange?.Invoke();
    }
}
