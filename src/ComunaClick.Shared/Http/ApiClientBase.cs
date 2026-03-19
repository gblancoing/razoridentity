using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ComunaClick.Shared.Auth.Interfaces;

namespace ComunaClick.Shared.Http;

public abstract class ApiClientBase
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly IAuthClient _authClient;

    protected ApiClientBase(HttpClient httpClient, ITokenStore tokenStore, IAuthClient authClient)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _authClient = authClient;
    }

    protected async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task<T?> PostAsync<T>(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task<T?> PatchAsync<T>(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body)
        };
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task PostNoContentAsync(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        await SendAsync(request, cancellationToken);
    }

    protected async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync(cancellationToken))
            {
                request = Clone(request);
                await AttachTokenAsync(request, cancellationToken);
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    protected async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync(request, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync(cancellationToken))
            {
                request = Clone(request);
                await AttachTokenAsync(request, cancellationToken);
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task AttachTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync(cancellationToken);
        if (tokens is null)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync(cancellationToken);
        if (tokens is null)
        {
            return false;
        }

        var refreshed = await _authClient.RefreshAsync(tokens.RefreshToken, cancellationToken: cancellationToken);
        await _tokenStore.SaveAsync(refreshed, cancellationToken);
        return true;
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
