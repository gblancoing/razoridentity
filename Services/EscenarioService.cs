namespace RazorIdentity.Services;

// ── DTOs Escenarios ─────────────────────────────────────────────────────────

public class EscenarioDetalle
{
    public string Nombre        { get; set; } = "";
    public string Descripcion   { get; set; } = "";
    public string Color         { get; set; } = "";
    public double EAT           { get; set; }
    public double EATItemsDelta { get; set; }   // delta respecto a probable
    public double EATRiesgosDelta { get; set; } // delta respecto al VE base
    public double VsBase        { get; set; }   // % vs escenario base
    public double PctCapex      { get; set; }   // EAT / CAPEX * 100
}

public class EscenariosResult
{
    public EscenarioDetalle Optimista { get; set; } = new();
    public EscenarioDetalle Base      { get; set; } = new();
    public EscenarioDetalle Pesimista { get; set; } = new();
}

/// <summary>
/// Análisis What-If — tres escenarios estructurados (Optimista / Base / Pesimista).
/// No requiere simulación Monte Carlo: usa valores determinísticos de la distribución.
/// </summary>
public class EscenarioService
{
    /// <param name="baseFixed">Certeza total (comprometido + ítems certeza).</param>
    /// <param name="capex">CAPEX total del proyecto para el cálculo de % vs CAPEX.</param>
    /// <param name="incertItems">Ítems de incertidumbre con Min/Probable/Max.</param>
    /// <param name="risks">Riesgos con Probability/Min/Mode/Max.</param>
    public EscenariosResult RunEscenariosAnalysis(
        double                 baseFixed,
        double                 capex,
        IList<CombinedEATItem> incertItems,
        IList<CombinedEATRisk> risks)
    {
        var amenazas = risks.Where(r => r.Mode >= 0).ToList();
        var opport   = risks.Where(r => r.Mode <  0).ToList();

        // ── Escenario Base ───────────────────────────────────────────────────
        // Ítems en su valor probable; riesgos a su VE (Prob × Mode).
        double baseItems = incertItems.Sum(i => i.Probable);
        double baseRisks = risks.Sum(r => r.Probability * r.Mode);
        double eatBase   = baseFixed + baseItems + baseRisks;

        // ── Escenario Optimista ──────────────────────────────────────────────
        // Ítems en su mínimo; amenazas al 50% de su probabilidad (menos riesgo);
        // oportunidades al 150% de su probabilidad (más ahorro).
        double optItems  = incertItems.Sum(i => i.Min);
        double optRisks  = amenazas.Sum(r => r.Probability * 0.50 * r.Mode)
                         + opport  .Sum(r => Math.Min(r.Probability * 1.50, 1.0) * r.Mode);
        double eatOpt    = baseFixed + optItems + optRisks;

        // ── Escenario Pesimista ──────────────────────────────────────────────
        // Ítems en su máximo; amenazas al 150% de su probabilidad (más impacto);
        // oportunidades al 50% de su probabilidad (menos ahorro).
        double pesItems  = incertItems.Sum(i => i.Max);
        double pesRisks  = amenazas.Sum(r => Math.Min(r.Probability * 1.50, 1.0) * r.Max)
                         + opport  .Sum(r => r.Probability * 0.50 * r.Min);
        double eatPes    = baseFixed + pesItems + pesRisks;

        var capexSafe = capex > 0 ? capex : 1;

        return new EscenariosResult
        {
            Optimista = new EscenarioDetalle
            {
                Nombre          = "Optimista",
                Descripcion     = "Ítems en mínimo · amenazas –50% prob · oportunidades +50% prob",
                Color           = "emerald",
                EAT             = eatOpt,
                EATItemsDelta   = optItems - baseItems,
                EATRiesgosDelta = optRisks - baseRisks,
                VsBase          = eatBase > 0 ? (eatOpt - eatBase) / eatBase * 100 : 0,
                PctCapex        = eatOpt / capexSafe * 100
            },
            Base = new EscenarioDetalle
            {
                Nombre          = "Base",
                Descripcion     = "Ítems en probable · riesgos a su probabilidad estimada",
                Color           = "blue",
                EAT             = eatBase,
                EATItemsDelta   = 0,
                EATRiesgosDelta = 0,
                VsBase          = 0,
                PctCapex        = eatBase / capexSafe * 100
            },
            Pesimista = new EscenarioDetalle
            {
                Nombre          = "Pesimista",
                Descripcion     = "Ítems en máximo · amenazas +50% prob · oportunidades –50% prob",
                Color           = "red",
                EAT             = eatPes,
                EATItemsDelta   = pesItems - baseItems,
                EATRiesgosDelta = pesRisks - baseRisks,
                VsBase          = eatBase > 0 ? (eatPes - eatBase) / eatBase * 100 : 0,
                PctCapex        = eatPes / capexSafe * 100
            }
        };
    }
}
