using System.Net.Http.Headers;
using System.Net.Http.Json;
using ComunaClick.Admin.Models;
using Microsoft.Extensions.Options;

namespace ComunaClick.Admin.Services;

public sealed class AdminAclManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentsAuthStateService _authState;
    private readonly PaymentsAuthOptions _options;

    public AdminAclManagementClient(HttpClient httpClient, IOptions<PaymentsAuthOptions> options, PaymentsAuthStateService authState)
    {
        _httpClient = httpClient;
        _authState = authState;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await GetAsync<List<AclUserDto>>("v1/users", cancellationToken) ?? new();
        var roles = await GetAsync<List<AdminRoleDto>>("v1/roles", cancellationToken) ?? new();
        var roleMap = roles.ToDictionary(x => x.Id, x => x.Name);

        var result = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            var userRoles = await GetAsync<List<AclRoleDto>>($"v1/users/{user.Id}/roles", cancellationToken) ?? new();
            result.Add(new AdminUserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                userRoles.Select(x => x.Name).OrderBy(x => x).ToArray()));
        }

        return result
            .OrderByDescending(x => x.Roles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase))
            .ThenBy(x => x.Email)
            .ToList();
    }

    public Task ToggleUserActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
        => PutNoContentAsync($"v1/users/{id}", new
        {
            email = (string?)null,
            password = (string?)null,
            displayName = (string?)null,
            isActive
        }, cancellationToken);

    public Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
        => PostNoContentAsync($"v1/users/{id}/reset-password", new { newPassword }, cancellationToken);

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task PutNoContentAsync(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, path);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task PostNoContentAsync(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var accessToken = _authState.Tokens?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException("No active admin session.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private sealed record AclUserDto(
        Guid Id,
        string Email,
        string? DisplayName,
        string? PasswordHash,
        bool IsActive,
        bool IsSuperAdmin,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record AclRoleDto(
        Guid Id,
        string Name);
}
