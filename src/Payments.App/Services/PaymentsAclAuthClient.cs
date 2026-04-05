using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Payments.App.Models;

namespace Payments.App.Services;

public sealed class PaymentsAclAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentsAuthOptions _options;

    public PaymentsAclAuthClient(HttpClient httpClient, IOptions<PaymentsAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<PaymentsAuthTokens> LoginAsync(string email, string password, string? recaptchaToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/auth/login", new LoginRequestDto(
            email,
            password,
            _options.DefaultTenantId,
            null,
            recaptchaToken), cancellationToken);

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("ACL did not return a valid auth response.");

        return new PaymentsAuthTokens(
            auth.AccessToken,
            auth.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresIn),
            auth.User.TenantId,
            auth.User.PartnerId,
            auth.User.DisplayName,
            auth.User.Email,
            auth.User.Roles);
    }

    private sealed record LoginRequestDto(
        string Email,
        string Password,
        Guid? TenantId,
        Guid? PartnerId,
        string? RecaptchaToken);

    private sealed record AuthResponseDto(
        string AccessToken,
        string RefreshToken,
        int ExpiresIn,
        string TokenType,
        AuthUserDto User);

    private sealed record AuthUserDto(
        Guid Id,
        string Email,
        string? DisplayName,
        IReadOnlyList<string> Roles,
        Guid? TenantId,
        Guid? PartnerId);
}
