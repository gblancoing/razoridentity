using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;

namespace ComunaClick.Mobile.Services;

/// <summary>
/// Refresco de sesión para mobile: usa el refresh token guardado en el almacenamiento seguro del SO
/// (comportamiento previo). El hardening por cookie es específico del navegador y no aplica aquí.
/// </summary>
public sealed class AclTokenRefresher : ITokenRefresher
{
    private readonly IAuthClient _authClient;
    private readonly ITokenStore _tokenStore;

    public AclTokenRefresher(IAuthClient authClient, ITokenStore tokenStore)
    {
        _authClient = authClient;
        _tokenStore = tokenStore;
    }

    public async Task<AuthTokens?> RefreshAsync(AuthTokens? current, CancellationToken cancellationToken = default)
    {
        current ??= await _tokenStore.GetAsync(cancellationToken);
        if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            return null;
        }

        try
        {
            var refreshed = await _authClient.RefreshAsync(
                current.RefreshToken,
                current.TenantId,
                current.PartnerId,
                cancellationToken);
            await _tokenStore.SaveAsync(refreshed, cancellationToken);
            return refreshed;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
