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
    private readonly ApiOptions? _apiOptions;

    public AuthStateService(
        ITokenStore tokenStore,
        SessionTokenHolder sessionTokens,
        IAuthClient authClient,
        IOptions<ApiOptions>? apiOptions = null)
    {
        _tokenStore = tokenStore;
        _sessionTokens = sessionTokens;
        _authClient = authClient;
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
            OnChange?.Invoke();
            return;
        }

        var tenantId = ResolveTenantId(Tokens);
        var accessExpired = Tokens.ExpiresAt <= DateTimeOffset.UtcNow;
        var jwtMissingTenant = tenantId.HasValue && !JwtHasTenantClaim(Tokens.AccessToken);

        if ((accessExpired || jwtMissingTenant)
            && !string.IsNullOrWhiteSpace(Tokens.RefreshToken)
            && _authClient is not null)
        {
            try
            {
                var refreshed = await _authClient.RefreshAsync(
                    Tokens.RefreshToken,
                    tenantId,
                    Tokens.PartnerId,
                    cancellationToken);
                Tokens = refreshed;
                _sessionTokens.Current = refreshed;
                await _tokenStore.SaveAsync(refreshed, cancellationToken);
            }
            catch (HttpRequestException)
            {
            }
        }

        OnChange?.Invoke();
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
