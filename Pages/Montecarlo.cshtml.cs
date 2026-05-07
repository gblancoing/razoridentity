using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Montecarlo;
using RazorIdentity.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class MontecarloModel : PageModel
{
    private readonly IMontecarloApiClient _api;
    private readonly IOllamaApiClient _ollama;
    private readonly ILogger<MontecarloModel> _logger;
    private readonly ApplicationDbContext _db;
    private readonly MonteCarloService _mc;

    // ── Sistema prompt del experto Monte Carlo + Taller de Costos CODELCO ──────
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
        "7. Cuando el contexto incluye TALLER_DE_COSTOS, SIEMPRE sigue este orden de análisis:\n" +
        "   a) Estado financiero: EAT_det vs CAPEX → semáforo + slack/déficit\n" +
        "   b) EAT probabilístico: P50/P80/P90 y CVaR90 (¿cuánto supera el P90?)\n" +
        "   c) Tornado top-3: ¿qué ítem/riesgo mueve más el EAT? → priorizar ahí\n" +
        "   d) Escenarios What-If: rango Optimista–Pesimista vs CAPEX → % de probabilidad de sobrecosto\n" +
        "   e) Riesgos críticos: Nivel 4-5 sin plan de respuesta → recomendación inmediata\n" +
        "   f) Si hay correlación calculada: ¿cambia el P90 significativamente vs independiente?\n" +
        "8. Al comparar CRA vs Costos o SRA vs Cronograma, SIEMPRE cuantifica el incremento de riesgo\n" +
        "9. SIEMPRE termina con mínimo 3 recomendaciones accionables y priorizadas para el equipo\n" +
        "10. Si no hay datos suficientes, orienta al usuario sobre qué análisis ejecutar primero y por qué\n" +
        "11. Usa tablas markdown para comparaciones de percentiles, escenarios o riesgos cuando ayude\n" +
        "12. Nunca inventes números; si un dato no está en el contexto, dilo explícitamente\n\n";

    public MontecarloModel(IMontecarloApiClient api, IOllamaApiClient ollama,
        ILogger<MontecarloModel> logger, ApplicationDbContext db, MonteCarloService mc)
    {
        _api    = api;
        _ollama = ollama;
        _logger = logger;
        _db     = db;
        _mc     = mc;
    }

    // ── Riesgos en Costo ─────────────────────────────────────────────────────
    [BindProperty] public List<RiskItem> Risks { get; set; } = new() { new() };
    [BindProperty] public int RiskSimulations { get; set; } = 10000;

    // ── Costos ───────────────────────────────────────────────────────────────
    [BindProperty] public List<CostComponent> CostComponents { get; set; } = new() { new() };
    [BindProperty] public int CostSimulations { get; set; } = 10000;

    // ── Cronograma ───────────────────────────────────────────────────────────
    [BindProperty] public List<ScheduleTaskInput> ScheduleTasks { get; set; } = new() { new() };
    [BindProperty] public int ScheduleSimulations { get; set; } = 10000;
    [BindProperty] public double? ScheduleGlobalPlannedDays { get; set; }

    // ── Riesgos del Programa ──────────────────────────────────────────────────
    [BindProperty] public List<ScheduleRiskItem> ScheduleRisks { get; set; } = new() { new() };
    [BindProperty] public int ScheduleRiskSimulations { get; set; } = 10000;

    // ── VaR ──────────────────────────────────────────────────────────────────
    [BindProperty] public List<AssetItem> Assets { get; set; } = new() { new() };
    [BindProperty] public int VarHorizonDays { get; set; } = 252;
    [BindProperty] public double VarConfidenceLevel { get; set; } = 0.95;
    [BindProperty] public int VarSimulations { get; set; } = 10000;

    // ── Estado de página ─────────────────────────────────────────────────────
    public string ActiveTab { get; set; } = "tc-resumen";
    public string? ResultJson { get; set; }
    public string? ApiError { get; set; }

    public void OnGet(string tab = "tc-resumen")
    {
        ActiveTab = tab;
        ResultJson = TempData["ResultJson"] as string;
        ApiError = TempData["ApiError"] as string;
    }

    // ── Handlers POST ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostRisksAsync()
    {
        var filteredRisks = Risks
            .Where(r => !string.IsNullOrWhiteSpace(r.Description) || !string.IsNullOrWhiteSpace(r.Cause) || !string.IsNullOrWhiteSpace(r.Code))
            .ToList();
        filteredRisks.ForEach(r => r.Probability = r.Probability / 100.0);
        var request = new RiskAnalysisRequest
        {
            Risks = filteredRisks,
            Simulations = RiskSimulations
        };
        if (!request.Risks.Any())
        {
            TempData["ApiError"] = "Agregue al menos un riesgo con descripción.";
            return RedirectToPage(new { tab = "risks" });
        }
        return await RunAndRedirect("api/montecarlo/risks", request, "risks");
    }

    public async Task<IActionResult> OnPostRisksAjaxAsync()
    {
        try
        {
            using var reader  = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = JsonSerializer.Deserialize<RisksAjaxRequest>(rawBody, opts);
            if (request == null)
                return new JsonResult(new { error = "Cuerpo de solicitud inválido." });

            var risks = (request.Risks ?? new List<RiskItem>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Description)
                         || !string.IsNullOrWhiteSpace(r.Cause)
                         || !string.IsNullOrWhiteSpace(r.Code)
                         || r.MinImpact != 0 || r.MostLikelyImpact != 0 || r.MaxImpact != 0)
                .ToList();
            if (!risks.Any())
                return new JsonResult(new { error = "Agregue al menos un riesgo con descripción, causa, código o impactos mayores a 0." });

            // Validar rango de probabilidad [0, 100] antes de convertir
            var invalidRisk = risks.FirstOrDefault(r => r.Probability < 0 || r.Probability > 100);
            if (invalidRisk != null)
                return new JsonResult(new { error = $"Probabilidad fuera de rango [0, 100]: {invalidRisk.Probability}% en riesgo '{invalidRisk.Description ?? invalidRisk.Code}'." });

            // Probabilidad viene en % (ej. 90) → convertir a 0-1 (0.90)
            risks.ForEach(r => r.Probability /= 100.0);

            int sims   = request.Simulations > 0 ? request.Simulations : 10_000;
            var result = await Task.Run(() => _mc.RunRiskAnalysis(risks, sims));

            var serOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json    = JsonSerializer.Serialize(result, serOpts);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular Riesgos AJAX");
            return new JsonResult(new { error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostCostsAsync()
    {
        var components = CostComponents.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        if (!components.Any())
        {
            TempData["ApiError"] = "Agregue al menos un componente de costo.";
            return RedirectToPage(new { tab = "costs" });
        }
        var request = new
        {
            components = components.Select(BuildCostComponentApi).ToList(),
            simulations = CostSimulations
        };
        return await RunAndRedirect("api/montecarlo/costs", request, "costs");
    }

    public async Task<IActionResult> OnPostScheduleAsync()
    {
        var tasks = ScheduleTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => new
            {
                name = t.Name,
                minDays = t.MinDays,
                mostLikelyDays = t.MostLikelyDays,
                maxDays = t.MaxDays,
                plannedDays = t.PlannedDays,
                dependencies = string.IsNullOrWhiteSpace(t.DependenciesText)
                    ? new List<string>()
                    : t.DependenciesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                distribution = t.Distribution
            }).ToList();

        if (!tasks.Any())
        {
            TempData["ApiError"] = "Agregue al menos una tarea.";
            return RedirectToPage(new { tab = "schedule" });
        }
        var request = new
        {
            tasks,
            simulations = ScheduleSimulations,
            plannedDays = ScheduleGlobalPlannedDays
        };
        return await RunAndRedirect("api/montecarlo/schedule", request, "schedule");
    }

    public async Task<IActionResult> OnPostScheduleRisksAsync()
    {
        var request = new ScheduleRiskAnalysisRequest
        {
            Risks = ScheduleRisks.Where(r => !string.IsNullOrWhiteSpace(r.Cause) || !string.IsNullOrWhiteSpace(r.RiskEvent)).ToList(),
            Simulations = ScheduleRiskSimulations
        };
        if (!request.Risks.Any())
        {
            TempData["ApiError"] = "Agregue al menos un riesgo de programa.";
            return RedirectToPage(new { tab = "sched-risks" });
        }
        return await RunAndRedirect("api/montecarlo/schedule-risks", request, "sched-risks");
    }

    // ── AJAX: SRA y CRA ───────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostSraAjaxAsync([FromBody] SraRequest request)
    {
        try
        {
            var apiRequest = new
            {
                schedule = new
                {
                    tasks = request.Tasks.Select(t => new
                    {
                        name = t.Name,
                        minDays = t.MinDays,
                        mostLikelyDays = t.MostLikelyDays,
                        maxDays = t.MaxDays,
                        plannedDays = t.PlannedDays,
                        dependencies = t.Dependencies,
                        distribution = t.Distribution
                    }).ToList(),
                    simulations = request.Simulations,
                    plannedDays = request.GlobalPlannedDays
                },
                scheduleRisks = new
                {
                    risks = request.Risks,
                    simulations = request.Simulations
                },
                simulations = request.Simulations
            };
            var json = await _api.PostRawAsync("api/montecarlo/sra", apiRequest);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular SRA");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    public async Task<IActionResult> OnPostCraAjaxAsync([FromBody] CraRequest request)
    {
        try
        {
            var apiRequest = new
            {
                costs = new
                {
                    components = request.Components.Select(BuildCostComponentApi).ToList(),
                    simulations = request.Simulations
                },
                risks = new
                {
                    risks = request.Risks,
                    simulations = request.Simulations
                },
                simulations = request.Simulations
            };
            var json = await _api.PostRawAsync("api/montecarlo/cra", apiRequest);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular CRA");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    public async Task<IActionResult> OnPostVarAsync()
    {
        var request = new VaRRequest
        {
            Assets = Assets.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList(),
            HorizonDays = VarHorizonDays,
            ConfidenceLevel = VarConfidenceLevel,
            Simulations = VarSimulations
        };
        if (!request.Assets.Any())
        {
            TempData["ApiError"] = "Agregue al menos un activo.";
            return RedirectToPage(new { tab = "var" });
        }
        return await RunAndRedirect("api/montecarlo/var", request, "var");
    }

    // ── Handlers AJAX proyectos ───────────────────────────────────────────────

    public async Task<IActionResult> OnGetProjectsJsonAsync(int page = 1, int pageSize = 30, string? status = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        try
        {
            var query = _db.MontecarloProjects
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.UpdatedAt);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.ProjectName, p.Status, p.UpdatedAt,
                    completedTabs = GetCompletedTabs(p.TabsJson)
                })
                .ToListAsync();

            return new JsonResult(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al listar proyectos Montecarlo");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    public async Task<IActionResult> OnGetProjectJsonAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        try
        {
            var project = await _db.MontecarloProjects
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (project == null)
                return Content(JsonSerializer.Serialize(new
                {
                    error = "Proyecto no encontrado o no pertenece a su usuario. Si cambió de cuenta o recreó la base de datos, cree un proyecto nuevo o elija uno de la lista."
                }), "application/json");

            var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(project.TabsJson)
                       ?? new Dictionary<string, ProjectTabData>();
            return new JsonResult(new { project.Id, project.ProjectName, project.Status, tabs });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener proyecto Montecarlo {Id}", id);
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    public async Task<IActionResult> OnPostSaveProjectJsonAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        try
        {
            // Read raw body to handle/strip any literal control characters before JSON parsing
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            var cleanBody = System.Text.RegularExpressions.Regex.Replace(rawBody, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var body = JsonSerializer.Deserialize<ProjectSessionSaveRequest>(cleanBody, opts);
            if (body == null || string.IsNullOrWhiteSpace(body.ProjectName))
                return Content("{\"error\":\"Nombre de proyecto requerido.\"}", "application/json");

            var tabsJson = JsonSerializer.Serialize(body.Tabs);
            MontecarloProject project;

            if (body.Id.HasValue)
            {
                project = await _db.MontecarloProjects
                    .FirstOrDefaultAsync(p => p.Id == body.Id.Value && p.UserId == userId)
                    ?? new MontecarloProject { UserId = userId, CreatedAt = DateTime.UtcNow };
            }
            else
            {
                project = new MontecarloProject { UserId = userId, CreatedAt = DateTime.UtcNow };
            }

            project.ProjectName = body.ProjectName.Trim();
            project.Status      = body.Status ?? "InProgress";
            project.TabsJson    = tabsJson;
            project.UpdatedAt   = DateTime.UtcNow;

            if (project.Id == Guid.Empty || !await _db.MontecarloProjects.AnyAsync(p => p.Id == project.Id))
                _db.MontecarloProjects.Add(project);

            await _db.SaveChangesAsync();
            return new JsonResult(new { id = project.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al guardar proyecto Montecarlo");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    public async Task<IActionResult> OnPostDeleteProjectJsonAsync([FromBody] DeleteProjectRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        try
        {
            var project = await _db.MontecarloProjects
                .FirstOrDefaultAsync(p => p.Id == body.Id && p.UserId == userId);
            if (project == null)
                return Content("{\"error\":\"Proyecto no encontrado.\"}", "application/json");
            _db.MontecarloProjects.Remove(project);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar proyecto Montecarlo {Id}", body.Id);
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    // Returns saved risks tab data for a given TallerProyecto ID.
    public async Task<IActionResult> OnGetRisksTabForTcProjectAsync(Guid tcProjectId)
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var keyName = $"tc:{tcProjectId}";
        var project = await _db.MontecarloProjects
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectName == keyName);
        if (project == null)
            return new JsonResult(new { id = (string?)null, inputJson = "{}", resultJson = "" });
        var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(project.TabsJson ?? "{}")
                   ?? new Dictionary<string, ProjectTabData>();
        var tab = tabs.GetValueOrDefault("risks") ?? new ProjectTabData();
        return new JsonResult(new { id = project.Id, inputJson = tab.InputJson, resultJson = tab.ResultJson ?? "" });
    }

    // Returns risk variables for the correlation tab from a TallerProyecto.
    // Priority: MC risks tab → TallerRiesgos fallback.
    public async Task<IActionResult> OnGetCorrVarsForTcProjectAsync(Guid tcProjectId, string source = "risks")
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var owns    = await _db.TallerProyectos.AnyAsync(p => p.Id == tcProjectId && p.UserId == userId);
        if (!owns) return Forbid();

        var vars = new List<object>();
        bool includeContracts = source == "contracts" || source == "both";
        bool includeRisks     = source == "risks"     || source == "both";

        // ── Contratos: ítems de "D - Rangos" por contrato ─────────────────────
        // Cada fila con MinKusd/ProbableKusd/MaxKusd definidos es una variable.
        // Nombre = Código contrato + Descripción ítem.
        if (includeContracts)
        {
            var contratos = await _db.TallerContratos
                .Where(c => c.ProyectoId == tcProjectId)
                .OrderBy(c => c.Orden)
                .ToListAsync();

            var contratoIds = contratos.Select(c => c.Id).ToList();
            var items = await _db.TallerItems
                .Where(i => contratoIds.Contains(i.ContratoId)
                         && !i.ViaRiesgo
                         && i.MinKusd.HasValue && i.ProbableKusd.HasValue && i.MaxKusd.HasValue)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            foreach (var c in contratos)
            {
                foreach (var item in items.Where(i => i.ContratoId == c.Id))
                {
                    vars.Add(new {
                        name = ($"{c.Codigo} {item.Descripcion}").Trim(),
                        unit = "USD",
                        dist = "Triangular",
                        p1   = Math.Round((double)item.MinKusd!.Value,      2),
                        p2   = Math.Round((double)item.ProbableKusd!.Value, 2),
                        p3   = Math.Round((double)item.MaxKusd!.Value,      2)
                    });
                }
            }
        }

        // ── Riesgos: fuente primaria = MontecarloProjects (tc:{id}), fallback = TallerRevisionesRiesgos ──
        // Usa la misma lógica de doble fuente que ResumenGlobalJson para garantizar que se
        // muestren los riesgos independientemente de dónde estén almacenados.
        if (includeRisks)
        {
            // 1. Intentar desde MontecarloProject asociado al TC proyecto
            var mcProjectKey = $"tc:{tcProjectId}";
            var mcProj = await _db.MontecarloProjects
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectName == mcProjectKey);

            var mcRisksLoaded = false;
            if (mcProj != null)
            {
                try
                {
                    var opts2 = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var tabs  = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(mcProj.TabsJson ?? "{}", opts2)
                                ?? new Dictionary<string, ProjectTabData>();
                    if (tabs.TryGetValue("risks", out var risksTab) &&
                        !string.IsNullOrEmpty(risksTab.InputJson) && risksTab.InputJson != "{}")
                    {
                        var mcItems = ParseCorrRisksFromInputJson(risksTab.InputJson);
                        foreach (var r in mcItems)
                        {
                            var tipo = r.Tipo == "Oportunidad" ? "OPO" : "AME";
                            vars.Add(new {
                                name = ($"{tipo} {r.Code} {r.Description}").Trim(),
                                unit = "USD",
                                dist = "Triangular",
                                p1   = Math.Round(r.MinImpact,        2),
                                p2   = Math.Round(r.MostLikelyImpact, 2),
                                p3   = Math.Round(r.MaxImpact,        2)
                            });
                        }
                        mcRisksLoaded = mcItems.Count > 0;
                    }
                }
                catch { /* ignorar errores de parseo */ }
            }

            // 2. Fallback: TallerRevisionesRiesgos (riesgos nativos del Taller de Costos)
            if (!mcRisksLoaded)
            {
                var revisionId = await _db.TallerRevisionesRiesgos
                    .Where(rev => rev.ProyectoId == tcProjectId)
                    .OrderByDescending(rev => rev.NumeroRevision)
                    .Select(rev => (Guid?)rev.Id)
                    .FirstOrDefaultAsync();

                if (revisionId.HasValue)
                {
                    var rows = await _db.TallerRiesgos
                        .Where(r => r.RevisionId == revisionId.Value)
                        .OrderBy(r => r.Orden)
                        .ToListAsync();

                    foreach (var r in rows)
                    {
                        var tipo = r.EsOportunidad ? "OPO" : "AME";
                        var imp  = (double)r.ImpactoProableUsd;
                        var minV = r.ImpactoMinUsd.HasValue ? (double)r.ImpactoMinUsd.Value : imp * 0.8;
                        var maxV = r.ImpactoMaxUsd.HasValue ? (double)r.ImpactoMaxUsd.Value : imp * 1.2;
                        vars.Add(new {
                            name = ($"{tipo} {r.CodigoRiesgo} {r.Descripcion}").Trim(),
                            unit = "USD",
                            dist = "Triangular",
                            p1   = Math.Round(minV, 2),
                            p2   = Math.Round(imp,  2),
                            p3   = Math.Round(maxV, 2)
                        });
                    }
                }
            }
        }

        return new JsonResult(new { vars },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    // Saves / updates a single tab in an existing project without overwriting other tabs.
    public async Task<IActionResult> OnPostSaveProjectTabAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        try
        {
            using var reader    = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody         = await reader.ReadToEndAsync();
            var cleanBody       = System.Text.RegularExpressions.Regex.Replace(rawBody, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
            var opts            = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var body            = JsonSerializer.Deserialize<ProjectTabSaveRequest>(cleanBody, opts);
            if (body == null || string.IsNullOrWhiteSpace(body.ProjectName) || string.IsNullOrWhiteSpace(body.TabId))
                return Content("{\"error\":\"Nombre de proyecto y tab son requeridos.\"}", "application/json");

            MontecarloProject project;
            if (body.Id.HasValue)
            {
                project = await _db.MontecarloProjects
                    .FirstOrDefaultAsync(p => p.Id == body.Id.Value && p.UserId == userId)
                    ?? new MontecarloProject { UserId = userId, CreatedAt = DateTime.UtcNow };
            }
            else
            {
                project = new MontecarloProject { UserId = userId, CreatedAt = DateTime.UtcNow };
            }

            // Merge: load existing tabs, update only the requested tab
            var tabs = string.IsNullOrEmpty(project.TabsJson)
                ? new Dictionary<string, ProjectTabData>()
                : JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(project.TabsJson)
                  ?? new Dictionary<string, ProjectTabData>();

            // Preservar resultJson existente si el nuevo está vacío (evita borrar cálculos previos)
            var existing = tabs.GetValueOrDefault(body.TabId);
            if (existing != null && string.IsNullOrEmpty(body.TabData.ResultJson) && !string.IsNullOrEmpty(existing.ResultJson))
                body.TabData.ResultJson = existing.ResultJson;
            tabs[body.TabId] = body.TabData;

            project.ProjectName = body.ProjectName.Trim();
            project.Status      = "InProgress";
            project.TabsJson    = JsonSerializer.Serialize(tabs);
            project.UpdatedAt   = DateTime.UtcNow;

            if (project.Id == Guid.Empty || !await _db.MontecarloProjects.AnyAsync(p => p.Id == project.Id))
                _db.MontecarloProjects.Add(project);

            await _db.SaveChangesAsync();
            return new JsonResult(new { id = project.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al guardar tab de proyecto Montecarlo");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    private static List<string> GetCompletedTabs(string tabsJson)
    {
        try
        {
            var tabs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tabsJson);
            if (tabs == null) return new();
            return tabs
                .Where(kv => kv.Value.TryGetProperty("resultJson", out var rj) && rj.GetString() is { Length: > 0 })
                .Select(kv => kv.Key)
                .ToList();
        }
        catch { return new(); }
    }

    // ── AJAX: Correlaciones ───────────────────────────────────────────────────

    public async Task<IActionResult> OnPostCorrelationAjaxAsync([FromBody] CorrelationAnalysisRequestModel request)
    {
        try
        {
            var json = await _api.PostRawAsync("api/montecarlo/correlation", request);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular correlaciones");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    // ── Handler AJAX historial ────────────────────────────────────────────────

    public async Task<IActionResult> OnGetHistoryJsonAsync(int page = 1, int pageSize = 20, string? simulationType = null)
    {
        var ruta = $"api/montecarlo/history?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(simulationType))
            ruta += $"&simulationType={Uri.EscapeDataString(simulationType)}";
        try
        {
            var json = await _api.GetRawAsync(ruta);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener historial Montecarlo");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Transforms CostComponent into the API-expected shape where opportunities/threats
    // are arrays of CostRisk objects (Bernoulli event = single-item list when amount > 0).
    private static object BuildCostComponentApi(CostComponent c)
    {
        var opps = c.OpportunityProbability > 0 && c.OpportunityAmount > 0
            ? new[] { new { name = "Oportunidad", probability = c.OpportunityProbability,
                            minImpact = c.OpportunityAmount * 0.7,
                            mostLikelyImpact = c.OpportunityAmount,
                            maxImpact = c.OpportunityAmount * 1.3,
                            distribution = c.Distribution } }
            : Array.Empty<object>();

        var threats = c.ThreatProbability > 0 && c.ThreatAmount > 0
            ? new[] { new { name = "Amenaza", probability = c.ThreatProbability,
                            minImpact = c.ThreatAmount * 0.7,
                            mostLikelyImpact = c.ThreatAmount,
                            maxImpact = c.ThreatAmount * 1.3,
                            distribution = c.Distribution } }
            : Array.Empty<object>();

        return new
        {
            name = c.Name,
            baseCost = c.BaseCost,
            estimationClass = c.EstimationClass,
            minCost = c.MinCost,
            mostLikelyCost = c.MostLikelyCost,
            maxCost = c.MaxCost,
            opportunities = opps,
            threats,
            distribution = c.Distribution,
            observations = c.Observations
        };
    }

    // ── Handler AJAX: ASISTENTE GPR (experto Monte Carlo) ───────────────────

    public async Task<IActionResult> OnPostAiConsultAsync([FromBody] McAiConsultRequest request)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(AriaSystemPrompt);

            // Contexto del proyecto
            if (!string.IsNullOrWhiteSpace(request.ProjectName))
                sb.AppendLine($"NOMBRE DEL PROYECTO: {request.ProjectName}");
            if (!string.IsNullOrWhiteSpace(request.TabId))
                sb.AppendLine($"PESTAÑA ACTIVA AL CONSULTAR: {request.TabId}");

            // Análisis Taller de Costos CODELCO (tiempo real — cargado desde el cliente)
            if (!string.IsNullOrWhiteSpace(request.TcContextJson))
            {
                sb.AppendLine();
                sb.AppendLine("=== TALLER DE COSTOS CODELCO — ANÁLISIS EN TIEMPO REAL ===");
                sb.AppendLine(request.TcContextJson);
                sb.AppendLine("============================================================");
            }

            // Resultados de simulaciones Monte Carlo
            if (!string.IsNullOrWhiteSpace(request.ContextJson))
            {
                sb.AppendLine();
                sb.AppendLine("=== RESULTADOS DE SIMULACIONES MONTE CARLO DISPONIBLES ===");
                sb.AppendLine(request.ContextJson);
                sb.AppendLine("===========================================================");
            }
            else if (string.IsNullOrWhiteSpace(request.TcContextJson))
            {
                sb.AppendLine();
                sb.AppendLine("NOTA: No se han ejecutado simulaciones ni cargado un proyecto del Taller de Costos. Orienta al usuario sobre qué ejecutar primero y por qué.");
            }

            sb.AppendLine();
            sb.AppendLine("INSTRUCCIÓN OBLIGATORIA: Responde ÚNICAMENTE en español. Está estrictamente prohibido usar cualquier palabra o frase en inglés.");
            sb.AppendLine($"CONSULTA DEL USUARIO: {request.Question}");

            var respuesta = await _ollama.GenerateAsync(sb.ToString());
            return Content(
                $"{{\"response\":{System.Text.Json.JsonSerializer.Serialize(respuesta)}}}",
                "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en ASISTENTE GPR Monte Carlo");
            var msg = ex.Message ?? "";
            if (ex is OperationCanceledException
                || msg.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
                || (msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                    && msg.Contains("canceled", StringComparison.OrdinalIgnoreCase)))
            {
                msg = "Tiempo de espera agotado al generar la respuesta. Compruebe que el servicio Api_Ollama está en ejecución (OllamaApi:BaseUrl) y responde; si el modelo es lento o el contexto es muy grande, aumente OllamaApi:HttpTimeoutSeconds en appsettings.";
            }
            return Content(JsonSerializer.Serialize(new { error = msg }), "application/json");
        }
    }

    // ── IA: Sugerencia automática de matriz de correlación ───────────────────
    public async Task<IActionResult> OnPostCorrMatrixAiAsync([FromBody] CorrMatrixAiRequest request)
    {
        try
        {
            var vars = request.Variables ?? new List<CorrAiVarDto>();
            if (vars.Count < 2)
                return Content("{\"error\":\"Se necesitan al menos 2 variables para analizar correlaciones.\"}", "application/json");

            int n     = vars.Count;
            int nPairs = n * (n - 1) / 2;

            // Umbral: >10 variables (>45 pares) → el prompt sería demasiado largo para Ollama local.
            // En ese caso aplicamos reglas de dominio directamente sin llamar al modelo IA.
            const int AiMaxVars = 10;
            bool useAI = n <= AiMaxVars;

            string proyecto = string.IsNullOrWhiteSpace(request.ProjectName) ? "Proyecto CODELCO" : request.ProjectName;
            string fuente = request.Source switch {
                "contracts" => "contratos de costo",
                "risks"     => "riesgos del proyecto",
                "both"      => "contratos y riesgos combinados",
                _           => "variables ingresadas"
            };
            bool withJust = request.WithJustification;

            string aiResponse = "";
            bool   jsonOk     = false;
            List<object> parsedPairs;
            string parsedScenario, parsedAnalysis;

            if (useAI)
            {
                // Variables en texto compacto
                var sb = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    var v = vars[i];
                    sb.AppendLine($"{i}: {v.Name} ({v.Unit}, {v.Distribution}, min={v.P1:N0}, prob={v.P2:N0}, max={v.P3:N0})");
                }
                string varList = sb.ToString().Trim();

                // Pares en texto compacto
                var pairsText = new StringBuilder();
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        pairsText.AppendLine($"({i},{j}): {vars[i].Name} ↔ {vars[j].Name}");

                // Prompt corto y directo — mejor compliance en modelos locales
                var prompt = new StringBuilder();
                prompt.AppendLine("Eres ARIA, experto en correlaciones de riesgo para proyectos mineros CODELCO.");
                prompt.AppendLine("RESPONDE ÚNICAMENTE CON JSON VÁLIDO — sin texto antes ni después del JSON.");
                prompt.AppendLine();
                prompt.AppendLine($"PROYECTO: {proyecto}");
                prompt.AppendLine($"FUENTE: {fuente}");
                prompt.AppendLine();
                prompt.AppendLine("VARIABLES:");
                prompt.AppendLine(varList);
                prompt.AppendLine();
                prompt.AppendLine("PARES A CORRELACIONAR:");
                prompt.Append(pairsText);
                prompt.AppendLine();
                prompt.AppendLine("CRITERIOS (aplica según tipo de variable):");
                prompt.AppendLine("- Contratos misma disciplina: 0.50-0.75");
                prompt.AppendLine("- Contratos distintas fases: 0.20-0.45");
                prompt.AppendLine("- Riesgos misma categoría: 0.45-0.65");
                prompt.AppendLine("- Riesgos distintas categorías: 0.10-0.35");
                prompt.AppendLine("- Amenaza vs Oportunidad: -0.20 a -0.55");
                prompt.AppendLine("- Sin relación causal: 0.05-0.15");
                prompt.AppendLine("- Factor mercado minero CODELCO suma 0.10-0.20 a todos los pares");
                prompt.AppendLine();
                prompt.AppendLine("FORMATO DE RESPUESTA (JSON exacto, sin texto adicional):");
                if (withJust)
                    prompt.AppendLine("{\"scenario\":\"nombre escenario\",\"pairs\":[{\"i\":0,\"j\":1,\"value\":0.60,\"reason\":\"razón en español\"}],\"analysis\":\"análisis narrativo 2-3 párrafos en español\"}");
                else
                    prompt.AppendLine("{\"scenario\":\"nombre escenario\",\"pairs\":[{\"i\":0,\"j\":1,\"value\":0.60}]}");

                aiResponse = await _ollama.GenerateAsync(prompt.ToString());
            }
            else
            {
                // >10 variables: usar reglas de dominio directamente (sin Ollama)
                parsedPairs = new List<object>();
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        double val = ComputeDomainCorrelation(vars[i], vars[j], request.Source ?? "");
                        string reason = withJust ? BuildDomainReason(vars[i], vars[j], val, request.Source ?? "") : "";
                        parsedPairs.Add(new { i, j, value = val, reason });
                    }
                parsedScenario = $"Reglas de dominio CODELCO ({n} variables, {nPairs} pares)";
                parsedAnalysis = withJust
                    ? $"Con {n} variables ({nPairs} pares) se aplican automáticamente las reglas de dominio CODELCO para no saturar el modelo de IA local. " +
                      "Revise los valores sugeridos y ajuste manualmente los pares más relevantes para su proyecto."
                    : null!;

                var resultOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                return new JsonResult(new {
                    scenario          = parsedScenario,
                    pairs             = parsedPairs,
                    analysis          = withJust ? parsedAnalysis : null,
                    aiRaw             = (string?)null,
                    withJustification = withJust,
                    projectName       = proyecto,
                    source            = fuente,
                    generatedAt       = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    domainRulesOnly   = true,
                    nVars             = n,
                    nPairs
                }, resultOpts);
            }

            // ── Estrategia 1: extraer bloque ```json ... ```
            string? jsonStr = null;
            var m1 = System.Text.RegularExpressions.Regex.Match(aiResponse, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m1.Success) jsonStr = m1.Groups[1].Value.Trim();

            // ── Estrategia 2: primera llave balanceada { ... }
            if (jsonStr == null)
            {
                int start = aiResponse.IndexOf('{');
                if (start >= 0)
                {
                    int depth = 0, end = -1;
                    for (int k = start; k < aiResponse.Length; k++)
                    {
                        if (aiResponse[k] == '{') depth++;
                        else if (aiResponse[k] == '}') { depth--; if (depth == 0) { end = k; break; } }
                    }
                    if (end > start) jsonStr = aiResponse.Substring(start, end - start + 1).Trim();
                }
            }

            // ── Estrategia 3: respuesta completa (puede ser JSON puro sin llaves extra)
            if (jsonStr == null) jsonStr = aiResponse.Trim();

            // Limpiar problemas comunes del LLM: comas finales antes de } o ]
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @",\s*([\}\]])", "$1");

            // Intentar parsear el JSON (solo si se llegó aquí, es decir useAI == true)
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                parsedScenario = root.TryGetProperty("scenario", out var sc) ? sc.GetString() ?? "" : "Análisis IA";
                parsedAnalysis = root.TryGetProperty("analysis", out var an) ? an.GetString() ?? aiResponse : aiResponse;

                parsedPairs = new List<object>();
                if (root.TryGetProperty("pairs", out var pArr) && pArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in pArr.EnumerateArray())
                    {
                        int pi = el.TryGetProperty("i", out var pi_) ? pi_.GetInt32() : -1;
                        int pj = el.TryGetProperty("j", out var pj_) ? pj_.GetInt32() : -1;
                        double pv = 0;
                        if (el.TryGetProperty("value", out var pv_))
                        {
                            if (pv_.ValueKind == JsonValueKind.Number) pv = pv_.GetDouble();
                            else double.TryParse(pv_.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pv);
                        }
                        string pr = el.TryGetProperty("reason", out var pr_) ? pr_.GetString() ?? "" : "";
                        if (pi >= 0 && pj >= 0 && pi < n && pj < n)
                            parsedPairs.Add(new { i = pi, j = pj, value = Math.Round(Math.Max(-1.0, Math.Min(1.0, pv)), 2), reason = pr });
                    }
                }
                jsonOk = parsedPairs.Count > 0;
            }
            catch
            {
                parsedPairs = new List<object>();
                parsedScenario = "Análisis IA";
                parsedAnalysis = aiResponse;
            }

            // ── Fallback: si el LLM no devolvió pares válidos, aplicar reglas de dominio
            if (!jsonOk)
            {
                parsedPairs = new List<object>();
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double val = ComputeDomainCorrelation(vars[i], vars[j], request.Source ?? "");
                        string reason = BuildDomainReason(vars[i], vars[j], val, request.Source ?? "");
                        parsedPairs.Add(new { i, j, value = val, reason });
                    }
                }
                parsedScenario = "Correlación por reglas de dominio CODELCO (IA no generó JSON válido)";
                if (string.IsNullOrWhiteSpace(parsedAnalysis) || parsedAnalysis == aiResponse)
                    parsedAnalysis = (string.IsNullOrWhiteSpace(aiResponse) ? "" : aiResponse + "\n\n") +
                        "Nota: Los valores fueron calculados automáticamente con reglas de dominio CODELCO porque el modelo de IA no generó JSON estructurado en esta consulta.";
            }

            var result = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return new JsonResult(new {
                scenario         = parsedScenario,
                pairs            = parsedPairs,
                analysis         = withJust ? parsedAnalysis : null,
                aiRaw            = jsonOk ? null : aiResponse,
                withJustification= withJust,
                projectName      = proyecto,
                source           = fuente,
                generatedAt      = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            }, result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en sugerencia IA de correlación");
            return Content(JsonSerializer.Serialize(new { error = ex.Message }), "application/json");
        }
    }

    // Calcula correlación por reglas de dominio cuando la IA no puede responder
    private static double ComputeDomainCorrelation(CorrAiVarDto a, CorrAiVarDto b, string source)
    {
        string na = (a.Name ?? "").ToLower(), nb = (b.Name ?? "").ToLower();

        // Detectar oportunidades (nombres con "oport" o negativos)
        bool aIsOpp = na.Contains("oport") || a.P2 < 0;
        bool bIsOpp = nb.Contains("oport") || b.P2 < 0;
        if (aIsOpp != bIsOpp) return -0.35;  // amenaza vs oportunidad

        // Detectar palabras clave de disciplina en contratos
        string[] civil    = { "civil", "obra", "tierra", "excavac", "hormig", "fundac" };
        string[] electro  = { "electr", "instrument", "control", "autom" };
        string[] mecanic  = { "mecán", "mecanic", "equip", "tubería", "montaj" };
        string[] ingDesig = { "ingenier", "diseño", "básic", "detall" };
        string[] comiss   = { "comision", "puesta", "marcha", "start" };

        Func<string, string[], bool> has = (s, kw) => kw.Any(k => s.Contains(k));

        if (source == "contracts" || source == "both")
        {
            // Misma disciplina
            foreach (var kw in new[] { civil, electro, mecanic, ingDesig, comiss })
                if (has(na, kw) && has(nb, kw)) return Math.Round(0.55 + (new Random().NextDouble() * 0.10), 2);

            // Ingeniería → construcción: dependencia parcial
            if ((has(na, ingDesig) && has(nb, civil)) || (has(nb, ingDesig) && has(na, civil))) return 0.35;
            // Construcción → comisionamiento
            if ((has(na, civil) && has(nb, comiss)) || (has(nb, civil) && has(na, comiss))) return 0.30;

            // Contratos genéricos: factor sistémico de mercado
            return Math.Round(0.30 + (new Random().NextDouble() * 0.15), 2);
        }

        if (source == "risks")
        {
            // Riesgos con palabras clave similares = misma categoría
            string[] clima    = { "clima", "lluvia", "tempe", "sismo", "geotec" };
            string[] regulat  = { "permiso", "regulat", "ambiental", "legal", "normat" };
            string[] contrat  = { "contratist", "proveedor", "subcontrat", "supply" };
            string[] segur    = { "accident", "seguridad", "hsse", "lesión" };

            foreach (var kw in new[] { clima, regulat, contrat, segur })
                if (has(na, kw) && has(nb, kw)) return Math.Round(0.50 + (new Random().NextDouble() * 0.10), 2);

            return Math.Round(0.25 + (new Random().NextDouble() * 0.15), 2);
        }

        // Default: factor sistémico base
        return Math.Round(0.25 + (new Random().NextDouble() * 0.15), 2);
    }

    private static string BuildDomainReason(CorrAiVarDto a, CorrAiVarDto b, double val, string source)
    {
        if (val < -0.1) return $"Las variables '{a.Name}' y '{b.Name}' representan eventos opuestos (amenaza/oportunidad): cuando una ocurre, la otra tiene menor probabilidad.";
        if (val >= 0.5) return $"Alta correlación entre '{a.Name}' y '{b.Name}': comparten disciplina, recursos, proveedores o condiciones de mercado.";
        if (val >= 0.3) return $"Correlación moderada entre '{a.Name}' y '{b.Name}': dependencia parcial por factores sistémicos del proyecto (inflación, clima, regulación).";
        return $"Correlación débil entre '{a.Name}' y '{b.Name}': relación indirecta a través del factor sistémico de mercado minero CODELCO.";
    }

    private async Task<IActionResult> RunAndRedirect<T>(string ruta, T request, string tab)
    {
        try
        {
            TempData["ResultJson"] = await _api.PostRawAsync(ruta, request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al llamar Montecarlo API ({Ruta})", ruta);
            TempData["ApiError"] = ex.Message;
        }
        return RedirectToPage(new { tab });
    }

    private record CorrRiskItem(string Tipo, string Code, string Description,
        double MinImpact, double MostLikelyImpact, double MaxImpact);

    // ── Helper: parsear riesgos desde el InputJson del tab "risks" de MontecarloProjects ──
    // Misma lógica que TallerCostos.cshtml.cs → ParseMcRisksFromInputJson
    private static List<CorrRiskItem> ParseCorrRisksFromInputJson(string inputJson)
    {
        var result = new List<CorrRiskItem>();
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(inputJson)
                       ?? new Dictionary<string, string>();
            var maxIdx = -1;
            foreach (var key in dict.Keys)
            {
                var m = System.Text.RegularExpressions.Regex.Match(key, @"Risks\[(\d+)\]");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                    maxIdx = Math.Max(maxIdx, idx);
            }
            for (var i = 0; i <= maxIdx; i++)
            {
                var likely = GetCorrDouble(dict, $"Risks[{i}].MostLikelyImpact");
                if (likely == 0) continue;
                var minV = GetCorrDouble(dict, $"Risks[{i}].MinImpact");
                var maxV = GetCorrDouble(dict, $"Risks[{i}].MaxImpact");
                result.Add(new CorrRiskItem(
                    Tipo:             dict.GetValueOrDefault($"Risks[{i}].Tipo")        ?? "Amenaza",
                    Code:             dict.GetValueOrDefault($"Risks[{i}].Code")        ?? "",
                    Description:      dict.GetValueOrDefault($"Risks[{i}].Description") ?? "",
                    MinImpact:        minV  == 0 ? likely * 0.8 : minV,
                    MostLikelyImpact: likely,
                    MaxImpact:        maxV  == 0 ? likely * 1.2 : maxV
                ));
            }
        }
        catch { /* ignorar errores de parseo */ }
        return result;
    }

    private static double GetCorrDouble(Dictionary<string, string> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return 0;
        v = v.Trim();
        var ns = System.Globalization.NumberStyles.Float;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        // Formato chileno/español: coma = decimal, punto = separador de miles
        // Ejemplos: "992.466" → 992466 | "992.466,50" → 992466.5 | "992,47" → 992.47
        if (v.Contains(','))
        {
            // Hay coma: punto es miles, coma es decimal
            var normalized = v.Replace(".", "").Replace(",", ".");
            if (double.TryParse(normalized, ns, inv, out var r1)) return r1;
        }
        else if (v.Contains('.'))
        {
            var parts = v.Split('.');
            // Si el último grupo tiene exactamente 3 dígitos → punto es separador de miles
            if (parts[^1].Length == 3 && parts[^1].All(char.IsDigit))
            {
                var stripped = v.Replace(".", "");
                if (double.TryParse(stripped, ns, inv, out var r2)) return r2;
            }
            else
            {
                // Punto decimal normal (1-2 dígitos decimales)
                if (double.TryParse(v, ns, inv, out var r3)) return r3;
            }
        }
        else
        {
            if (double.TryParse(v, ns, inv, out var r4)) return r4;
        }
        return 0;
    }
}
