using System.Net.Http.Json;
using System.Text.Json;
using RazorIdentity.Models.Ollama;

namespace RazorIdentity.Services;

public class OllamaApiClient : IOllamaApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string ModelsPath = "api/Ollama/models";
    private const string SpecialistsPath = "api/Ollama/specialists";
    private const string GeneratePath = "api/Ollama/generate";
    private const string SpecialistPath = "api/Ollama/specialist";

    public OllamaApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<string>> GetModelsAsync(CancellationToken ct = default)
    {
        var list = await GetStringListAsync(ModelsPath, ct);
        return list;
    }

    public async Task<List<string>> GetSpecialistsAsync(CancellationToken ct = default)
    {
        var list = await GetStringListAsync(SpecialistsPath, ct);
        return list;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new GenerateRequest { Prompt = prompt };
        var response = await _httpClient.PostAsJsonAsync(GeneratePath, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOptions, ct);
        return body?.GetContent() ?? "";
    }

    public async Task<string> SpecialistAsync(string specialist, string prompt, CancellationToken ct = default)
    {
        var request = new SpecialistRequest { Specialist = specialist, Prompt = prompt };
        var response = await _httpClient.PostAsJsonAsync(SpecialistPath, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SpecialistResponse>(JsonOptions, ct);
        return body?.GetContent() ?? "";
    }

    private async Task<List<string>> GetStringListAsync(string path, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(path, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new List<string>();
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        return list ?? new List<string>();
    }
}
