namespace RazorIdentity.Services;

// ── DTOs Tornado ────────────────────────────────────────────────────────────

public class TornadoBarra
{
    public string Nombre   { get; set; } = "";
    public string Codigo   { get; set; } = "";
    public bool   EsRiesgo { get; set; }
    public double EatBase  { get; set; }
    public double EatMin   { get; set; }
    public double EatMax   { get; set; }
    public double Swing    { get; set; }
}

public class TornadoResult
{
    public double              EatBase  { get; set; }
    public List<TornadoBarra>  Barras   { get; set; } = new();
    public int                 NItems   { get; set; }
    public int                 NRiesgos { get; set; }
}

/// <summary>
/// Análisis de Sensibilidad (Tornado Chart) — determinístico.
/// Para cada ítem/riesgo: fija el resto en su valor probable/VE y varía solo ese elemento
/// entre su mínimo y máximo. El swing = |EAT_max_i − EAT_min_i| define la importancia.
/// </summary>
public class TornadoService
{
    /// <param name="baseFixed">Certeza total (comprometido + ítems certeza).</param>
    /// <param name="incertItems">Ítems de incertidumbre con Min/Probable/Max.</param>
    /// <param name="risks">Riesgos con Probability/Min/Mode/Max.</param>
    /// <param name="itemNames">Nombres de los ítems (misma longitud que incertItems).</param>
    /// <param name="riskNames">Nombres de los riesgos (misma longitud que risks).</param>
    /// <param name="topN">Número máximo de barras a retornar.</param>
    public TornadoResult RunTornadoAnalysis(
        double                 baseFixed,
        IList<CombinedEATItem> incertItems,
        IList<CombinedEATRisk> risks,
        IList<string>          itemNames,
        IList<string>          riskNames,
        int                    topN = 10)
    {
        // EAT base determinístico = certeza + Σ probable + Σ VE riesgos
        double eatBase = baseFixed
            + incertItems.Sum(i => i.Probable)
            + risks.Sum(r => r.Probability * r.Mode);

        var barras = new List<TornadoBarra>(incertItems.Count + risks.Count);

        // Barras de ítems: variar entre Min y Max, resto en probable
        for (int i = 0; i < incertItems.Count; i++)
        {
            var item   = incertItems[i];
            var nombre = i < itemNames.Count ? itemNames[i] : $"Ítem {i + 1}";
            var eatMin = eatBase - item.Probable + item.Min;
            var eatMax = eatBase - item.Probable + item.Max;
            barras.Add(new TornadoBarra
            {
                Nombre  = nombre,
                Codigo  = "",
                EsRiesgo= false,
                EatBase = eatBase,
                EatMin  = eatMin,
                EatMax  = eatMax,
                Swing   = Math.Abs(eatMax - eatMin)
            });
        }

        // Barras de riesgos: variar impacto entre Min×Prob y Max×Prob
        for (int i = 0; i < risks.Count; i++)
        {
            var r      = risks[i];
            var nombre = i < riskNames.Count ? riskNames[i] : $"Riesgo {i + 1}";
            // VE base del riesgo i en eatBase = r.Probability * r.Mode
            var veBase = r.Probability * r.Mode;
            var veMin  = r.Probability * r.Min;
            var veMax  = r.Probability * r.Max;
            var eatMin = eatBase - veBase + veMin;
            var eatMax = eatBase - veBase + veMax;
            barras.Add(new TornadoBarra
            {
                Nombre  = nombre,
                Codigo  = "",
                EsRiesgo= true,
                EatBase = eatBase,
                EatMin  = eatMin,
                EatMax  = eatMax,
                Swing   = Math.Abs(eatMax - eatMin)
            });
        }

        barras = barras
            .Where(b => b.Swing > 0)
            .OrderByDescending(b => b.Swing)
            .Take(topN)
            .ToList();

        return new TornadoResult
        {
            EatBase  = eatBase,
            Barras   = barras,
            NItems   = incertItems.Count,
            NRiesgos = risks.Count
        };
    }
}
