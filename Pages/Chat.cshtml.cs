using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorIdentity.Pages;

[Authorize]
public class ChatModel : PageModel
{
    private readonly Services.IOllamaApiClient _ollama;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(Services.IOllamaApiClient ollama, ILogger<ChatModel> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }

    public List<string> Specialists { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Specialists = await _ollama.GetSpecialistsAsync();
            if (Specialists.Count == 0)
                Specialists = new List<string> { "database", "frontend", "backend", "security" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron cargar especialistas de Ollama");
            Specialists = new List<string> { "database", "frontend", "backend", "security" };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync(string prompt, string? specialist, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return new JsonResult(new { success = false, error = "Escribe un mensaje." });

        try
        {
            string response;
            if (!string.IsNullOrWhiteSpace(specialist) && specialist != "general")
                response = await _ollama.SpecialistAsync(specialist.Trim(), prompt.Trim(), ct);
            else
                response = await _ollama.GenerateAsync(prompt.Trim(), ct);

            return new JsonResult(new { success = true, response = response ?? "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al llamar a Ollama");
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}
