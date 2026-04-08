using System.Text.Json;
using Microsoft.JSInterop;
using ComunaClick.Admin.Models;

namespace ComunaClick.Admin.Services;

public sealed class PaymentsWebTokenStore
{
    private readonly IJSRuntime _jsRuntime;

    public PaymentsWebTokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<PaymentsAuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("comunaclicAdmin.getTokens", cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PaymentsAuthTokens>(json);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async Task SaveAsync(PaymentsAuthTokens tokens, CancellationToken cancellationToken = default)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("comunaclicAdmin.setTokens", cancellationToken, JsonSerializer.Serialize(tokens));
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
            await _jsRuntime.InvokeVoidAsync("comunaclicAdmin.clearTokens", cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
