namespace RazorIdentity.Configuration;

public class MiroFishSettings
{
    public const string SectionName = "MiroFish";

    /// <summary>URL base del servidor MiroFish Python Flask (default: http://localhost:5001)</summary>
    public string BaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>Habilitar integración. Si el servidor no está disponible se muestra mensaje informativo.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Rondas de simulación (más rondas = más costo LLM y tiempo)</summary>
    public int MaxRounds { get; set; } = 12;

    /// <summary>Timeout en segundos para cada llamada HTTP individual a MiroFish</summary>
    public int HttpTimeoutSeconds { get; set; } = 60;

    /// <summary>Máximo tiempo total de espera del job en minutos antes de marcarlo como timeout</summary>
    public int JobTimeoutMinutes { get; set; } = 20;
}
