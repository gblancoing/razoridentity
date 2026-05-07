using Microsoft.Extensions.Options;
using RazorIdentity.Configuration;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RazorIdentity.Services;

// ─── DTOs de entrada ─────────────────────────────────────────────────────────

public record MiroFishRiesgoDto(
    string Codigo, string Descripcion,
    double Probabilidad, double Min, double Moda, double Max,
    double ValorEsperado, bool EsAmenaza,
    string NivelLabel, int NivelInt,
    string PlanRespuesta);

public record MiroFishIniciarDto(
    Guid ProyectoId, string ProyectoNombre, string ProyectoCodigo, string Organizacion,
    double EatDeterministico, double PctIncertidumbre,
    double? EatP50, double? EatP80, double? SpreadP10P90,
    List<MiroFishRiesgoDto> Riesgos,
    string ContextoUsuario);

// ─── Estado del Job ───────────────────────────────────────────────────────────

public enum MiroFishJobStatus { Pendiente, Procesando, Completado, Error, Timeout }

public class MiroFishJob
{
    public string JobId      { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public Guid ProyectoId   { get; set; }
    public MiroFishJobStatus Status { get; set; } = MiroFishJobStatus.Pendiente;
    public string StepLabel  { get; set; } = "Iniciando...";
    public int    StepNum    { get; set; } = 0;
    public int    TotalSteps { get; set; } = 6;
    public double Progress   { get; set; } = 0;

    // IDs internos MiroFish
    public string? ProjectId    { get; set; }
    public string? GraphId      { get; set; }
    public string? SimulationId { get; set; }
    public string? ReportId     { get; set; }

    // Resultados
    public string?  ReportMarkdown   { get; set; }
    public string?  EscenarioBase    { get; set; }
    public string?  EscenarioPesimista { get; set; }
    public string?  EscenarioOptimista { get; set; }
    public List<MiroFishRiesgoAjuste> RiesgosAjustados { get; set; } = new();
    public bool     HayDivergencia   { get; set; }
    public string?  ErrorMessage     { get; set; }
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt     { get; set; }
}

public class MiroFishRiesgoAjuste
{
    public string Codigo         { get; set; } = "";
    public string Descripcion    { get; set; } = "";
    public double ProbOriginal   { get; set; }
    public double? ProbAjustada  { get; set; }
    public double Variacion      => ProbAjustada.HasValue ? ProbAjustada.Value - ProbOriginal : 0;
    public bool   EsDivergente   => Math.Abs(Variacion) > 15;
    public string NarrativaIA    { get; set; } = "";
}

// ─── Servicio ─────────────────────────────────────────────────────────────────

public class MiroFishService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly MiroFishSettings _cfg;
    private readonly ILogger<MiroFishService> _log;
    private readonly ConcurrentDictionary<string, MiroFishJob> _jobs = new();

    private static readonly JsonSerializerOptions _jOpts = new() { PropertyNameCaseInsensitive = true };

    private HttpClient Http => _httpFactory.CreateClient("MiroFish");

    public MiroFishService(IHttpClientFactory httpFactory, IOptions<MiroFishSettings> cfg, ILogger<MiroFishService> log)
    {
        _httpFactory = httpFactory;
        _cfg  = cfg.Value;
        _log  = log;
    }

    // ── API pública ──────────────────────────────────────────────────────────

