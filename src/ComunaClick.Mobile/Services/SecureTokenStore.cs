using System.Text.Json;
using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using Microsoft.Maui.Storage;

namespace ComunaClick.Mobile.Services;

public sealed class SecureTokenStore : ITokenStore
{
    private const string StorageKey = "comunaclic.tokens";

    public async Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AuthTokens>(json);
    }

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(tokens);
        return SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
