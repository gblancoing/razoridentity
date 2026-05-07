using RazorIdentity.Models.Montecarlo;

namespace RazorIdentity.Services;

/// <summary>
/// Convierte impactos del formulario de riesgos MC en vértices triangulares con signo para el costo.
/// Las oportunidades a menudo se ingresan como valores negativos (ahorro); la simulación EAT combinada exige moda &lt; 0.
/// </summary>
public static class McRiskFormTriangular
{
    public static void ToSignedVertices(RiskItem r, out double triMin, out double triMode, out double triMax)
    {
        var isOport = !string.IsNullOrWhiteSpace(r.Tipo) &&
                      r.Tipo.Trim().Equals("Oportunidad", StringComparison.OrdinalIgnoreCase);
        if (!isOport)
        {
            triMin  = r.MinImpact;
            triMode = r.MostLikelyImpact;
            triMax  = r.MaxImpact;
            return;
        }

        var modeAbs = Math.Abs(r.MostLikelyImpact);
        var minAbs  = r.MinImpact != 0 ? Math.Abs(r.MinImpact) : modeAbs * 0.8;
        var maxAbs  = r.MaxImpact != 0 ? Math.Abs(r.MaxImpact) : modeAbs * 1.2;
        triMin  = -maxAbs;
        triMode = -modeAbs;
        triMax  = -minAbs;
    }
}
