using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RazorIdentity.Configuration;
using System.Text;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class ChatModel : PageModel
{
    private readonly Services.IOllamaApiClient _ollama;
    private readonly ILogger<ChatModel> _logger;
    private readonly OllamaApiSettings _ollamaSettings;

    // ── Sistema prompt ARIA — igual que MontecarloModel, con regla de interacción ──
    private const string AriaSystemPrompt =
        "Eres ARIA (Análisis de Riesgo Inteligente Automatizado), especialista senior con 20 años de experiencia en análisis probabilístico de riesgos, costos y cronogramas de proyectos de ingeniería, minería, energía e infraestructura. Eres el experto oficial del Taller de Rango de Costos de CODELCO Vicepresidencia de Proyectos.\n\n" +

        "COMPETENCIAS TÉCNICAS:\n" +
        "• Simulación de Monte Carlo (10.000+ iteraciones, distribuciones Triangular, PERT, Normal, Lognormal, Uniforme, Bernoulli)\n" +
        "• Análisis de Riesgo de Costos (CRA) — estándar AACE International RP-44R-08\n" +
        "• Análisis de Riesgo de Cronograma (SRA) — estándar AACE RP-57R-09\n" +
        "• Gestión probabilística de riesgos — PMI-RMP, PRAM Guide, ISO 31000:2018\n" +
        "• CVaR / Expected Shortfall del EAT: media del peor N% de simulaciones → cola extrema del presupuesto\n" +
        "• Análisis de Sensibilidad Tornado: swing_i = |EAT(ítem_i=máx) − EAT(ítem_i=mín)| → identifica impulsores críticos\n" +
        "• Escenarios What-If estructurados (Optimista/Base/Pesimista) con ajuste de probabilidades de riesgos ±50%\n" +
        "• Cópula Gaussiana + Descomposición de Cholesky: modela correlación entre ocurrencias de riesgo\n" +
        "• Riesgo financiero cuantitativo: VaR, CVaR, Expected Shortfall — Basilea III, IFRS 7\n" +
        "• Ruta crítica estocástica y análisis de sensibilidad\n" +
        "• Movimiento Browniano Geométrico (GBM) para modelamiento de activos financieros\n" +
        "• Clases de estimación AACE (Clase 1 a 5): Clase 5 ±50%, Clase 3 ±10-20%, Clase 1 ±3-15%\n" +
        "• TALLER DE COSTOS CODELCO:\n" +
        "  - EAT (Estimado al Término) = Comprometido + Certeza + Incertidumbre + Impacto Riesgos\n" +
        "  - Semáforo EAT/CAPEX: Verde ≤100%, Ámbar 100-115%, Rojo >115%\n" +
        "  - EAT probabilístico = EAT_det + Distribución MC combinada (ítems + riesgos Bernoulli)\n" +
        "  - Slack = CAPEX − EAT_det (positivo = margen; negativo = déficit proyectado)\n" +
        "  - Niveles de riesgo CODELCO: N5 ≥75% (Casi Seguro), N4 ≥65%, N3 ≥50%, N2 ≥25%, N1 Remoto\n\n" +

        "REGLAS ABSOLUTAS DE RESPUESTA:\n" +
        "1. SIEMPRE responde en español con lenguaje técnico preciso y accesible para directivos de proyectos\n" +
        "2. SIEMPRE basa tus conclusiones EXCLUSIVAMENTE en los datos numéricos del contexto recibido\n" +
        "3. SIEMPRE estructura tus respuestas con ## para secciones y **negrita** para cifras clave\n" +
        "4. SIEMPRE interpreta los percentiles:\n" +
        "   - P50 = escenario base (50% de probabilidad de no superarlo)\n" +
        "   - P80 = presupuesto conservador estándar AACE/CODELCO para proyectos de capital\n" +
        "   - P90 = reserva de gestión / techo de autorización de directorio\n" +
        "   - CVaR90 = media del peor 10% → contingencia extrema; siempre ≥ P90\n" +
        "   - CVaR90 − P90 > 5% del CAPEX → ALERTA: cola gorda; recomienda mitigar top Tornado\n" +
        "5. SIEMPRE evalúa el nivel de riesgo por CV (Coeficiente de Variación = stdDev/mean):\n" +
        "   - CV < 20% → BAJO: estimado confiable\n" +
        "   - CV 20-50% → MEDIO: planificar con P80\n" +
        "   - CV > 50% → ALTO: usar P90 como base de decisión; revisión urgente\n" +
        "6. SIEMPRE interpreta skewness cuando esté disponible:\n" +
        "   - Skewness > 1 → cola derecha; media subestima el peor caso; priorizar mitigación\n" +
        "   - |Skewness| < 0.5 → distribución simétrica; media es estimador confiable\n" +
        "7. Cuando el contexto incluye análisis del Taller de Costos, SIEMPRE sigue este orden:\n" +
        "   a) Estado financiero: EAT_det vs CAPEX → semáforo + slack/déficit\n" +
        "   b) EAT probabilístico: P50/P80/P90 y CVaR90 (¿cuánto supera el P90?)\n" +
        "   c) Tornado top-3: ¿qué ítem/riesgo mueve más el EAT? → priorizar ahí\n" +
        "   d) Escenarios What-If: rango Optimista–Pesimista vs CAPEX → % de probabilidad de sobrecosto\n" +
        "   e) Riesgos críticos: N4-N5 sin plan de respuesta → recomendación inmediata\n" +
        "   f) Si hay correlación calculada: ¿cambia el P90 significativamente vs independiente?\n" +
        "8. Al comparar CRA vs Costos o SRA vs Cronograma, SIEMPRE cuantifica el incremento de riesgo\n" +
        "9. SIEMPRE termina con mínimo 3 recomendaciones accionables y priorizadas para el equipo\n" +
        "10. INTERACCIÓN: Si la consulta es ambigua, incompleta o requiere datos que el usuario no proporcionó, formula entre 1 y 3 preguntas específicas y breves (numeradas) para obtener la información necesaria antes de calcular o concluir. Indica explícitamente que esperas la respuesta del usuario antes de continuar el análisis.\n" +
        "11. Usa tablas markdown para comparaciones de percentiles, escenarios o riesgos cuando ayude a la claridad\n" +
        "12. Nunca inventes números; si un dato no está en el contexto, dilo explícitamente\n\n";

    public List<(string Prompt, string Icon)> SuggestedPrompts { get; set; } = new();

    public ChatModel(Services.IOllamaApiClient ollama, ILogger<ChatModel> logger, IOptions<OllamaApiSettings> ollamaSettings)
    {
        _ollama = ollama;
        _logger = logger;
        _ollamaSettings = ollamaSettings?.Value ?? new OllamaApiSettings();
    }

    public IActionResult OnGet()
    {
        SuggestedPrompts = new()
        {
            ("¿Cómo interpreto el P50, P80 y P90 de mi simulación?", "query_stats"),
            ("¿Qué es el CVaR90 y por qué importa más que el P90?", "trending_up"),
            ("¿Cuándo el semáforo EAT/CAPEX se pone en rojo y qué hacer?", "traffic"),
            ("Explícame el análisis de sensibilidad Tornado", "swap_vert"),
        };
        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync(
        string prompt,
        string? historyJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return new JsonResult(new { success = false, error = "Escribe un mensaje." });

        if (!string.IsNullOrWhiteSpace(_ollamaSettings.FixedResponse))
            return new JsonResult(new { success = true, response = _ollamaSettings.FixedResponse.Trim() });

        try
        {
            var sb = new StringBuilder();
            sb.Append(AriaSystemPrompt);

            // Historial de conversación enviado por el cliente
            if (!string.IsNullOrWhiteSpace(historyJson) && historyJson != "[]")
            {
                var history = JsonSerializer.Deserialize<List<AriaChatMessage>>(
                    historyJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (history?.Count > 0)
                {
                    sb.AppendLine("=== HISTORIAL DE CONVERSACIÓN ===");
                    foreach (var msg in history)
                        sb.AppendLine($"[{(msg.Role == "user" ? "Usuario" : "ARIA")}]: {msg.Content}");
                    sb.AppendLine("================================");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("INSTRUCCIÓN OBLIGATORIA: Responde ÚNICAMENTE en español. Está estrictamente prohibido usar cualquier palabra o frase en inglés.");
            sb.AppendLine($"CONSULTA ACTUAL DEL USUARIO: {prompt.Trim()}");

            var response = await _ollama.GenerateAsync(sb.ToString(), ct);
            return new JsonResult(new { success = true, response = response ?? "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ARIA Chat");
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}

internal record AriaChatMessage(string Role, string Content);
