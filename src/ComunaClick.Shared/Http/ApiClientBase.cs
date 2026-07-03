using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ComunaClick.Shared.Auth.Interfaces;

namespace ComunaClick.Shared.Http;

public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ITokenRefresher _tokenRefresher;

    protected ApiClientBase(HttpClient httpClient, ITokenStore tokenStore, ITokenRefresher tokenRefresher)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _tokenRefresher = tokenRefresher;
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
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task<T?> PatchAsync<T>(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task<T?> SendPutAsync<T>(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync<T>(request, cancellationToken);
    }

    protected async Task PostNoContentAsync(string path, object body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        await SendAsync(request, cancellationToken);
    }

    protected Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        return SendAsync(request, cancellationToken);
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

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(errorBody)
                    ? $"HTTP {(int)response.StatusCode} ({response.StatusCode})"
                    : $"HTTP {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
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
        // El refresco depende del mecanismo de cada host (cookie en web, refresh token guardado en
        // mobile); no exigimos que el store tenga refresh token porque en web ya no lo guarda.
        var tokens = await _tokenStore.GetAsync(cancellationToken);
        var refreshed = await _tokenRefresher.RefreshAsync(tokens, cancellationToken);
        return refreshed is not null;
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