    public async Task<bool> IsAvailableAsync()
    {
        if (!_cfg.Enabled) return false;
        try
        {
            var resp = await Http.GetAsync("/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>Inicia el análisis en background y retorna el jobId inmediatamente.</summary>
    public string IniciarAnalisis(MiroFishIniciarDto req)
    {
        var job = new MiroFishJob { ProyectoId = req.ProyectoId };
        _jobs[job.JobId] = job;
        _ = Task.Run(() => EjecutarPipelineAsync(job, req));
        return job.JobId;
    }

    public MiroFishJob? GetJob(string jobId) =>
        _jobs.TryGetValue(jobId, out var j) ? j : null;

    // ── Pipeline principal (background) ──────────────────────────────────────

    private async Task EjecutarPipelineAsync(MiroFishJob job, MiroFishIniciarDto req)
    {
        try
        {
            job.Status = MiroFishJobStatus.Procesando;

            // Paso 1: Generar texto semilla
            Avanzar(job, 1, "Preparando material semilla...", 5);
            var seedText = BuildSeedText(req);

            // Paso 2: Subir documento y crear proyecto
            Avanzar(job, 2, "Construyendo grafo de conocimiento...", 15);
            var (projectId, graphId) = await CrearProyectoYGrafoAsync(req, seedText);
            job.ProjectId = projectId;
            job.GraphId   = graphId;

            // Paso 3: Esperar a que el grafo esté listo
            Avanzar(job, 3, "Procesando grafo (puede tardar 1-2 min)...", 30);
            await EsperarTareaAsync($"/api/graph/project/{projectId}", "status", "completed");

            // Paso 4: Crear simulación y ejecutar
            Avanzar(job, 4, "Configurando agentes de enjambre...", 45);
            var simId = await CrearYEjecutarSimulacionAsync(projectId, graphId);
            job.SimulationId = simId;

            // Paso 5: Esperar fin de simulación
            Avanzar(job, 5, $"Simulando trayectorias ({_cfg.MaxRounds} rondas)...", 60);
            await EsperarSimulacionAsync(simId);

            // Paso 6: Generar reporte
            Avanzar(job, 6, "Generando reporte narrativo...", 80);
            var reportId = await GenerarReporteAsync(simId);
            job.ReportId = reportId;
            await EsperarTareaAsync($"/api/report/{reportId}/progress", "status", "completed");

            // Paso 7: Obtener reporte + consultar escenarios
            Avanzar(job, 6, "Extrayendo insights del análisis...", 90);
            job.ReportMarkdown = await ObtenerMarkdownAsync(reportId);
            await ConsultarEscenariosAsync(job, simId, req.Riesgos);

            job.Status      = MiroFishJobStatus.Completado;
            job.Progress    = 100;
            job.StepLabel   = "Análisis completado";
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error en pipeline MiroFish job {JobId}", job.JobId);
            job.Status       = MiroFishJobStatus.Error;
            job.ErrorMessage = ex.Message;
            job.StepLabel    = "Error en el análisis";
        }
    }

    // ── Paso 2: Crear proyecto y grafo ───────────────────────────────────────

    private async Task<(string projectId, string graphId)> CrearProyectoYGrafoAsync(
        MiroFishIniciarDto req, string seedText)
    {
        // Subir texto como archivo .txt vía multipart
        using var form = new MultipartFormDataContent();
        var textBytes = Encoding.UTF8.GetBytes(seedText);
        var fileContent = new ByteArrayContent(textBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "files", $"riesgos_{req.ProyectoCodigo}.txt");
        form.Add(new StringContent(
            $"Predecir trayectorias y escenarios futuros del proyecto {req.ProyectoNombre} ({req.ProyectoCodigo}) " +
            $"considerando sus riesgos identificados, análisis Monte Carlo y el contexto del mundo real proporcionado. " +
            $"Generar escenarios base, conservador y pesimista con ajuste cualitativo de probabilidades por riesgo."),
            "simulation_requirement");
        form.Add(new StringContent($"TC_{req.ProyectoCodigo}"), "project_name");
        form.Add(new StringContent(req.ContextoUsuario), "additional_context");

        var resp = await PostAsync("/api/graph/ontology/generate", form);
        var data = resp.GetProperty("data");
        var projectId = data.GetProperty("project_id").GetString()!;

        // Construir el grafo
        var buildResp = await PostJsonAsync("/api/graph/build", new
        {
            project_id    = projectId,
            graph_name    = $"TC_{req.ProyectoCodigo}",
            chunk_size    = 400,
            chunk_overlap = 40,
            force         = false
        });
        var graphId = buildResp.TryGetProperty("data", out var bd)
                      && bd.TryGetProperty("graph_id", out var gid)
            ? gid.GetString() ?? projectId
            : projectId;

        return (projectId, graphId);
    }

    // ── Paso 4: Crear y ejecutar simulación ──────────────────────────────────

    private async Task<string> CrearYEjecutarSimulacionAsync(string projectId, string graphId)
    {
        var createResp = await PostJsonAsync("/api/simulation/create", new
        {
            project_id     = projectId,
            graph_id       = graphId,
            enable_twitter = true,
            enable_reddit  = false
        });
        var simId = createResp.GetProperty("data").GetProperty("simulation_id").GetString()!;

        // Preparar perfiles de agentes
        await PostJsonAsync("/api/simulation/prepare", new
        {
            simulation_id         = simId,
            use_llm_for_profiles  = true,
            parallel_profile_count= 5,
            force_regenerate      = false
        });

        // Esperar preparación
        await EsperarSimulacionPreparadaAsync(simId);

        // Iniciar simulación
        await PostJsonAsync("/api/simulation/start", new
        {
            simulation_id              = simId,
            platform                   = "twitter",
            max_rounds                 = _cfg.MaxRounds,
            enable_graph_memory_update = true,
            force                      = false
        });

        return simId;
    }

    // ── Paso 5: Esperar fin de simulación ────────────────────────────────────

    private async Task EsperarSimulacionAsync(string simId)
    {
        var deadline = DateTime.UtcNow.AddMinutes(_cfg.JobTimeoutMinutes);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(8_000);
            try
            {
                var resp = await Http.GetAsync($"/api/simulation/{simId}/run-status");
                if (!resp.IsSuccessStatusCode) continue;
                var json = JsonSerializer.Deserialize<JsonElement>(
                    await resp.Content.ReadAsStringAsync(), _jOpts);
                if (!json.TryGetProperty("data", out var d)) continue;
                if (d.TryGetProperty("status", out var st))
                {
                    var s = st.GetString() ?? "";
                    if (s is "completed" or "stopped" or "error") return;
                }
                // Algunos builds retornan progress como porcentaje
                if (d.TryGetProperty("progress", out var prg) && prg.GetDouble() >= 1.0) return;
            }
            catch { /* continuar polling */ }
        }
    }

    // ── Paso 5b: Esperar preparación de agentes ──────────────────────────────

    private async Task EsperarSimulacionPreparadaAsync(string simId)
    {
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(5_000);
            try
            {
                var resp = await PostJsonAsync("/api/simulation/prepare/status",
                    new { simulation_id = simId });
                if (resp.TryGetProperty("data", out var d) &&
                    d.TryGetProperty("status", out var st))
                {
                    var s = st.GetString() ?? "";
                    if (s is "completed" or "ready") return;
                }
            }
            catch { /* continuar */ }
        }
    }

    // ── Paso 6: Generar reporte ──────────────────────────────────────────────

    private async Task<string> GenerarReporteAsync(string simId)
    {
        var resp = await PostJsonAsync("/api/report/generate",
            new { simulation_id = simId, force_regenerate = false });
        return resp.GetProperty("data").GetProperty("report_id").GetString()!;
    }

    private async Task<string> ObtenerMarkdownAsync(string reportId)
    {
        var resp = await Http.GetAsync($"/api/report/{reportId}");
        resp.EnsureSuccessStatusCode();
        var json = JsonSerializer.Deserialize<JsonElement>(
            await resp.Content.ReadAsStringAsync(), _jOpts);
        return json.GetProperty("data")
                   .TryGetProperty("markdown_content", out var mc)
            ? mc.GetString() ?? "(sin contenido)"
            : "(sin contenido)";
    }

    // ── Paso 7: Consultar escenarios y ajuste de riesgos ────────────────────

    private async Task ConsultarEscenariosAsync(MiroFishJob job, string simId,
        List<MiroFishRiesgoDto> riesgos)
    {
        // Escenario base
        job.EscenarioBase = await ChatReporteAsync(simId,
            "Describe en 3-5 oraciones el escenario base más probable para este proyecto " +
            "considerando el contexto real provisto y los riesgos identificados. Sé concreto y cuantitativo cuando sea posible.");

        // Escenario pesimista/conservador
        job.EscenarioPesimista = await ChatReporteAsync(simId,
            "Describe en 3-5 oraciones el escenario pesimista/conservador donde los principales riesgos se materializan. " +
            "¿Cuánto podría desviarse el costo final del EAT determinístico?");

        // Escenario optimista
        job.EscenarioOptimista = await ChatReporteAsync(simId,
            "Describe en 3-5 oraciones el escenario optimista donde las oportunidades se capturan y los riesgos se mitigan efectivamente.");

        // Ajuste cualitativo por riesgo
        foreach (var r in riesgos.Take(8))
        {
            var pregunta =
                $"Para el riesgo '{r.Codigo} — {r.Descripcion}' (probabilidad actual: {r.Probabilidad}%, " +
                $"tipo: {(r.EsAmenaza ? "amenaza" : "oportunidad")}, nivel CODELCO: {r.NivelInt}):\n" +
                $"1. ¿La probabilidad debería ajustarse considerando el contexto actual? Si es así, estima el nuevo valor (0-100%).\n" +
                $"2. En una oración, explica el razonamiento principal.\n" +
                $"Responde en formato: PROB_AJUSTADA: XX% | RAZON: [texto]";

            var respuesta = await ChatReporteAsync(simId, pregunta);
            var ajuste = ParsearAjuste(r, respuesta);
            job.RiesgosAjustados.Add(ajuste);
        }

        // Detectar divergencia significativa
        job.HayDivergencia = job.RiesgosAjustados.Any(a => a.EsDivergente);
    }

    private async Task<string> ChatReporteAsync(string simId, string mensaje)
    {
        try
        {
            var resp = await PostJsonAsync("/api/report/chat", new
            {
                simulation_id = simId,
                message       = mensaje,
                chat_history  = Array.Empty<object>()
            });
            return resp.TryGetProperty("data", out var d) &&
                   d.TryGetProperty("response", out var r)
                ? r.GetString() ?? ""
                : "";
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error en chat reporte MiroFish");
            return "";
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MiroFishRiesgoAjuste ParsearAjuste(MiroFishRiesgoDto r, string respuesta)
    {
        var ajuste = new MiroFishRiesgoAjuste
        {
            Codigo      = r.Codigo,
            Descripcion = r.Descripcion,
            ProbOriginal = r.Probabilidad,
            NarrativaIA  = respuesta
        };

        // Intentar extraer "PROB_AJUSTADA: XX%"
        var matchProb = System.Text.RegularExpressions.Regex.Match(
            respuesta, @"PROB_AJUSTADA:\s*(\d+(?:\.\d+)?)\s*%",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchProb.Success && double.TryParse(matchProb.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var pAjust))
        {
            ajuste.ProbAjustada = Math.Clamp(pAjust, 0, 100);
        }

        // Extraer razón principal
        var matchRazon = System.Text.RegularExpressions.Regex.Match(
            respuesta, @"RAZON:\s*(.+?)(?:\n|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (matchRazon.Success)
            ajuste.NarrativaIA = matchRazon.Groups[1].Value.Trim();

        return ajuste;
    }

    private async Task EsperarTareaAsync(string pollUrl, string statusField, string targetStatus)
    {
        var deadline = DateTime.UtcNow.AddMinutes(_cfg.JobTimeoutMinutes);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(6_000);
            try
            {
                var resp = await Http.GetAsync(pollUrl);
                if (!resp.IsSuccessStatusCode) continue;
                var json = JsonSerializer.Deserialize<JsonElement>(
                    await resp.Content.ReadAsStringAsync(), _jOpts);
                string status = "";
                if (json.TryGetProperty("data", out var d) && d.TryGetProperty(statusField, out var st))
                    status = st.GetString() ?? "";
                else if (json.TryGetProperty(statusField, out var st2))
                    status = st2.GetString() ?? "";
                if (string.Equals(status, targetStatus, StringComparison.OrdinalIgnoreCase)) return;
            }
            catch { /* continuar polling */ }
        }
    }

    private async Task<JsonElement> PostAsync(string url, HttpContent content)
    {
        var resp = await Http.PostAsync(url, content);
        resp.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(
            await resp.Content.ReadAsStringAsync(), _jOpts);
    }

    private async Task<JsonElement> PostJsonAsync(string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await PostAsync(url, content);
    }

    private void Avanzar(MiroFishJob job, int step, string label, double pct)
    {
        job.StepNum   = step;
        job.StepLabel = label;
        job.Progress  = pct;
    }

    // ── Generador de texto semilla ───────────────────────────────────────────

    public static string BuildSeedText(MiroFishIniciarDto req)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Análisis de Riesgos de Proyecto CODELCO — {req.ProyectoCodigo}: {req.ProyectoNombre}");
        sb.AppendLine();
        sb.AppendLine("## Información General del Proyecto");
        sb.AppendLine($"- **Empresa**: CODELCO — Vicepresidencia de Proyectos");
        if (!string.IsNullOrWhiteSpace(req.Organizacion))
            sb.AppendLine($"- **Organización**: {req.Organizacion}");
        sb.AppendLine($"- **EAT Determinístico**: ${req.EatDeterministico:N0} USD");
        sb.AppendLine($"- **Porcentaje de incertidumbre**: {req.PctIncertidumbre:N1}%");
        if (req.EatP50.HasValue) sb.AppendLine($"- **EAT Monte Carlo P50**: ${req.EatP50.Value:N0} USD");
        if (req.EatP80.HasValue) sb.AppendLine($"- **EAT Monte Carlo P80**: ${req.EatP80.Value:N0} USD");
        if (req.SpreadP10P90.HasValue) sb.AppendLine($"- **Spread P10-P90 (incertidumbre)**: ${req.SpreadP10P90.Value:N0} USD");
        sb.AppendLine();

        var amenazas      = req.Riesgos.Where(r =>  r.EsAmenaza).ToList();
        var oportunidades = req.Riesgos.Where(r => !r.EsAmenaza).ToList();

        if (amenazas.Any())
        {
            sb.AppendLine($"## Amenazas Identificadas ({amenazas.Count})");
            sb.AppendLine();
            foreach (var r in amenazas)
                AppendRiesgo(sb, r);
        }

        if (oportunidades.Any())
        {
            sb.AppendLine($"## Oportunidades Identificadas ({oportunidades.Count})");
            sb.AppendLine();
            foreach (var r in oportunidades)
                AppendRiesgo(sb, r);
        }

        sb.AppendLine("## Resumen de Valor Esperado");
        sb.AppendLine($"- **VE Total Amenazas**: ${amenazas.Sum(r => r.ValorEsperado):N0} USD");
        sb.AppendLine($"- **VE Total Oportunidades**: ${Math.Abs(oportunidades.Sum(r => r.ValorEsperado)):N0} USD");
        sb.AppendLine($"- **VE Neto**: ${req.Riesgos.Sum(r => r.ValorEsperado):N0} USD");
        sb.AppendLine();

        sb.AppendLine("## Contexto del Mundo Real (Aportado por el Equipo del Proyecto)");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(req.ContextoUsuario)
            ? "Sin contexto adicional especificado."
            : req.ContextoUsuario.Trim());
        sb.AppendLine();

        sb.AppendLine("## Objetivo del Análisis");
        sb.AppendLine("Analizar las trayectorias probables de este proyecto CODELCO y sus riesgos usando " +
                      "simulación de enjambre multiagente. Predecir escenarios base, conservador y pesimista " +
                      "para los próximos 6-12 meses. Identificar si el contexto del mundo real justifica " +
                      "ajustes a las probabilidades e impactos estimados en el análisis Monte Carlo.");

        return sb.ToString();
    }

    private static void AppendRiesgo(StringBuilder sb, MiroFishRiesgoDto r)
    {
        sb.AppendLine($"### {(r.EsAmenaza ? "AMENAZA" : "OPORTUNIDAD")} {r.Codigo}: {r.Descripcion}");
        sb.AppendLine($"- **Probabilidad**: {r.Probabilidad:N0}% (Nivel CODELCO {r.NivelInt}: {r.NivelLabel})");
        sb.AppendLine($"- **Impacto mínimo**: ${r.Min:N0} USD");
        sb.AppendLine($"- **Impacto probable (moda)**: ${r.Moda:N0} USD");
        sb.AppendLine($"- **Impacto máximo**: ${r.Max:N0} USD");
        sb.AppendLine($"- **Valor Esperado (prob × moda)**: ${r.ValorEsperado:N0} USD");
        if (!string.IsNullOrWhiteSpace(r.PlanRespuesta))
            sb.AppendLine($"- **Plan de respuesta actual**: {r.PlanRespuesta}");
        sb.AppendLine();
    }
}
