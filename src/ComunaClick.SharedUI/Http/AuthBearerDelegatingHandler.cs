using System.Net.Http.Headers;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Http;

/// <summary>
/// Adjunta el JWT del circuito Blazor actual a cada llamada HTTP saliente (evita desfase con localStorage).
/// </summary>
public sealed class AuthBearerDelegatingHandler : DelegatingHandler
{
    private readonly AuthStateService _authState;
    private readonly ITokenStore _tokenStore;

    public AuthBearerDelegatingHandler(AuthStateService authState, ITokenStore tokenStore)
    {
        _authState = authState;
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            await _authState.InitializeAsync(cancellationToken);
            var accessToken = _authState.Tokens?.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var stored = await _tokenStore.GetAsync(cancellationToken);
                accessToken = stored?.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
