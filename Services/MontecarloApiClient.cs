using System.Net.Http.Json;
using System.Text.Json;

namespace RazorIdentity.Services;

public class MontecarloApiClient : IMontecarloApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions _pretty = new() { WriteIndented = true };

    public MontecarloApiClient(HttpClient http) => _http = http;

    public async Task<string> PostRawAsync<TRequest>(string ruta, TRequest body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(ruta, body, _opts, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        return PrettyPrint(json);
    }

    public async Task<string> GetRawAsync(string ruta, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(ruta, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        return PrettyPrint(json);
    }

    private static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, _pretty);
        }
        catch { return json; }
    }
}
