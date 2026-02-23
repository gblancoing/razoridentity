namespace RazorIdentity.Configuration;

/// <summary>
/// Configuración de la URL base de la API Ollama (Api_Ollama).
/// </summary>
public class OllamaApiSettings
{
    public const string SectionName = "OllamaApi";
    public string BaseUrl { get; set; } = "https://localhost:7006";
}
