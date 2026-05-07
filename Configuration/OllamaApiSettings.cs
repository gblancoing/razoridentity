namespace RazorIdentity.Configuration;

/// <summary>
/// Configuración de la URL base de la API Ollama (Api_Ollama).
/// </summary>
public class OllamaApiSettings
{
    public const string SectionName = "OllamaApi";
    public string BaseUrl { get; set; } = "https://localhost:7006";
    /// <summary>Tiempo máximo de espera para generar respuestas (por defecto 300 s; el valor por defecto de HttpClient es 100 s).</summary>
    public int HttpTimeoutSeconds { get; set; } = 300;
    /// <summary>Si está definido, el Chat responde siempre con este texto y no llama a la API.</summary>
    public string? FixedResponse { get; set; }
}
