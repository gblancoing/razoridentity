using ComunaClick.Admin.Models;

namespace ComunaClick.Admin.Services;

public sealed class PaymentsAuthStateService
{
    private readonly PaymentsWebTokenStore _tokenStore;

    public PaymentsAuthStateService(PaymentsWebTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public event Action? OnChange;

    public PaymentsAuthTokens? Tokens { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated => Tokens is not null && Tokens.ExpiresAt > DateTimeOffset.UtcNow;
    public bool IsPaymentsAdmin =>
        IsAuthenticated &&
        Tokens!.Roles.Any(role =>
            string.Equals(role, "tenant_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "platform_admin", StringComparison.OrdinalIgnoreCase));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        Tokens = await _tokenStore.GetAsync(cancellationToken);
        IsInitialized = true;
        OnChange?.Invoke();
    }

    public async Task SetTokensAsync(PaymentsAuthTokens tokens, CancellationToken cancellationToken = default)
    {
        Tokens = tokens;
        IsInitialized = true;
        await _tokenStore.SaveAsync(tokens, cancellationToken);
        OnChange?.Invoke();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Tokens = null;
        IsInitialized = true;
        await _tokenStore.ClearAsync(cancellationToken);
        OnChange?.Invoke();
    }
}
