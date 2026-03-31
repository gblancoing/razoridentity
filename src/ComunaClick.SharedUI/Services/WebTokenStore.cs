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
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("comunaclic.getTokens", cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AuthTokens>(json);
        }
        catch (InvalidOperationException)
        {
            // Blazor Server prerender: JS interop is not available yet.
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(tokens);
            await _jsRuntime.InvokeVoidAsync("comunaclic.setTokens", cancellationToken, json);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("comunaclic.clearTokens", cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
