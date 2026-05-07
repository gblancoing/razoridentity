using RazorIdentity.Models.Montecarlo;

namespace RazorIdentity.Services;

// ── DTOs Correlación ─────────────────────────────────────────────────────────

public class CorrelacionResult
{
    public double P10       { get; set; }
    public double P50       { get; set; }
    public double P80       { get; set; }
    public double P90       { get; set; }
    public double Media     { get; set; }
    public double CVaR90    { get; set; }
    public double SpreadP10P90 { get; set; }
    public int    NSimulaciones { get; set; }
    public List<RiskMcHistogramBin> Histograma { get; set; } = new();
    // Comparación vs simulación sin correlación (EAT = baseFixed + comprometido)
    public double DeltaP50  { get; set; }
    public double DeltaP90  { get; set; }
}

/// <summary>
/// Simulación Monte Carlo con Cópula Gaussiana (Cholesky) para riesgos correlacionados.
/// Permite modelar situaciones donde la ocurrencia de un riesgo está relacionada
/// con la ocurrencia de otros (por ejemplo, dos riesgos geopolíticos).
/// </summary>
public class CorrelacionService
{
    /// <param name="baseFixed">Certeza total + comprometido.</param>
    /// <param name="incertItems">Ítems de incertidumbre (triangular, independientes).</param>
    /// <param name="risks">Riesgos (Bernoulli correlacionado × triangular).</param>
    /// <param name="corrMatrix">
    ///   Matriz de correlación n×n.
    ///   - varSource="riesgos"   → n = nRisks  (sólo riesgos correlacionados; comportamiento original)
    ///   - varSource="contratos" → n = nItems   (ítems correlacionados, riesgos independientes)
    ///   - varSource="ambos"     → n = nItems + nRisks (todos correlacionados juntos)
    ///   null → usa identidad (equivalente a simulación independiente estándar).
    /// </param>
    /// <param name="varSource">Qué variables correlacionar: "riesgos" | "contratos" | "ambos".</param>
    /// <param name="refP50">P50 de la simulación independiente para calcular deltas.</param>
    /// <param name="refP90">P90 de la simulación independiente.</param>
    /// <param name="simulations">Número de iteraciones.</param>
    /// <param name="randomSeed">Semilla opcional (p. ej. PDF reproducible).</param>
    public CorrelacionResult RunCorrelacionAnalysis(
        double                 baseFixed,
        IList<CombinedEATItem> incertItems,
        IList<CombinedEATRisk> risks,
        double[,]?             corrMatrix = null,
        string                 varSource  = "riesgos",
        double                 refP50 = 0,
        double                 refP90 = 0,
        int                    simulations = 10_000,
        int?                   randomSeed = null)
    {
        int nItems = incertItems.Count;
        int nRisks = risks.Count;

        // Dimensión de la Cópula según la fuente seleccionada
        int nCorr = varSource switch {
            "contratos" => nItems,
            "ambos"     => nItems + nRisks,
            _           => nRisks   // "riesgos" (default)
        };

        // Descomposición de Cholesky de la matriz de correlación
        double[,] L = nCorr > 0 && corrMatrix != null
            ? Cholesky(corrMatrix, nCorr)
            : IdentityMatrix(Math.Max(nCorr, 1));

        var rng    = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        var totals = new double[simulations];

        for (int sim = 0; sim < simulations; sim++)
        {
            double total = baseFixed;

            // Generar nCorr normales estándar correlacionadas vía Cópula Gaussiana (si aplica)
            double[]? w = null;
            if (nCorr > 0)
            {
                var z = new double[nCorr];
                for (int k = 0; k < nCorr; k++) z[k] = SampleNormal(rng);
                w = new double[nCorr];
                for (int i = 0; i < nCorr; i++)
                {
                    double s = 0;
                    for (int j = 0; j <= i; j++) s += L[i, j] * z[j];
                    w[i] = s;
                }
            }

            // ── Ítems de incertidumbre ────────────────────────────────────────
            if (varSource == "contratos" || varSource == "ambos")
            {
                // Correlacionados: usar Φ(w[i]) como cuantil de la CDF triangular
                for (int i = 0; i < nItems; i++)
                {
                    double u = w != null ? NormalCDF(w[i]) : rng.NextDouble();
                    total += InverseTriangularCDF(u, incertItems[i].Min, incertItems[i].Probable, incertItems[i].Max);
                }
            }
            else
            {
                // Independientes (modo "riesgos")
                foreach (var item in incertItems)
                    total += SampleTriangular(rng, item.Min, item.Probable, item.Max);
            }

            // ── Riesgos ───────────────────────────────────────────────────────
            if (nRisks > 0)
            {
                if (varSource == "riesgos" || varSource == "ambos")
                {
                    // Correlacionados: Bernoulli via Φ(w[idx]) < Prob
                    int offset = varSource == "ambos" ? nItems : 0;
                    for (int i = 0; i < nRisks; i++)
                    {
                        double wi = w != null ? w[offset + i] : SampleNormal(rng);
                        if (NormalCDF(wi) < risks[i].Probability)
                            total += SampleTriangular(rng, risks[i].Min, risks[i].Mode, risks[i].Max);
                    }
                }
                else
                {
                    // Independientes (modo "contratos")
                    foreach (var r in risks)
                        if (rng.NextDouble() < r.Probability)
                            total += SampleTriangular(rng, r.Min, r.Mode, r.Max);
                }
            }

            totals[sim] = total;
        }

        Array.Sort(totals);
        int    n    = simulations;
        int    c90  = (int)(n * 0.90);
        double p50  = Percentile(totals, 50);
        double p90  = Percentile(totals, 90);
        double cvar = c90 < n ? totals.Skip(c90).Average() : totals[n - 1];

        return new CorrelacionResult
        {
            P10          = Percentile(totals, 10),
            P50          = p50,
            P80          = Percentile(totals, 80),
            P90          = p90,
            Media        = totals.Sum() / n,
            CVaR90       = cvar,
            SpreadP10P90 = p90 - Percentile(totals, 10),
            NSimulaciones= n,
            Histograma   = BuildHistogram(totals, 30),
            DeltaP50     = refP50 > 0 ? (p50 - refP50) / refP50 * 100 : 0,
            DeltaP90     = refP90 > 0 ? (p90 - refP90) / refP90 * 100 : 0
        };
    }

