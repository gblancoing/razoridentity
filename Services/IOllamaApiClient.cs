using RazorIdentity.Models.Ollama;

namespace RazorIdentity.Services;

/// <summary>
/// Cliente HTTP para consumir la API Ollama (Api_Ollama): generate, specialist, modelos y especialistas.
/// </summary>
public interface IOllamaApiClient
{
    Task<List<string>> GetModelsAsync(CancellationToken ct = default);
    Task<List<string>> GetSpecialistsAsync(CancellationToken ct = default);
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
    Task<string> SpecialistAsync(string specialist, string prompt, CancellationToken ct = default);
}
