using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;

using Microsoft.Extensions.Options;



namespace ComunaClick.SharedUI.Services;



/// <summary>

/// Sincroniza tokens entre AuthState, almacenamiento del circuito y llamadas HTTP a la API.

/// </summary>

public sealed class ApiSessionService

{

    private readonly AuthStateService _authState;

    private readonly ITokenStore _tokenStore;

    private readonly IAuthClient _authClient;

    private readonly ApiOptions _apiOptions;



    public ApiSessionService(

        AuthStateService authState,

        ITokenStore tokenStore,

        IAuthClient authClient,

        IOptions<ApiOptions> apiOptions)

    {

        _authState = authState;

        _tokenStore = tokenStore;

        _authClient = authClient;

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

        var needsRefresh = string.IsNullOrWhiteSpace(tokens.RefreshToken)

            || !_authState.IsAuthenticated

            || !tenantId.HasValue

            || !JwtHasTenantClaim(tokens.AccessToken);



        if (!needsRefresh)

        {

            return _authState.IsAuthenticated && ResolveTenantId(_authState.Tokens) is not null;

        }



        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))

        {

            return false;

        }



        try

        {

            var refreshed = await _authClient.RefreshAsync(

                tokens.RefreshToken,

                tenantId,

                tokens.PartnerId,

                cancellationToken);

            await _authState.SetTokensAsync(refreshed, cancellationToken);

            return _authState.IsAuthenticated && ResolveTenantId(_authState.Tokens) is not null;

        }

        catch (HttpRequestException)

        {

            return false;

        }

    }



    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)

    {

        await _authState.InitializeAsync(cancellationToken);

        var tokens = _authState.Tokens ?? await _tokenStore.GetAsync(cancellationToken);

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))

        {

            return false;

        }



        try

        {

            var refreshed = await _authClient.RefreshAsync(

                tokens.RefreshToken,

                ResolveTenantId(tokens),

                tokens.PartnerId,

                cancellationToken);

            await _authState.SetTokensAsync(refreshed, cancellationToken);

            return _authState.IsAuthenticated;

        }

        catch (HttpRequestException)

        {

            return false;

        }

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


