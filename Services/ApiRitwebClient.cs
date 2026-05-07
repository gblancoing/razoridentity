using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RazorIdentity.Configuration;

namespace RazorIdentity.Services;

public class ApiRitwebClient : IApiRitwebClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiRitwebClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(ruta, ct);
        if (!response.IsSuccessStatusCode)
        {
            var bodyError = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{(int)response.StatusCode} ({response.ReasonPhrase}): {bodyError}");
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
        return list ?? new List<T>();
    }

    public async Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(ruta, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        if (!response.IsSuccessStatusCode)
        {
            var bodyError = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{(int)response.StatusCode} ({response.ReasonPhrase}): {bodyError}");
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task<T?> PostAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(ruta, body, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var bodyError = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{(int)response.StatusCode} ({response.StatusCode}): {bodyError}");
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task<T?> PostMultipartAsync<T>(string ruta, MultipartFormDataContent content, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync(ruta, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var bodyError = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{(int)response.StatusCode} ({response.StatusCode}): {bodyError}");
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task<T?> PutAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(ruta, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task PatchAsync(string ruta, object body, CancellationToken ct = default)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync(ruta, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync(ruta, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]?> GetByteArrayAsync(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(ruta, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
