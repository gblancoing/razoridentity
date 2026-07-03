using System.Text.Json;
using ComunaClick.Shared.Auth;
using ComunaClick.Shared.Auth.Interfaces;
using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

public sealed class WebTokenStore : ITokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;
    private readonly SessionTokenHolder _sessionTokens;

    public WebTokenStore(IJSRuntime jsRuntime, SessionTokenHolder sessionTokens)
    {
        _jsRuntime = jsRuntime;
        _sessionTokens = sessionTokens;
    }

    public async Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionTokens.Current is not null)
        {
            return _sessionTokens.Current;
        }

        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("comunaclic.getTokens", cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var tokens = JsonSerializer.Deserialize<AuthTokens>(json, JsonOptions);
            _sessionTokens.Current = tokens;
            return tokens;
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
        _sessionTokens.Current = tokens;
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
        _sessionTokens.Current = null;
        try
        {
            // Revoca el refresh token en ACL y borra la cookie HttpOnly del host, además del sessionStorage.
            await _jsRuntime.InvokeVoidAsync("comunaclic.sessionLogout", cancellationToken);
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
