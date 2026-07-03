using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;
using Microsoft.Extensions.Options;

namespace ComunaClick.SharedUI.Services;

public sealed class AuthStateService
{
    private readonly ITokenStore _tokenStore;
    private readonly SessionTokenHolder _sessionTokens;
    private readonly IAuthClient _authClient;
    private readonly ITokenRefresher _tokenRefresher;
    private readonly ApiOptions? _apiOptions;

    public AuthStateService(
        ITokenStore tokenStore,
        SessionTokenHolder sessionTokens,
        IAuthClient authClient,
        ITokenRefresher tokenRefresher,
        IOptions<ApiOptions>? apiOptions = null)
    {
        _tokenStore = tokenStore;
        _sessionTokens = sessionTokens;
        _authClient = authClient;
        _tokenRefresher = tokenRefresher;
        _apiOptions = apiOptions?.Value;
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
        Tokens = _sessionTokens.Current ?? await _tokenStore.GetAsync(cancellationToken);
        _sessionTokens.Current = Tokens;

        if (Tokens is null)
        {
            // Pestaña nueva: sessionStorage vacío, pero puede existir cookie de refresh válida (7 días).
            var restored = await _tokenRefresher.RefreshAsync(null, cancellationToken);
            if (restored is not null)
            {
                Tokens = restored;
                _sessionTokens.Current = restored;
            }

            OnChange?.Invoke();
            return;
        }

        var tenantId = ResolveTenantId(Tokens);
        var accessExpired = Tokens.ExpiresAt <= DateTimeOffset.UtcNow;
        var jwtMissingTenant = tenantId.HasValue && !JwtHasTenantClaim(Tokens.AccessToken);

        if (accessExpired || jwtMissingTenant)
        {
            // El refresco depende del mecanismo del host (cookie en web / refresh token en mobile).
            // Pasamos el tenant resuelto para no perder el scope al renovar.
            var refreshed = await _tokenRefresher.RefreshAsync(Tokens with { TenantId = tenantId }, cancellationToken);
            if (refreshed is not null)
            {
                Tokens = refreshed;
                _sessionTokens.Current = refreshed;
            }
        }

        OnChange?.Invoke();
    }

    /// <summary>
    /// Reobtiene un token acotado a un tenant/partner concreto (cambio de "negocio activo"). En web usa
    /// la cookie de refresh; en mobile el refresh token guardado. Devuelve false si no se pudo re-scopear.
    /// </summary>
    public async Task<bool> SwitchScopeAsync(Guid tenantId, Guid? partnerId, CancellationToken cancellationToken = default)
    {
        var basis = Tokens ?? _sessionTokens.Current
            ?? new AuthTokens(string.Empty, string.Empty, DateTimeOffset.MinValue);
        var current = basis with { TenantId = tenantId, PartnerId = partnerId };

        var refreshed = await _tokenRefresher.RefreshAsync(current, cancellationToken);
        if (refreshed is null)
        {
            return false;
        }

        Tokens = refreshed;
        _sessionTokens.Current = refreshed;
        OnChange?.Invoke();
        return true;
    }

    public async Task SetTokensAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        Tokens = tokens;
        _sessionTokens.Current = tokens;
        await _tokenStore.SaveAsync(tokens, cancellationToken);
        OnChange?.Invoke();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // Mobile guarda el refresh token: revócalo en ACL. En web el campo va vacío (el refresh vive
        // en la cookie) y la revocación + borrado de cookie la hace WebTokenStore.ClearAsync vía JS.
        var refreshToken = Tokens?.RefreshToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _authClient.LogoutAsync(refreshToken, cancellationToken);
            }
            catch (HttpRequestException)
            {
            }
        }

        Tokens = null;
        _sessionTokens.Current = null;
        await _tokenStore.ClearAsync(cancellationToken);
        OnChange?.Invoke();
    }

    private Guid? ResolveTenantId(AuthTokens tokens)
    {
        if (tokens.TenantId is Guid tid && tid != Guid.Empty)
        {
            return tid;
        }

        if (_apiOptions?.DefaultTenantId is Guid defaultTenant && defaultTenant != Guid.Empty)
        {
            return defaultTenant;
        }

        return null;
    }

    private static bool JwtHasTenantClaim(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        return JwtHelper.GetGuidClaim(accessToken, "tenant_id").HasValue;
    }
}
