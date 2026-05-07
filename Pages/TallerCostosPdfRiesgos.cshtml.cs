using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Montecarlo;
using RazorIdentity.Models.TallerCostos;
using System.Security.Claims;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class TallerCostosPdfRiesgosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public TallerCostosPdfRiesgosModel(ApplicationDbContext db) => _db = db;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private static readonly JsonSerializerOptions _ciOpts = new() { PropertyNameCaseInsensitive = true };

    public TallerProyecto Proyecto { get; private set; } = null!;

    public record RiskRow(
        string Codigo, string Descripcion, string Causa, string PlanRespuesta,
        double ProbPct, double Min, double Moda, double Max, double VE,
        bool EsAmenaza, string NivelLabel, string NivelColor);

    public List<RiskRow> Riesgos      { get; private set; } = new();
    public string        Fuente       { get; private set; } = "sin_datos";
    public string?       RevisionLabel { get; private set; }
    public DateTime?     McActualizado { get; private set; }

    public double VeAmenazas     => Riesgos.Where(r =>  r.EsAmenaza).Sum(r => r.VE);
    public double VeOportunidades => Riesgos.Where(r => !r.EsAmenaza).Sum(r => Math.Abs(r.VE));
    public double VeNeto         => Riesgos.Sum(r => r.VE);

    public async Task<IActionResult> OnGetAsync(Guid proyectoId)
    {
        var proyecto = await _db.TallerProyectos
            .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UserId == UserId);
        if (proyecto is null) return Forbid();
        Proyecto = proyecto;

        // ── Fuente primaria: MontecarloProject "tc:{proyectoId}" ─────────────
        var mcProj = await _db.MontecarloProjects
            .FirstOrDefaultAsync(p => p.UserId == UserId && p.ProjectName == $"tc:{proyectoId}");

        var mcItems = new List<RiskItem>();
        if (mcProj != null)
        {
            McActualizado = mcProj.UpdatedAt;
            try
            {
                var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(
                    mcProj.TabsJson ?? "{}", _ciOpts) ?? new();
                if (tabs.TryGetValue("risks", out var risksTab) &&
                    !string.IsNullOrEmpty(risksTab.InputJson) && risksTab.InputJson != "{}")
                    mcItems = ParseRisksFromInput(risksTab.InputJson);
            }
            catch { }
        }

        if (mcItems.Count > 0)
        {
            Fuente = "montecarlo";
            foreach (var r in mcItems)
                Riesgos.Add(MakeRow(r.Code, r.Description, r.Cause, r.ResponsePlan,
                    r.Probability, r.MinImpact, r.MostLikelyImpact, r.MaxImpact));
        }
        else
        {
            // ── Fuente fallback: TallerRevisionesRiesgos ─────────────────────
            var rev = await _db.TallerRevisionesRiesgos
                .Where(r => r.ProyectoId == proyectoId)
                .OrderByDescending(r => r.NumeroRevision)
                .FirstOrDefaultAsync();

            if (rev != null)
            {
                Fuente = "taller";
                RevisionLabel = $"Rev. {rev.NumeroRevision} — {rev.Descripcion} ({rev.FechaCreacion:dd/MM/yyyy})";

                var rows = await _db.TallerRiesgos
                    .Where(r => r.RevisionId == rev.Id && r.ImpactoProableUsd != 0)
                    .OrderBy(r => r.Orden)
                    .ToListAsync();

                foreach (var r in rows)
                {
                    var prob   = (double)(r.Probabilidad ?? 100m);
                    var impAbs = (double)r.ImpactoProableUsd;
                    var minAbs = r.ImpactoMinUsd.HasValue ? (double)r.ImpactoMinUsd.Value : impAbs * 0.8;
                    var maxAbs = r.ImpactoMaxUsd.HasValue ? (double)r.ImpactoMaxUsd.Value : impAbs * 1.2;
                    var moda   = r.EsOportunidad ? -impAbs : impAbs;
                    var minV   = r.EsOportunidad ? -maxAbs : minAbs;
                    var maxV   = r.EsOportunidad ? -minAbs : maxAbs;
                    Riesgos.Add(MakeRow(r.CodigoRiesgo, r.Titulo, "", r.NotasCambio, prob, minV, moda, maxV));
                }
            }
        }

        return Page();
    }

    private static RiskRow MakeRow(string cod, string desc, string causa, string plan,
        double prob, double min, double moda, double max)
    {
        var nivel = CodelcoNivel(prob);
        return new RiskRow(cod, desc, causa, plan, prob, min, moda, max,
            prob / 100.0 * moda, moda >= 0,
            CodelcoLabel(nivel), CodelcoHexColor(nivel));
    }

    public string FmtKusd(double v) => $"{v:N2} USD$";
    public string FmtPct(double v)  => $"{v:N1}%";

    private static int CodelcoNivel(double pct) =>
        pct >= 75 ? 5 : pct >= 65 ? 4 : pct >= 50 ? 3 : pct >= 25 ? 2 : 1;

    private static string CodelcoLabel(int n) => n switch {
        5 => "Casi Seguro", 4 => "Muy Probable", 3 => "Probable",
        2 => "Poco Probable", _ => "Remoto"
    };

    private static string CodelcoHexColor(int n) => n switch {
        5 => "#ef4444", 4 => "#f97316", 3 => "#f59e0b", 2 => "#3b82f6", _ => "#94a3b8"
    };

    private static List<RiskItem> ParseRisksFromInput(string inputJson)
    {
        var result = new List<RiskItem>();
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(inputJson) ?? new();
            int maxIdx = -1;
            foreach (var key in dict.Keys)
            {
                var m = System.Text.RegularExpressions.Regex.Match(key, @"Risks\[(\d+)\]");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                    maxIdx = Math.Max(maxIdx, idx);
            }
            for (int i = 0; i <= maxIdx; i++)
            {
                var likely = GetMcDouble(dict, $"Risks[{i}].MostLikelyImpact");
                if (likely == 0) continue;
                var minV = GetMcDouble(dict, $"Risks[{i}].MinImpact");
                var maxV = GetMcDouble(dict, $"Risks[{i}].MaxImpact");
                result.Add(new RiskItem {
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
