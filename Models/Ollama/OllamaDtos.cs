namespace RazorIdentity.Models.Ollama;

/// <summary>Request para POST /api/Ollama/generate</summary>
public class GenerateRequest
{
    public string Prompt { get; set; } = "";
}

/// <summary>Response de generate (campo típico: response o message)</summary>
public class GenerateResponse
{
    public string? Response { get; set; }
    public string? Message { get; set; }
    public string? Text { get; set; }

    public string GetContent() => Response ?? Message ?? Text ?? "";
}

/// <summary>Request para POST /api/Ollama/specialist</summary>
public class SpecialistRequest
{
    public string Specialist { get; set; } = "";
    public string Prompt { get; set; } = "";
    /// <summary>Opcional; usado por especialista "analytics" (filtro por mes).</summary>
    public int? Month { get; set; }
    /// <summary>Opcional; usado por especialista "analytics" (filtro por año).</summary>
    public int? Year { get; set; }
    /// <summary>Opcional; usado por especialista "analytics" (filtro por proyecto).</summary>
    public int? ProyectoId { get; set; }
    public string? Model { get; set; }
}

/// <summary>Request para POST /api/Ollama/analyze (análisis con datos del proyecto).</summary>
public class AnalyzeRequest
{
    public string Prompt { get; set; } = "";
    public int? ProyectoId { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
    public string? Model { get; set; }
}

/// <summary>Response de specialist (misma forma que generate)</summary>
public class SpecialistResponse
{
    public string? Response { get; set; }
    public string? Message { get; set; }
    public string? Text { get; set; }

    public string GetContent() => Response ?? Message ?? Text ?? "";
}
