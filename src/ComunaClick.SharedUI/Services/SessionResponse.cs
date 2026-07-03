using System.Text.Json;
using ComunaClick.Shared.Auth;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Respuesta de los endpoints /auth/session/* del host. NO incluye refresh token: ese viaja solo
/// en la cookie HttpOnly y nunca es accesible desde JS.
/// </summary>
public sealed record SessionResponse(
    string AccessToken,
    int ExpiresIn,
    Guid? TenantId,
    Guid? PartnerId,
    string? DisplayName,
    string? Email)
{
    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Parsea el JSON devuelto por los helpers comunaclic.session*; null si vino vacío/erróneo.</summary>
    public static SessionResponse? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var session = JsonSerializer.Deserialize<SessionResponse>(json, ParseOptions);
            return string.IsNullOrWhiteSpace(session?.AccessToken) ? null : session;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public AuthTokens ToTokens() => new(
        AccessToken,
        // El refresh token vive en la cookie del host; el store del navegador no lo conoce.
        string.Empty,
        DateTimeOffset.UtcNow.AddSeconds(ExpiresIn),
        TenantId,
        PartnerId,
        DisplayName,
        Email);
}
