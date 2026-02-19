using System.Net.Http.Json;
using System.Text.Json;

namespace RazorIdentity.Services;

public class RitApiClient : IRitApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RitApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(ruta, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
        return list ?? new List<T>();
    }

    public async Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(ruta, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
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

    public async Task<T?> PutAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(ruta, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task DeleteAsync(string ruta, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync(ruta, ct);
        response.EnsureSuccessStatusCode();
    }
}