    // ── Cholesky (lower triangular L tal que L·Lᵀ = A) ─────────────────────
    private static double[,] Cholesky(double[,] A, int n)
    {
        var L = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double s = A[i, j];
                for (int k = 0; k < j; k++) s -= L[i, k] * L[j, k];
                if (i == j)
                    L[i, j] = Math.Sqrt(Math.Max(s, 1e-12));
                else
                    L[i, j] = L[j, j] > 1e-12 ? s / L[j, j] : 0;
            }
        }
        return L;
    }

    private static double[,] IdentityMatrix(int n)
    {
        var m = new double[n, n];
        for (int i = 0; i < n; i++) m[i, i] = 1.0;
        return m;
    }

    // ── Distribución Triangular (CDF inversa) ───────────────────────────────
    private static double SampleTriangular(Random rng, double min, double mode, double max)
        => InverseTriangularCDF(rng.NextDouble(), min, mode, max);

    /// <summary>Inversa analítica de la CDF triangular para un cuantil u ∈ (0,1) dado.</summary>
    private static double InverseTriangularCDF(double u, double min, double mode, double max)
    {
        double a  = Math.Min(min, max);
        double b  = Math.Max(min, max);
        if (Math.Abs(b - a) < 1e-10) return mode;
        double c  = Math.Clamp(mode, a, b);
        u = Math.Clamp(u, 1e-10, 1 - 1e-10);
        double fc = (c - a) / (b - a);
        return u < fc
            ? a + Math.Sqrt(u  * (b - a) * (c - a))
            : b - Math.Sqrt((1 - u) * (b - a) * (b - c));
    }

    // ── Normal estándar vía Box-Muller ───────────────────────────────────────
    private static double SampleNormal(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // ── CDF de la normal estándar (Abramowitz & Stegun 7.1.26) ──────────────
    private static double NormalCDF(double x)
    {
        const double a1 =  0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 =  1.061405429, p  = 0.3275911;
        double sign = x < 0 ? -1.0 : 1.0;
        x = Math.Abs(x);
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t
                       * Math.Exp(-x * x);
        return 0.5 * (1.0 + sign * y);
    }

    // ── Percentil sobre array ordenado ──────────────────────────────────────
    private static double Percentile(double[] sorted, int pct)
    {
        int    n   = sorted.Length;
        double idx = (pct / 100.0) * (n - 1);
        int    lo  = (int)idx;
        int    hi  = Math.Min(lo + 1, n - 1);
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }

    // ── Histograma de frecuencia relativa ───────────────────────────────────
    private static List<RiskMcHistogramBin> BuildHistogram(double[] sorted, int bins)
    {
        int    n   = sorted.Length;
        double min = sorted[0];
        double max = sorted[n - 1];
        if (Math.Abs(max - min) < 1e-10)
            return new List<RiskMcHistogramBin> { new() { RangeMin = min, RangeMax = min, Frequency = 1.0 } };
        double width  = (max - min) / bins;
        var    result = new List<RiskMcHistogramBin>(bins);
        int    pos    = 0;
        for (int b = 0; b < bins; b++)
        {
            double lo     = min + b * width;
            double hi     = lo + width;
            bool   isLast = b == bins - 1;
            int    start  = pos;
            while (pos < n && (isLast ? sorted[pos] <= hi : sorted[pos] < hi)) pos++;
            result.Add(new RiskMcHistogramBin { RangeMin = lo, RangeMax = hi, Frequency = (double)(pos - start) / n });
        }
        return result;
    }
}
