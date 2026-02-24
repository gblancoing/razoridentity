using System.Text.Json;
using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

public sealed class WebTokenStore : ITokenStore
{
    private const string StorageKey = "comunaclic.tokens";
    private readonly IJSRuntime _jsRuntime;

    public WebTokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await _jsRuntime.InvokeAsync<string>("comunaclic.getTokens", cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AuthTokens>(json);
    }

    public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(tokens);
        await _jsRuntime.InvokeVoidAsync("comunaclic.setTokens", cancellationToken, json);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        return _jsRuntime.InvokeVoidAsync("comunaclic.clearTokens", cancellationToken).AsTask();
    }
}
