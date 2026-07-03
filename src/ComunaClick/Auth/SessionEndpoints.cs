using System.Net;
using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.SharedUI.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ComunaClick.App.Auth;

/// <summary>
/// Capa fina de sesión del host: emite/renueva/revoca una cookie HttpOnly con el refresh token para
/// que este nunca sea accesible desde JS. El navegador llama estos endpoints (mismo origen, la cookie
/// se adjunta sola); el host habla server-to-server con ACL reutilizando <see cref="IAuthClient"/>.
/// El body de respuesta lleva SOLO el access token (nunca el refresh token).
/// </summary>
public static class SessionEndpoints
{
    private const string RefreshCookieName = "__Host-cc_rt";

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Endpoints JSON, exentos del antiforgery basado en formularios (se aplica en la creación del
        // grupo para que la convención cubra todos los endpoints). La defensa CSRF es SameSite=Strict
        // + validación de Origin + header X-CC-Session (ver IsTrustedRequest).
        var group = endpoints.MapGroup("/auth/session").DisableAntiforgery();

        group.MapPost("/login", async (
            HttpContext http,
            SessionLoginRequest request,
            IAuthClient authClient,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (!IsTrustedRequest(http))
            {
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            }

            return await IssueSessionAsync(http, config, ct, () => authClient.LoginAsync(
                new LoginRequest(request.Email, request.Password, request.TenantId, request.PartnerId, request.RecaptchaToken), ct));
        });

        group.MapPost("/register", async (
            HttpContext http,
            SessionRegisterRequest request,
            IAuthClient authClient,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (!IsTrustedRequest(http))
            {
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            }

            return await IssueSessionAsync(http, config, ct, () => authClient.RegisterAsync(
                new RegisterRequest(request.Name, request.Email, request.Password, request.RecaptchaToken), ct));
        });

        group.MapPost("/google", (
            HttpContext http,
            SessionExternalRequest request,
            IAuthClient authClient,
            IConfiguration config,
            CancellationToken ct) => ExternalLoginAsync(http, request, ExternalProvider.Google, authClient, config, ct));

        group.MapPost("/apple", (
            HttpContext http,
            SessionExternalRequest request,
            IAuthClient authClient,
            IConfiguration config,
            CancellationToken ct) => ExternalLoginAsync(http, request, ExternalProvider.Apple, authClient, config, ct));

        group.MapPost("/refresh", async (
            HttpContext http,
            SessionRefreshRequest request,
            IAuthClient authClient,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (!IsTrustedRequest(http))
            {
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var refreshToken = http.Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                // Cuerpo JSON explícito: evita que UseStatusCodePagesWithReExecute re-ejecute la
                // respuesta vacía hacia /not-found (que a su vez fallaría el antiforgery con 400).
                return Results.Json(new { error = "no_session" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            return await IssueSessionAsync(http, config, ct, () => authClient.RefreshAsync(
                refreshToken, request.TenantId, request.PartnerId, ct),
                onFailure: () => DeleteRefreshCookie(http));
        });

        group.MapPost("/logout", async (
            HttpContext http,
            IAuthClient authClient,
            CancellationToken ct) =>
        {
            if (!IsTrustedRequest(http))
            {
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var refreshToken = http.Request.Cookies[RefreshCookieName];
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await authClient.LogoutAsync(refreshToken, ct);
            }

            DeleteRefreshCookie(http);
            return Results.Ok();
        });

        return endpoints;
    }

    private static async Task<IResult> ExternalLoginAsync(
        HttpContext http,
        SessionExternalRequest request,
        ExternalProvider provider,
        IAuthClient authClient,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!IsTrustedRequest(http))
        {
            return Results.Forbid();
        }

        return await IssueSessionAsync(http, config, ct, () => authClient.LoginExternalAsync(
            new ExternalLoginRequest(provider, request.IdToken, TenantId: request.TenantId, PartnerId: request.PartnerId), ct));
    }

    private static async Task<IResult> IssueSessionAsync(
        HttpContext http,
        IConfiguration config,
        CancellationToken ct,
        Func<Task<AuthTokens>> authCall,
        Action? onFailure = null)
    {
        try
        {
            var tokens = await authCall();
            SetRefreshCookie(http, tokens.RefreshToken, config);
            var response = new SessionResponse(
                tokens.AccessToken,
                (int)Math.Max(0, (tokens.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
                tokens.TenantId,
                tokens.PartnerId,
                tokens.DisplayName,
                tokens.Email);
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            onFailure?.Invoke();
            // Propaga el código de ACL (401 credenciales inválidas, 400 validación, etc.) sin cuerpo sensible.
            // Cuerpo JSON explícito para no gatillar el re-execute de StatusCodePages.
            var status = ex.StatusCode is HttpStatusCode code ? (int)code : StatusCodes.Status502BadGateway;
            return Results.Json(new { error = "auth_failed" }, statusCode: status);
        }
        catch (InvalidOperationException)
        {
            onFailure?.Invoke();
            return Results.Json(new { error = "auth_unavailable" }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static void SetRefreshCookie(HttpContext http, string refreshToken, IConfiguration config)
    {
        var days = config.GetValue("Jwt:RefreshTokenDays", 7);
        http.Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(days)
        });
    }

    private static void DeleteRefreshCookie(HttpContext http)
    {
        // Debe repetir los mismos atributos del set; si no, el navegador no borra la cookie.
        http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        });
    }

    /// <summary>
    /// Defensa CSRF sin antiforgery explícito (el login es anónimo): exige el header custom
    /// <c>X-CC-Session</c> (fuerza preflight CORS, un form cross-site no lo puede enviar) y, si viene
    /// <c>Origin</c>, que coincida con el host. Combinado con SameSite=Strict en la cookie.
    /// </summary>
    private static bool IsTrustedRequest(HttpContext http)
    {
        if (!http.Request.Headers.ContainsKey("X-CC-Session"))
        {
            return false;
        }

        var origin = http.Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return string.Equals(originUri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase);
        }

        // Sin Origin (algunos navegadores lo omiten en same-origin): el header custom + SameSite bastan.
        return true;
    }

    public sealed record SessionLoginRequest(string Email, string Password, Guid? TenantId, Guid? PartnerId, string? RecaptchaToken);
    public sealed record SessionRegisterRequest(string Name, string Email, string Password, string? RecaptchaToken);
    public sealed record SessionExternalRequest(string IdToken, Guid? TenantId, Guid? PartnerId);
    public sealed record SessionRefreshRequest(Guid? TenantId, Guid? PartnerId);
}
