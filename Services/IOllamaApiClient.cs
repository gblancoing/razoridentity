using RazorIdentity.Models.Ollama;

namespace RazorIdentity.Services;

/// <summary>
/// Cliente HTTP para consumir la API Ollama (Api_Ollama): generate, specialist, analyze, modelos y especialistas.
/// </summary>
public interface IOllamaApiClient
{
    Task<List<string>> GetModelsAsync(CancellationToken ct = default);
    Task<List<string>> GetSpecialistsAsync(CancellationToken ct = default);
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
    Task<string> SpecialistAsync(string specialist, string prompt, int? month = null, int? year = null, int? proyectoId = null, string? model = null, CancellationToken ct = default);
    /// <summary>Análisis con datos del proyecto (API_Ritweb). POST api/Ollama/analyze.</summary>
    Task<string> AnalyzeAsync(string prompt, int? proyectoId = null, int? month = null, int? year = null, string? model = null, CancellationToken ct = default);
}
