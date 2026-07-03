using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;
using Microsoft.Extensions.Options;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Sincroniza tokens entre AuthState, almacenamiento del circuito y llamadas HTTP a la API.
/// El refresco delega en <see cref="ITokenRefresher"/> (cookie HttpOnly en web / refresh token
/// guardado en mobile); por eso NO exige que el campo RefreshToken esté presente en el token web.
/// </summary>
public sealed class ApiSessionService
{
    private readonly AuthStateService _authState;
    private readonly ITokenStore _tokenStore;
    private readonly ITokenRefresher _tokenRefresher;
    private readonly ApiOptions _apiOptions;

    public ApiSessionService(
        AuthStateService authState,
        ITokenStore tokenStore,
        ITokenRefresher tokenRefresher,
        IOptions<ApiOptions> apiOptions)
    {
        _authState = authState;
        _tokenStore = tokenStore;
        _tokenRefresher = tokenRefresher;
        _apiOptions = apiOptions.Value;
    }

    public async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        await _authState.InitializeAsync(cancellationToken);
        var tokens = _authState.Tokens;
        if (tokens is null)
        {
            return false;
        }

        await _tokenStore.SaveAsync(tokens, cancellationToken);

        var tenantId = ResolveTenantId(tokens);
        // Ya NO gatillamos refresh solo por no tener refresh token en el campo (en web vive en la cookie).
        var needsRefresh = !_authState.IsAuthenticated
            || !tenantId.HasValue
            || !JwtHasTenantClaim(tokens.AccessToken);

        if (!needsRefresh)
        {
            return _authState.IsAuthenticated && ResolveTenantId(_authState.Tokens) is not null;
        }

        var refreshed = await _tokenRefresher.RefreshAsync(tokens with { TenantId = tenantId }, cancellationToken);
        if (refreshed is not null)
        {
            await _authState.SetTokensAsync(refreshed, cancellationToken);
        }

        return _authState.IsAuthenticated && ResolveTenantId(_authState.Tokens) is not null;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        await _authState.InitializeAsync(cancellationToken);
        var tokens = _authState.Tokens ?? await _tokenStore.GetAsync(cancellationToken);
        if (tokens is null)
        {
            return false;
        }

        var refreshed = await _tokenRefresher.RefreshAsync(
            tokens with { TenantId = ResolveTenantId(tokens) },
            cancellationToken);
        if (refreshed is null)
        {
            return false;
        }

        await _authState.SetTokensAsync(refreshed, cancellationToken);
        return _authState.IsAuthenticated;
    }

    private Guid? ResolveTenantId(AuthTokens? tokens)
    {
        if (tokens?.TenantId is Guid tid && tid != Guid.Empty)
        {
            return tid;
        }

        if (_apiOptions.DefaultTenantId is Guid defaultTenant && defaultTenant != Guid.Empty)
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

        return ComunaClick.Shared.Auth.JwtHelper.GetGuidClaim(accessToken, "tenant_id").HasValue;
    }
}
