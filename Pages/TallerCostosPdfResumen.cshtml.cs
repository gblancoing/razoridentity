using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Montecarlo;
using RazorIdentity.Models.TallerCostos;
using RazorIdentity.Services;
using System.Security.Claims;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class TallerCostosPdfResumenModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MonteCarloService    _mc;

    public TallerCostosPdfResumenModel(ApplicationDbContext db, MonteCarloService mc)
    {
        _db = db;
        _mc = mc;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private static readonly JsonSerializerOptions _ciOpts = new() { PropertyNameCaseInsensitive = true };

    // ── Datos de salida ──────────────────────────────────────────────────────
    public TallerProyecto Proyecto { get; private set; } = null!;

    // Totales del proyecto
    public double TotCapex       { get; private set; }
    public double TotComp        { get; private set; }
    /// <summary>Suma de ítems «Por comprometer» (misma base que resumen web).</summary>
    public double TotItems       { get; private set; }
    public double TotEAT         { get; private set; }
    public double TotCerteza     { get; private set; }
    public double TotIncert      { get; private set; }
    /// <summary>Incertidumbre por vía riesgo al probable fijo (no entra al MC Bloque F).</summary>
    public double TotViaRiesgoPorComp { get; private set; }
    public int    TotContratos   { get; private set; }
    public int    WithMc         { get; private set; }
    public double? McP10Agg      { get; private set; }
    public double? McP50Agg      { get; private set; }
    public double? McP80Agg      { get; private set; }
    public double? McP90Agg      { get; private set; }

    // Propiedades derivadas de totales
    public double TotSlack       => TotCapex - TotEAT;
    public double TotPctEAT      => TotCapex > 0 ? TotEAT  / TotCapex * 100 : 0;
    public double TotPctComp     => TotCapex > 0 ? TotComp / TotCapex * 100 : 0;
    public double TotPctCerteza  => (TotCerteza + TotIncert) > 0 ? TotCerteza / (TotCerteza + TotIncert) * 100 : 0;
    public int    WithoutMc      => TotContratos - WithMc;

    // EAT con MC agregado por contrato (comprometido + certeza por comp. + vía riesgo al probable + percentiles MC)
    public double? EatMcP10 => McP10Agg.HasValue ? TotComp + TotCerteza + TotViaRiesgoPorComp + McP10Agg.Value : null;
    public double? EatMcP50 => McP50Agg.HasValue ? TotComp + TotCerteza + TotViaRiesgoPorComp + McP50Agg.Value : null;
    public double? EatMcP80 => McP80Agg.HasValue ? TotComp + TotCerteza + TotViaRiesgoPorComp + McP80Agg.Value : null;
    public double? EatMcP90 => McP90Agg.HasValue ? TotComp + TotCerteza + TotViaRiesgoPorComp + McP90Agg.Value : null;

    // EAT combinado: RunCombinedEATAnalysis ya devuelve EAT total (base fija + triángulos + riesgos)
    public double? EatCombP10 => EatCombinado?.P10;
    public double? EatCombP50 => EatCombinado?.P50;
    public double? EatCombP80 => EatCombinado?.P80;
    public double? EatCombP90 => EatCombinado?.P90;

    // Contratos
    public record ContratoRow(string Codigo, string Nombre,
        double Capex, double Comprometido, double PorComprometer, double EAT, double Slack,
        double PctEAT, double PctComprometido,
        bool HasMc, double? McP10, double? McP50, double? McP80, double? McP90, double? McCV,
        int TotalItems, double CertTotal, double IncertTotal,
        Guid? FamiliaId, string? FamiliaNombre);
    public List<ContratoRow> Contratos { get; private set; } = new();

    // Familias
    public record FamiliaRow(Guid Id, string Nombre, int Orden);
    public List<FamiliaRow> Familias { get; private set; } = new();

    // Distribución por clase de estimación (global)
    public record ClaseItem(string Clase, int Count, double Pct);
    public List<ClaseItem> ClasesGlobal { get; private set; } = new();

    // Top 3 ítems con mayor spread (mayor exposición)
    public record ExposicionItem(string Contrato, string CodigoItem, string Descripcion, double Spread, double MinUsd);
    public List<ExposicionItem> TopExposicion { get; private set; } = new();

    // Riesgos
    public record RiskRow(
        string Codigo, string Descripcion, string PlanRespuesta,
        double ProbPct, double Min, double Moda, double Max, double VE,
        bool EsAmenaza, string NivelLabel, string NivelColor, int NivelInt);
    public List<RiskRow>  Riesgos        { get; private set; } = new();
    public string         FuenteRiesgos  { get; private set; } = "sin_datos";
    public string?        RevisionLabel  { get; private set; }
    public DateTime?      McActualizado  { get; private set; }

    // Distribución por nivel de riesgo CODELCO
    public record NivelDistRow(string Label, string Color, int Count);
    public List<NivelDistRow> DistribucionNiveles { get; private set; } = new();

    // Valores esperados de riesgos
    public double VeAmenazas      => Riesgos.Where(r =>  r.EsAmenaza).Sum(r => r.VE);
    public double VeOportunidades => Riesgos.Where(r => !r.EsAmenaza).Sum(r => Math.Abs(r.VE));
    public double VeNeto          => Riesgos.Sum(r => r.VE);

    // EAT Combinado
    public CombinedEATResult? EatCombinado { get; private set; }
    /// <summary>Misma corrida acoplada que <see cref="EatCombinado"/>: solo triángulos de ítems (sin riesgos MC).</summary>
    public CombinedEATResult? EatCombinadoSoloIncertidumbre { get; private set; }
    public string ChartDataJson { get; private set; } = "null";

    /// <summary>Último resultado guardado en pestaña Riesgos (Monte Carlo), si existe — misma fuente que Resumen Global.</summary>
    public RiskMcResponse? SavedRisksSimulation { get; private set; }

    /// <summary>P50/P80 amenazas y P50/P80 ahorro oportunidades para informes: prioridad = simulación guardada (RunRiskAnalysis), si no percentiles de <see cref="EatCombinado"/>.</summary>
    public double? AmenazasP50Informe =>
        EatCombinado is null ? null
        : SavedRisksSimulation?.AdditionalMetrics.ThreatP50 ?? EatCombinado.ContingenciaAmenazasP50;

    public double? AmenazasP80Informe =>
        EatCombinado is null ? null
        : SavedRisksSimulation?.AdditionalMetrics.ThreatP80 ?? EatCombinado.ContingenciaAmenazasP80;

    public double? AmenazasP90Informe =>
        EatCombinado is null ? null
        : SavedRisksSimulation?.AdditionalMetrics.ThreatP90 ?? EatCombinado.ContingenciaAmenazasP90;

    /// <summary>Ahorro en USD (positivo). La simulación guardada usa magnitudes; el combinado usa impactos ≤0.</summary>
    public double? OportunidadesAhorroP50Informe =>
        EatCombinado is null ? null
        : SavedRisksSimulation?.AdditionalMetrics.OpportunityP50
          ?? (Math.Abs(EatCombinado.ImpactoOportunidadesP50) > 1e-6
              ? (double?)Math.Abs(EatCombinado.ImpactoOportunidadesP50) : null);

    public double? OportunidadesAhorroP80Informe =>
        EatCombinado is null ? null
        : SavedRisksSimulation?.AdditionalMetrics.OpportunityP80
          ?? (Math.Abs(EatCombinado.ImpactoOportunidadesP80) > 1e-6
              ? (double?)Math.Abs(EatCombinado.ImpactoOportunidadesP80) : null);

    /// <summary>Escenarios de correlación uniforme (cópula Gaussiana), alineado con la vista web (variables «ambos»).</summary>
    public record CorrelacionEscenarioPdfRow(
        string Etiqueta,
        string RhoTexto,
        double EatP50,
        double EatP80,
        double EatP90,
        double DeltaP50Pct,
        double DeltaP90Pct);

    public IReadOnlyList<CorrelacionEscenarioPdfRow> CorrelacionEscenarios { get; private set; } = Array.Empty<CorrelacionEscenarioPdfRow>();
    public int CorrelacionNItemsIncert { get; private set; }
    public int CorrelacionNRiesgosMc { get; private set; }

    /// <summary>True cuando la URL es /TallerCostosPdfEjecutivo/… (informe compacto CODELCO).</summary>
    public bool EsVistaEjecutiva { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid proyectoId)
    {
        var proyecto = await _db.TallerProyectos
            .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UserId == UserId);
        if (proyecto is null) return Forbid();
        Proyecto = proyecto;

        var contratos = await _db.TallerContratos
            .Include(c => c.Items)
            .Include(c => c.Familia)
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        Familias = await _db.TallerFamilias
            .Where(f => f.ProyectoId == proyectoId)
            .OrderBy(f => f.Orden)
            .Select(f => new FamiliaRow(f.Id, f.Nombre, f.Orden))
            .ToListAsync();

        if (!contratos.Any()) return Page();

        // ── MC resultados por contrato ────────────────────────────────────────
        var contratoIds = contratos.Select(c => c.Id).ToList();
        var mcAll = await _db.TallerMcResultados
            .Where(r => contratoIds.Contains(r.ContratoId))
            .Select(r => new { r.ContratoId, r.FechaCalculo, r.P10, r.P50, r.P80, r.P90, r.Media, r.DesvStd })
            .ToListAsync();

        var mcLatest = mcAll
            .GroupBy(r => r.ContratoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.FechaCalculo).First());

        // ── Agregar contratos ─────────────────────────────────────────────────
        foreach (var c in contratos)
        {
            var items    = c.Items.ToList();
            var porComp  = items.Where(i => i.EsPorComprometer).Sum(i => (double)i.CostoUsd);
            var eat      = (double)c.CompometidoUsd + porComp;
            var capex    = (double)c.CapexUsd;
            var cert     = items.Where(i => i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd);
            var incert   = items.Where(i => !i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd);
            var pctEAT   = capex > 0 ? eat / capex * 100 : 0;
            var pctComp  = capex > 0 ? (double)c.CompometidoUsd / capex * 100 : 0;
            mcLatest.TryGetValue(c.Id, out var mc);
            var mcCV = (mc != null && mc.Media > 0) ? (double?)(mc.DesvStd / mc.Media * 100) : null;

            Contratos.Add(new ContratoRow(
                c.Codigo, c.NombrePaquete,
                capex, (double)c.CompometidoUsd,
                porComp,
                eat, capex - eat,
                pctEAT, pctComp,
                mc != null, mc?.P10, mc?.P50, mc?.P80, mc?.P90, mcCV,
                items.Count, cert, incert,
                c.FamiliaId, c.Familia?.Nombre));
        }

        // ── Totales ───────────────────────────────────────────────────────────
        TotContratos = contratos.Count;
        TotCapex     = contratos.Sum(c => (double)c.CapexUsd);
        TotComp      = contratos.Sum(c => (double)c.CompometidoUsd);
        TotItems     = contratos.Sum(c => c.Items.Where(i => i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        TotEAT       = TotComp + TotItems;
        TotCerteza   = contratos.Sum(c => c.Items.Where(i => i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        TotIncert    = contratos.Sum(c => c.Items.Where(i => !i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        TotViaRiesgoPorComp = contratos.Sum(c => c.Items.Where(i => !i.EsCerteza && i.EsPorComprometer && i.ViaRiesgo).Sum(i => (double)i.CostoUsd));
        WithMc       = mcLatest.Count;

        if (WithMc > 0)
        {
            McP10Agg = mcLatest.Values.Sum(r => r.P10);
            McP50Agg = mcLatest.Values.Sum(r => r.P50);
            McP80Agg = mcLatest.Values.Sum(r => r.P80);
            McP90Agg = mcLatest.Values.Sum(r => r.P90);
        }

        // ── Clase de estimación global ────────────────────────────────────────
        var clasesDict = new Dictionary<string, int>();
        foreach (var c in contratos)
            foreach (var item in c.Items)
            {
                var clase = string.IsNullOrWhiteSpace(item.ClaseEstimacion) ? "Sin clase" : item.ClaseEstimacion;
                clasesDict.TryGetValue(clase, out var cnt);
                clasesDict[clase] = cnt + 1;
            }
        var totalClaseItems = clasesDict.Values.Sum();
        ClasesGlobal = clasesDict
            .OrderBy(kv => kv.Key)
            .Select(kv => new ClaseItem(kv.Key, kv.Value,
                totalClaseItems > 0 ? kv.Value * 100.0 / totalClaseItems : 0))
            .ToList();

        // ── Top 3 ítems por spread ────────────────────────────────────────────
        TopExposicion = contratos
            .SelectMany(c => c.Items
                .Where(i => !i.EsCerteza && i.EsPorComprometer && i.MaxKusd.HasValue && i.MinKusd.HasValue)
                .Select(i => new ExposicionItem(
                    c.Codigo, i.CodigoItem, i.Descripcion,
                    (double)(i.MaxKusd!.Value - i.MinKusd!.Value),
                    (double)i.MinKusd!.Value)))
            .OrderByDescending(x => x.Spread)
            .Take(3)
            .ToList();

        // ── Riesgos (alineado con ResumenGlobalJson: Tipo MC, triángulo signado, fusión MC+Taller) ──
        var rev = await _db.TallerRevisionesRiesgos
            .Where(r => r.ProyectoId == proyectoId)
            .OrderByDescending(r => r.NumeroRevision)
            .FirstOrDefaultAsync();

        var mcProj = await _db.MontecarloProjects
            .FirstOrDefaultAsync(p => p.UserId == UserId && p.ProjectName == $"tc:{proyectoId}");

        var mcRiskItems = new List<RiskItem>();
        if (mcProj != null)
        {
            McActualizado = mcProj.UpdatedAt;
            try
            {
                var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(
                    mcProj.TabsJson ?? "{}", _ciOpts) ?? new();
                if (tabs.TryGetValue("risks", out var risksTab))
                {
                    if (!string.IsNullOrEmpty(risksTab.InputJson) && risksTab.InputJson != "{}")
                        mcRiskItems = ParseRisksFromInput(risksTab.InputJson);
                    if (!string.IsNullOrEmpty(risksTab.ResultJson) && risksTab.ResultJson != "{}")
                    {
                        try
                        {
                            SavedRisksSimulation = JsonSerializer.Deserialize<RiskMcResponse>(risksTab.ResultJson, _ciOpts);
                        }
                        catch { /* JSON antiguo o corrupto */ }
                    }
                }
            }
            catch { }
        }

        var risks = new List<CombinedEATRisk>();
        var useMcRisks = mcRiskItems.Count > 0;

        if (rev != null)
            RevisionLabel = $"Rev. {rev.NumeroRevision} — {rev.Descripcion} ({rev.FechaCreacion:dd/MM/yyyy})";

        if (useMcRisks)
        {
            FuenteRiesgos = "montecarlo";
            foreach (var r in mcRiskItems)
            {
                McRiskFormTriangular.ToSignedVertices(r, out var triMin, out var triMode, out var triMax);
                risks.Add(new CombinedEATRisk {
                    Probability = r.Probability / 100.0,
                    Min = triMin, Mode = triMode, Max = triMax
                });
                var nivel = CodelcoNivel(r.Probability);
                var ve = r.Probability / 100.0 * triMode;
                Riesgos.Add(new RiskRow(r.Code ?? "", r.Description ?? "", r.ResponsePlan ?? "",
                    r.Probability, triMin, triMode, triMax, ve,
                    triMode >= 0,
                    CodelcoLabel(nivel), CodelcoHexColor(nivel), nivel));
            }

            if (rev != null)
            {
                var mcCodes = new HashSet<string>(
                    mcRiskItems.Select(x => (x.Code ?? "").Trim()).Where(s => s.Length > 0),
                    StringComparer.OrdinalIgnoreCase);
                await AppendTallerRiesgosToPdfListsAsync(rev.Id, risks, Riesgos, mcCodes);
            }
        }
        else if (rev != null)
        {
            FuenteRiesgos = "taller";
            await AppendTallerRiesgosToPdfListsAsync(rev.Id, risks, Riesgos, codigosMcExistentes: null);
        }

        // ── Distribución por nivel ────────────────────────────────────────────
        DistribucionNiveles = Riesgos
            .GroupBy(r => r.NivelLabel)
            .Select(g => new NivelDistRow(g.Key, g.First().NivelColor, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        // ── EAT Combinado + correlación cópula (misma muestra de ítems/riesgos) ──
        try
        {
            var incertItems = contratos
                .SelectMany(c => c.Items)
                .Where(i => !i.EsCerteza && i.EsPorComprometer)
                .Select(i => new CombinedEATItem {
                    Min      = i.MinKusd.HasValue      ? (double)i.MinKusd.Value      : (double)i.CostoUsd * 0.8,
                    Probable = i.ProbableKusd.HasValue  ? (double)i.ProbableKusd.Value : (double)i.CostoUsd,
                    Max      = i.MaxKusd.HasValue       ? (double)i.MaxKusd.Value      : (double)i.CostoUsd * 1.2
                }).ToList();

            if (incertItems.Any() || risks.Any())
            {
                var pdfSeed = Math.Abs(proyectoId.GetHashCode());
                var baseFijoCombinadoPdf = TotComp + TotCerteza;
                await Task.Run(() =>
                {
                    var coupled = _mc.RunCoupledSoloIncertidumbreYConRiesgos(
                        baseFijoCombinadoPdf, incertItems, risks, 10_000, pdfSeed);
                    EatCombinado                   = coupled.ConRiesgos;
                    EatCombinadoSoloIncertidumbre  = coupled.SoloIncertidumbre;

                    if (EatCombinado != null)
                    {
                        var histBins = EatCombinado.Histograma
                            .Select(b => new { desde = b.RangeMin, hasta = b.RangeMax, freq = b.Frequency })
                            .ToList();

                        ChartDataJson = JsonSerializer.Serialize(new {
                            totComp   = TotComp,
                            eatDet    = TotEAT,
                            capex     = TotCapex,
                            p10       = EatCombinado.P10,
                            p50       = EatCombinado.P50,
                            p80       = EatCombinado.P80,
                            p90       = EatCombinado.P90,
                            histBins
                        });
                    }

                    // Correlación — misma base que API Resumen (certeza + comprometido), variables «ambos»
                    var nDim = incertItems.Count + risks.Count;
                    if (nDim <= 0) return;

                    CorrelacionNItemsIncert = incertItems.Count;
                    CorrelacionNRiesgosMc   = risks.Count;

                    const int iterCorr = 10_000;
                    var baseFixedCorr = baseFijoCombinadoPdf;
                    var refIndep = EatCombinado ?? _mc.RunCombinedEATAnalysis(baseFixedCorr, incertItems, risks, iterCorr, pdfSeed + 11);
                    var corrSvc = new CorrelacionService();
                    const string varSrc = "ambos";

                    CorrelacionEscenarios = new List<CorrelacionEscenarioPdfRow>
                    {
                        MapCorrRow(corrSvc.RunCorrelacionAnalysis(baseFixedCorr, incertItems, risks,
                            null, varSrc, refIndep.P50, refIndep.P90, iterCorr, pdfSeed + 101),
                            "Sin correlación", "ρ = 0 (independiente)"),
                        MapCorrRow(corrSvc.RunCorrelacionAnalysis(baseFixedCorr, incertItems, risks,
                            UniformCorrMatrix(nDim, 0.2), varSrc, refIndep.P50, refIndep.P90, iterCorr, pdfSeed + 102),
                            "Baja", "ρ = 0,2"),
                        MapCorrRow(corrSvc.RunCorrelacionAnalysis(baseFixedCorr, incertItems, risks,
                            UniformCorrMatrix(nDim, 0.5), varSrc, refIndep.P50, refIndep.P90, iterCorr, pdfSeed + 103),
                            "Media", "ρ = 0,5"),
                        MapCorrRow(corrSvc.RunCorrelacionAnalysis(baseFixedCorr, incertItems, risks,
                            UniformCorrMatrix(nDim, 0.8), varSrc, refIndep.P50, refIndep.P90, iterCorr, pdfSeed + 104),
                            "Alta", "ρ = 0,8"),
                    };
                });
            }
        }
        catch { /* simulación opcional */ }

        EsVistaEjecutiva = string.Equals((string?)Request.Query["v"], "ejecutivo", StringComparison.OrdinalIgnoreCase);

        return Page();
    }

    // ── Helpers de formato ───────────────────────────────────────────────────
    public string FmtKusd(double v)    => $"{v:N2} USD$";
    public string FmtKusd(double? v)   => v.HasValue ? $"{v.Value:N2} USD$" : "—";
    public string FmtPct(double v)     => $"{v:N1}%";
    public string FmtN0(double v)      => $"{v:N0}";
    public string RagColor(double pctEAT) =>
        pctEAT > 115 ? "#b91c1c" : pctEAT > 100 ? "#d97706" : pctEAT > 95 ? "#0f766e" : "#15803d";
    public string RagLabel(double pctEAT) =>
        pctEAT > 115 ? "SOBRE PRESUPUESTO" : pctEAT > 100 ? "EN ALERTA" :
        pctEAT > 95  ? "DENTRO DE PRESUPUESTO" : "BAJO PRESUPUESTO";

    // ── Helpers Codelco ──────────────────────────────────────────────────────
    private static int CodelcoNivel(double pct) =>
        pct >= 75 ? 5 : pct >= 65 ? 4 : pct >= 50 ? 3 : pct >= 25 ? 2 : 1;

    private static string CodelcoLabel(int n) => n switch {
        5 => "Casi Seguro", 4 => "Muy Probable", 3 => "Probable",
        2 => "Poco Probable", _ => "Remoto"
    };

    private static string CodelcoHexColor(int n) => n switch {
        5 => "#ef4444", 4 => "#f97316", 3 => "#f59e0b", 2 => "#3b82f6", _ => "#94a3b8"
    };

    private static bool McTipoEsOportunidad(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) &&
        tipo.Trim().Equals("Oportunidad", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> McFlatInputToDictionary(string inputJson)
    {
        var raw = string.IsNullOrWhiteSpace(inputJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(inputJson) ?? new();
        var n = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
            n[kv.Key] = kv.Value;
        return n;
    }

    private static void RemovePdfRiskRowsByCodigo(List<CombinedEATRisk> risks, List<RiskRow> riesgos, string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        for (var i = riesgos.Count - 1; i >= 0; i--)
        {
            if (string.Equals((riesgos[i].Codigo ?? "").Trim(), codigo, StringComparison.OrdinalIgnoreCase))
            {
                riesgos.RemoveAt(i);
                risks.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Riesgos de revisión Taller para PDF. Si el código ya existe en MC, sustituye la fila MC por la de Taller.
    /// </summary>
    private async Task AppendTallerRiesgosToPdfListsAsync(
        Guid revisionId,
        List<CombinedEATRisk> risks,
        List<RiskRow> riesgos,
        HashSet<string>? codigosMcExistentes)
    {
        var riskRows = await _db.TallerRiesgos
            .Where(r => r.RevisionId == revisionId && r.ImpactoProableUsd != 0)
            .OrderBy(r => r.Orden)
            .Select(r => new {
                r.Id, r.CodigoRiesgo, r.Titulo, r.NotasCambio,
                r.ImpactoProableUsd, r.ImpactoMinUsd, r.ImpactoMaxUsd,
                r.EsOportunidad
            })
            .ToListAsync();

        if (riskRows.Count == 0) return;

        var probMap = new Dictionary<Guid, decimal>();
        try
        {
            var probRows = await _db.TallerRiesgos
                .Where(r => r.RevisionId == revisionId)
                .Select(r => new { r.Id, r.Probabilidad })
                .ToListAsync();
            foreach (var p in probRows)
                probMap[p.Id] = p.Probabilidad ?? 100m;
        }
        catch { /* ignore */ }

        foreach (var r in riskRows)
        {
            var code = (r.CodigoRiesgo ?? "").Trim();
            if (codigosMcExistentes != null && code.Length > 0 && codigosMcExistentes.Contains(code))
                RemovePdfRiskRowsByCodigo(risks, riesgos, code);

            var prob   = probMap.TryGetValue(r.Id, out var pv) ? (double)pv : 100.0;
            var impAbs = (double)r.ImpactoProableUsd;
            var minAbs = r.ImpactoMinUsd.HasValue ? (double)r.ImpactoMinUsd.Value : impAbs * 0.8;
            var maxAbs = r.ImpactoMaxUsd.HasValue ? (double)r.ImpactoMaxUsd.Value : impAbs * 1.2;
            var triMin  = r.EsOportunidad ? -maxAbs : minAbs;
            var triMode = r.EsOportunidad ? -impAbs : impAbs;
            var triMax  = r.EsOportunidad ? -minAbs : maxAbs;
            risks.Add(new CombinedEATRisk { Probability = prob / 100.0, Min = triMin, Mode = triMode, Max = triMax });
            var nivel = CodelcoNivel(prob);
            var ve = prob / 100.0 * triMode;
            riesgos.Add(new RiskRow(r.CodigoRiesgo ?? "", r.Titulo, r.NotasCambio ?? "",
                prob, triMin, triMode, triMax, ve,
                triMode >= 0,
                CodelcoLabel(nivel), CodelcoHexColor(nivel), nivel));
        }
    }

    private static List<RiskItem> ParseRisksFromInput(string inputJson)
    {
        var result = new List<RiskItem>();
        try
        {
            var dict = McFlatInputToDictionary(inputJson);
            int maxIdx = -1;
            foreach (var key in dict.Keys)
            {
                var m = System.Text.RegularExpressions.Regex.Match(key, @"Risks\[(\d+)\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                    maxIdx = Math.Max(maxIdx, idx);
            }
            for (int i = 0; i <= maxIdx; i++)
            {
                var likely = GetMcDouble(dict, $"Risks[{i}].MostLikelyImpact");
                if (likely == 0) continue;
                var minV = GetMcDouble(dict, $"Risks[{i}].MinImpact");
                var maxV = GetMcDouble(dict, $"Risks[{i}].MaxImpact");
                var rawTipo = dict.GetValueOrDefault($"Risks[{i}].Tipo") ?? "Amenaza";
                result.Add(new RiskItem {
                    Tipo             = McTipoEsOportunidad(rawTipo) ? "Oportunidad" : "Amenaza",
                    Code             = dict.GetValueOrDefault($"Risks[{i}].Code")        ?? "",
                    Description      = dict.GetValueOrDefault($"Risks[{i}].Description") ?? "",
                    Cause            = dict.GetValueOrDefault($"Risks[{i}].Cause")        ?? "",
                    ResponsePlan     = dict.GetValueOrDefault($"Risks[{i}].ResponsePlan") ?? "",
                    Probability      = GetMcDouble(dict, $"Risks[{i}].Probability", 50),
                    MinImpact        = minV == 0 ? likely * 0.8 : minV,
                    MostLikelyImpact = likely,
                    MaxImpact        = maxV == 0 ? likely * 1.2 : maxV,
                });
            }
        }
        catch { }
        return result;
    }

    private static CorrelacionEscenarioPdfRow MapCorrRow(CorrelacionResult r, string etiqueta, string rho)
        => new(etiqueta, rho, r.P50, r.P80, r.P90, r.DeltaP50, r.DeltaP90);

    private static double[,] UniformCorrMatrix(int n, double rho)
    {
        var m = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                m[i, j] = i == j ? 1.0 : rho;
        return m;
    }

    // Mismo criterio que TallerCostos (formato chileno/español en inputs MC).
    private static double GetMcDouble(Dictionary<string, string> d, string key, double def = 0)
    {
        if (!d.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return def;
        v = v.Trim();
        var ns  = System.Globalization.NumberStyles.Float;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        if (v.Contains(','))
        {
            var normalized = v.Replace(".", "").Replace(",", ".");
            if (double.TryParse(normalized, ns, inv, out var r1)) return r1;
        }
        else if (v.Contains('.'))
        {
            var parts = v.Split('.');
            if (parts[^1].Length == 3 && parts[^1].All(char.IsDigit))
            {
                var stripped = v.Replace(".", "");
                if (double.TryParse(stripped, ns, inv, out var r2)) return r2;
            }
            else
            {
                if (double.TryParse(v, ns, inv, out var r3)) return r3;
            }
        }
        else
        {
            if (double.TryParse(v, ns, inv, out var r4)) return r4;
        }
        return def;
    }
}
