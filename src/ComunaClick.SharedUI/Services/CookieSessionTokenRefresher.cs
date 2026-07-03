using System.Text.Json;
using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;
using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Refresco de sesión para la web: dispara un fetch same-origin a /auth/session/refresh vía JS, de
/// modo que la cookie HttpOnly con el refresh token se adjunta sola. El resultado (access token) se
/// guarda en sessionStorage. Serializa refrescos concurrentes con un semáforo porque ACL rota el
/// refresh token (single-use): dos refrescos en paralelo revocarían el token del segundo y matarían
/// la sesión.
/// </summary>
public sealed class CookieSessionTokenRefresher : ITokenRefresher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;
    private readonly ITokenStore _tokenStore;
    private readonly SessionTokenHolder _sessionTokens;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CookieSessionTokenRefresher(IJSRuntime jsRuntime, ITokenStore tokenStore, SessionTokenHolder sessionTokens)
    {
        _jsRuntime = jsRuntime;
        _tokenStore = tokenStore;
        _sessionTokens = sessionTokens;
    }

    public async Task<AuthTokens?> RefreshAsync(AuthTokens? current, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Otra llamada concurrente pudo refrescar mientras esperábamos el semáforo: reutiliza ese
            // resultado en vez de rotar la cookie de nuevo.
            var latest = _sessionTokens.Current;
            if (latest is not null
                && !string.IsNullOrWhiteSpace(latest.AccessToken)
                && latest.ExpiresAt > DateTimeOffset.UtcNow
                && (current is null || !string.Equals(latest.AccessToken, current.AccessToken, StringComparison.Ordinal)))
            {
                return latest;
            }

            // Restauración en pestaña nueva (current == null): solo intenta el fetch si alguna vez hubo
            // sesión en este navegador, para no golpear /auth/session/refresh en usuarios anónimos.
            if (current is null)
            {
                try
                {
                    var hadSession = await _jsRuntime.InvokeAsync<bool>("comunaclic.hadSession", cancellationToken);
                    if (!hadSession)
                    {
                        return null;
                    }
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
                catch (JSDisconnectedException)
                {
                    return null;
                }
            }

            var tenantId = current?.TenantId?.ToString() ?? string.Empty;
            var partnerId = current?.PartnerId?.ToString() ?? string.Empty;

            string json;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("comunaclic.sessionRefresh", cancellationToken, tenantId, partnerId);
            }
            catch (InvalidOperationException)
            {
                // Prerender: no hay circuito para hacer el fetch todavía.
                return null;
            }
            catch (JSDisconnectedException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var session = JsonSerializer.Deserialize<SessionResponse>(json, JsonOptions);
            if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                return null;
            }

            var tokens = session.ToTokens();
            _sessionTokens.Current = tokens;
            await _tokenStore.SaveAsync(tokens, cancellationToken);
            return tokens;
        }
        finally
        {
            _gate.Release();
        }
    }
}
