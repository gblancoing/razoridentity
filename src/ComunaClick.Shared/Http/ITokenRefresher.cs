using ComunaClick.Shared.Auth;

namespace ComunaClick.Shared.Http;

/// <summary>
/// Renueva la sesión sin exponer el mecanismo al llamador. En web el refresh token vive en una
/// cookie HttpOnly (el refresco se dispara por fetch same-origin); en mobile se usa el refresh
/// token guardado en almacenamiento seguro. Devuelve null si no se pudo renovar.
/// </summary>
public interface ITokenRefresher
{
    Task<AuthTokens?> RefreshAsync(AuthTokens? current, CancellationToken cancellationToken = default);
}
