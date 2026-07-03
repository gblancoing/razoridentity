using System.Net.Http.Json;
using ComunaClick.Shared.Auth.Interfaces;

namespace ComunaClick.Shared.Auth.Acl;

public sealed class AclAuthClient : IAuthClient
{
    private readonly HttpClient _httpClient;

    public AclAuthClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var dto = new LoginRequestDto(request.Email, request.Password, request.TenantId, request.PartnerId, request.RecaptchaToken);
        var response = await _httpClient.PostAsJsonAsync("/v1/auth/login", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        return ToTokens(auth);
    }

    public async Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/v1/auth/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        return ToTokens(auth);
    }

    public async Task<AuthTokens> LoginExternalAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default)
    {
        var dto = new SocialLoginRequestDto(
            request.IdToken,
            request.AccessToken,
            request.AuthorizationCode,
            request.TenantId,
            request.PartnerId,
            null,
            null);
        var path = request.Provider == ExternalProvider.Apple ? "/v1/auth/apple" : "/v1/auth/google";
        var response = await _httpClient.PostAsJsonAsync(path, dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        return ToTokens(auth);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, Guid? tenantId = null, Guid? partnerId = null, CancellationToken cancellationToken = default)
    {
        var dto = new RefreshRequestDto(refreshToken, tenantId, partnerId);
        var response = await _httpClient.PostAsJsonAsync("/v1/auth/refresh", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        return ToTokens(auth);
    }

    public async Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        try
        {
            await _httpClient.PostAsJsonAsync("/v1/auth/logout", new { RefreshToken = refreshToken }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Logout es best-effort: si ACL no responde, el borrado de cookie ya cierra la sesión del navegador.
        }
    }

    private static AuthTokens ToTokens(AuthResponse? auth)
    {
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken) || string.IsNullOrWhiteSpace(auth.RefreshToken))
        {
            throw new InvalidOperationException("Invalid auth response.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresIn);
        var tenantId = auth.User?.TenantId ?? JwtHelper.GetGuidClaim(auth.AccessToken, "tenant_id");
        var partnerId = auth.User?.PartnerId ?? JwtHelper.GetGuidClaim(auth.AccessToken, "partner_id");
        var displayName = auth.User?.DisplayName ?? JwtHelper.GetStringClaim(auth.AccessToken, "name");
        var email = auth.User?.Email ?? JwtHelper.GetStringClaim(auth.AccessToken, "email");
        return new AuthTokens(auth.AccessToken, auth.RefreshToken, expiresAt, tenantId, partnerId, displayName, email);
    }
}
