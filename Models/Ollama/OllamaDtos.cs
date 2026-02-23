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
}

/// <summary>Response de specialist (misma forma que generate)</summary>
public class SpecialistResponse
{
    public string? Response { get; set; }
    public string? Message { get; set; }
    public string? Text { get; set; }

    public string GetContent() => Response ?? Message ?? Text ?? "";
}
