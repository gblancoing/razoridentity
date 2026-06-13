using Payments.App.Models;

namespace Payments.App.Services;

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

    // Viewer: puede ver todas las páginas de lectura.
    public bool IsViewer =>
        IsAuthenticated &&
        Tokens!.Roles.Any(role =>
            role.Equals("payments.viewer",   StringComparison.OrdinalIgnoreCase) ||
            role.Equals("payments.operator", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("payments.admin",    StringComparison.OrdinalIgnoreCase) ||
            role.Equals("tenant_admin",      StringComparison.OrdinalIgnoreCase) ||
            role.Equals("platform_admin",    StringComparison.OrdinalIgnoreCase));

    // Operator: puede ejecutar reintentos, revisiones y cancelaciones.
    public bool IsOperator =>
        IsAuthenticated &&
        Tokens!.Roles.Any(role =>
            role.Equals("payments.operator", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("payments.admin",    StringComparison.OrdinalIgnoreCase) ||
            role.Equals("tenant_admin",      StringComparison.OrdinalIgnoreCase) ||
            role.Equals("platform_admin",    StringComparison.OrdinalIgnoreCase));

    // Admin: acceso completo al gateway.
    public bool IsAdmin =>
        IsAuthenticated &&
        Tokens!.Roles.Any(role =>
            role.Equals("payments.admin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("tenant_admin",   StringComparison.OrdinalIgnoreCase) ||
            role.Equals("platform_admin", StringComparison.OrdinalIgnoreCase));

    // Backwards compat: alias de IsViewer para código existente.
    public bool IsPaymentsAdmin => IsViewer;

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
