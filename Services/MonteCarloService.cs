using RazorIdentity.Models.Montecarlo;

namespace RazorIdentity.Services;

// ── DTOs para análisis combinado EAT (Incertidumbre + Riesgos) ──────────────

public class CombinedEATItem
{
    public double Min      { get; set; }
    public double Probable { get; set; }
    public double Max      { get; set; }
}

public class CombinedEATRisk
{
    public double Probability { get; set; }  // 0–1
    public double Min         { get; set; }
    public double Mode        { get; set; }
    public double Max         { get; set; }
}

/// <summary>Fila de tabla de percentiles (estilo informes tipo @RISK).</summary>
public class McDistribPercentile
{
    public double P { get; set; }
    public double V { get; set; }
}

public class CombinedEATResult
{
    public double EatBase        { get; set; }   // certeza (sin comprometido ni incert base)
    public double Media          { get; set; }
    public double P10            { get; set; }
    public double P15            { get; set; }
    public double P50            { get; set; }
    public double P80            { get; set; }
    public double P90            { get; set; }
    public double Min            { get; set; }
    public double Max            { get; set; }
    public double SpreadP10P90   { get; set; }
    public int    NSimulaciones  { get; set; }
    public int    NItems         { get; set; }
    public int    NRiesgos       { get; set; }
    /// <summary>Desviación estándar (poblacional sobre las iteraciones).</summary>
    public double DesviacionEstandar { get; set; }
    /// <summary>Asimetría (momento normalizado, mismo criterio que RunRiskAnalysis).</summary>
    public double Skewness           { get; set; }
    /// <summary>Curtosis en exceso (Fisher): E[(z^4)] − 3.</summary>
    public double KurtosisExceso     { get; set; }
    /// <summary>Moda aproximada: centro del bin de mayor frecuencia del histograma.</summary>
    public double ModaEstimada       { get; set; }
    public double P1  { get; set; }
    public double P5  { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    /// <summary>Semiancho del intervalo central P5–P95; en informes se muestra como ± respecto a la media (referencia tipo IC 90%).</summary>
    public double Ic90Semirango      { get; set; }
    public List<McDistribPercentile> PercentilesTabla { get; set; } = new();
    // Desglose amenazas vs oportunidades
    public double ContingenciaAmenazasP10   { get; set; }
    public double ContingenciaAmenazasP50   { get; set; }
    public double ContingenciaAmenazasP80   { get; set; }
    public double ContingenciaAmenazasP90   { get; set; }
    public double ContingenciaAmenazasMedia { get; set; }
    public double ImpactoOportunidadesP50   { get; set; }  // ≤ 0 (reducción de costo)
    public double ImpactoOportunidadesP80   { get; set; }  // ≤ 0
    // CVaR — Expected Shortfall (análisis de cola)
    public double CVaR90   { get; set; }   // Media del peor 10% de simulaciones
    public double CVaR80   { get; set; }   // Media del peor 20%
    public double ExcessP90 { get; set; } // CVaR90 − P90 (cuánto excede la cola)
    public List<RiskMcHistogramBin> Histograma { get; set; } = new();
    /// <summary>Histograma del impacto neto de riesgos por iteración: EAT(con riesgos) − EAT(solo ítems), misma corrida acoplada.</summary>
    public List<RiskMcHistogramBin> HistogramaImpactoRiesgosNeto { get; set; } = new();
    /// <summary>Percentiles de la distribución del incremento neto por riesgos (USD).</summary>
    public double DeltaRiesgosP10 { get; set; }
    public double DeltaRiesgosP50 { get; set; }
    public double DeltaRiesgosP80 { get; set; }
    public double DeltaRiesgosP90 { get; set; }
}

/// <summary>Resultado de una sola corrida MC: misma semilla y mismos sorteos de ítems para solo-incertidumbre y para ítems+riesgos.</summary>
public sealed record CoupledEATSimulationResult(CombinedEATResult SoloIncertidumbre, CombinedEATResult ConRiesgos);

public class MonteCarloItem
{
    public double Minimo   { get; set; }
    public double Probable { get; set; }
    public double Maximo   { get; set; }
}

public class MonteCarloResult
{
    public double Media       { get; set; }
    public double Desviacion  { get; set; }
    public double Percentil10 { get; set; }
    public double Percentil50 { get; set; }
    public double Percentil90 { get; set; }
    public int    Iteraciones { get; set; }
}

/// <summary>
/// Simulación Monte Carlo local usando distribución triangular inversa (CDF inversa).
/// No requiere servicio externo.
/// </summary>
public class MonteCarloService
{
    /// <summary>
    /// Ejecuta la simulación. Para cada iteración genera un valor aleatorio por ítem
    /// usando la distribución triangular (a=Minimo, c=Probable, b=Maximo), los suma
    /// y acumula los totales. Retorna estadísticas del total acumulado.
    /// </summary>
    public MonteCarloResult RunSimulation(IList<MonteCarloItem> items, int iteraciones = 10_000)
    {
        if (items is null || items.Count == 0)
            return new MonteCarloResult { Iteraciones = iteraciones };

        var rng    = new Random();
        var totals = new double[iteraciones];

        for (int iter = 0; iter < iteraciones; iter++)
        {
            double sum = 0;
            foreach (var item in items)
            {
                double a = item.Minimo;
                double c = item.Probable;
                double b = item.Maximo;

                // Caso degenerado: todos iguales o rango inválido
                if (b <= a) { sum += c; continue; }

                // Asegurar que la moda esté en [a, b]
                c = Math.Clamp(c, a, b);

                double u  = rng.NextDouble();
                double fc = (c - a) / (b - a);   // punto de inflexión de la CDF

                // CDF inversa triangular
                double val = u < fc
                    ? a + Math.Sqrt(u * (b - a) * (c - a))
                    : b - Math.Sqrt((1.0 - u) * (b - a) * (b - c));

                sum += val;
            }
            totals[iter] = sum;
        }

        Array.Sort(totals);

        double mean     = totals.Sum() / iteraciones;
        double variance = totals.Sum(t => (t - mean) * (t - mean)) / iteraciones;
        double stdDev   = Math.Sqrt(variance);

        return new MonteCarloResult
        {
            Media       = mean,
            Desviacion  = stdDev,
            Percentil10 = Percentile(totals, 10),
            Percentil50 = Percentile(totals, 50),
            Percentil90 = Percentile(totals, 90),
            Iteraciones = iteraciones
        };
    }

    private static double Percentile(double[] sorted, double pct)
    {
        double idx = (pct / 100.0) * (sorted.Length - 1);
        int    lo  = (int)idx;
        int    hi  = lo + 1;
        if (hi >= sorted.Length) return sorted[lo];
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }

    // ── Simulación de Riesgos: Bernoulli × Triangular ────────────────────────
    // Replica exactamente la fórmula Excel: @RiskBernoulli(p) × @RiskTriang(min,moda,max)
    // Probability ya debe venir en escala 0-1
    public RiskMcResponse RunRiskAnalysis(IList<RiskItem> risks, int simulations = 10_000)
    {
        risks ??= Array.Empty<RiskItem>();
        var rng         = new Random();
        var threatRisks = risks.Where(r => r.Tipo != "Oportunidad").ToList();
        var oppRisks    = risks.Where(r => r.Tipo == "Oportunidad").ToList();

        // Una sola pasada por iteración: mismos Bernoulli/triangulares para amenazas, oportunidades y neto.
        // (Antes: tres corridas independientes; la resta visual P80(amen)−P80(opp) no era comparable al P80 del neto.)
        var totals  = new double[simulations];
        var tTotals = threatRisks.Count > 0 ? new double[simulations] : null;
        var oTotals = oppRisks.Count > 0 ? new double[simulations] : null;
        int anyCount = 0;

        for (int i = 0; i < simulations; i++)
        {
            double threat = 0, opp = 0;
            bool   anyHit = false;

            foreach (var r in risks)
            {
                if (rng.NextDouble() >= r.Probability) continue;
                anyHit = true;

                double sample = SampleTriangular(
                    rng,
                    Math.Abs(r.MinImpact),
                    Math.Abs(r.MostLikelyImpact),
                    Math.Abs(r.MaxImpact));

                if (r.Tipo == "Oportunidad")
                    opp += sample;
                else
                    threat += sample;
            }

            totals[i] = threat - opp;
            if (tTotals is not null) tTotals[i] = threat;
            if (oTotals is not null) oTotals[i] = opp;
            if (anyHit) anyCount++;
        }

        Array.Sort(totals);

        int    n        = simulations;
        double mean     = totals.Sum() / n;
        double variance = totals.Sum(t => (t - mean) * (t - mean)) / n;
        double stdDev   = Math.Sqrt(variance);

        double skewness = 0, kurtosis = 0;
        if (stdDev > 1e-10)
        {
            skewness = totals.Sum(t => Math.Pow((t - mean) / stdDev, 3)) / n;
            kurtosis = totals.Sum(t => Math.Pow((t - mean) / stdDev, 4)) / n - 3.0;
        }

        double? threatP50 = null, threatP80 = null, threatP90 = null;
        double? oppP50   = null, oppP80   = null;
        if (tTotals is not null)
        {
            Array.Sort(tTotals);
            threatP50 = Percentile(tTotals, 50);
            threatP80 = Percentile(tTotals, 80);
            threatP90 = Percentile(tTotals, 90);
        }
        if (oTotals is not null)
        {
            Array.Sort(oTotals);
            oppP50 = Percentile(oTotals, 50);
            oppP80 = Percentile(oTotals, 80);
        }

        return new RiskMcResponse
        {
            Statistics = new RiskMcStatistics
            {
                Mean     = mean,
                StdDev   = stdDev,
                Min      = totals[0],
                Max      = totals[n - 1],
                P5       = Percentile(totals, 5),
                P10      = Percentile(totals, 10),
                P25      = Percentile(totals, 25),
                P50      = Percentile(totals, 50),
                P75      = Percentile(totals, 75),
                P80      = Percentile(totals, 80),
                P90      = Percentile(totals, 90),
                P95      = Percentile(totals, 95),
                Skewness = skewness,
                Kurtosis = kurtosis
            },
            Histogram         = BuildHistogram(totals, 30),
            SimulationType    = "RiskAnalysis",
            AdditionalMetrics = new RiskMcAdditionalMetrics
            {
                ProbabilityOfAnyRisk = (double)anyCount / n,
                ThreatP50            = threatP50,
                ThreatP80            = threatP80,
                ThreatP90            = threatP90,
                OpportunityP50       = oppP50,
                OpportunityP80       = oppP80,
                ThreatCount          = threatRisks.Count,
                OpportunityCount     = oppRisks.Count
            }
        };
    }

    // Ejecuta la simulación Bernoulli × Triangular sobre una lista de riesgos.
    // signByTipo=true  → Amenazas suman, Oportunidades restan (simulación combinada).
    // signByTipo=false → todos los impactos se suman en valor absoluto (sub-simulaciones).
    private double[] SimulateRiskTotals(Random rng, IList<RiskItem> risks, int simulations, out int anyCount, bool signByTipo = false)
    {
        var totals   = new double[simulations];
        int hitCount = 0;

        for (int i = 0; i < simulations; i++)
        {
            double total  = 0;
            bool   anyHit = false;

            foreach (var r in risks)
            {
                if (rng.NextDouble() >= r.Probability) continue;
                anyHit = true;

                // Usar siempre valores absolutos para evitar inconsistencias de entrada
                double sample = SampleTriangular(
                    rng,
                    Math.Abs(r.MinImpact),
                    Math.Abs(r.MostLikelyImpact),
                    Math.Abs(r.MaxImpact));

                // En simulación combinada las oportunidades reducen el total
                total += (signByTipo && r.Tipo == "Oportunidad") ? -sample : sample;
            }

            totals[i] = total;
            if (anyHit) hitCount++;
        }

        anyCount = hitCount;
        return totals;
    }

    private static double SampleTriangular(Random rng, double min, double mode, double max)
    {
        // Normalizar: permite oportunidades (min < 0) y evita parámetros invertidos
        double a = Math.Min(min, max);
        double b = Math.Max(min, max);
        if (Math.Abs(b - a) < 1e-10) return mode;

        double c = Math.Clamp(mode, a, b);
        double u  = rng.NextDouble();
        double fc = (c - a) / (b - a);

        return u < fc
            ? a + Math.Sqrt(u * (b - a) * (c - a))
            : b - Math.Sqrt((1.0 - u) * (b - a) * (b - c));
    }

    // ── Análisis combinado EAT: Incertidumbre + Riesgos ────────────────────────
    // Formula: EAT_i = baseFixed + Σ Triangular(incert items) + Σ Bernoulli(p) × Triangular(risks)
    // - baseFixed   = certeza_total (ítems de certeza, sin comprometido)
    // - incertItems = ítems con incertidumbre triangular (min/probable/max en USD)
    // - risks       = riesgos con probabilidad Bernoulli × impacto triangular
    public CombinedEATResult RunCombinedEATAnalysis(
        double                   baseFixed,
        IList<CombinedEATItem>   incertItems,
        IList<CombinedEATRisk>   risks,
        int                      simulations = 10_000,
        int?                     seed        = null)
    {
        // Fix 1: separar amenazas (Mode ≥ 0) de oportunidades (Mode < 0)
        var amenazas      = risks.Where(r => r.Mode >= 0).ToList();
        var oportunidades = risks.Where(r => r.Mode <  0).ToList();

        var rng               = seed.HasValue ? new Random(seed.Value) : new Random();
        var totals            = new double[simulations];
        var amenazasTotals    = new double[simulations];
        var oportTotals       = new double[simulations];

        for (int i = 0; i < simulations; i++)
        {
            double total     = baseFixed;
            double amenSum   = 0;
            double oportSum  = 0;

            // Término 2: Δ Incertidumbre — Triangular(min, probable, max) por ítem
            foreach (var item in incertItems)
                total += SampleTriangular(rng, item.Min, item.Probable, item.Max);

            // Término 3a: Δ Amenazas — Bernoulli(p) × Triangular(min, mode, max)
            foreach (var r in amenazas)
            {
                if (rng.NextDouble() < r.Probability)
                {
                    double impact = SampleTriangular(rng, r.Min, r.Mode, r.Max);
                    total   += impact;
                    amenSum += impact;
                }
            }

            // Término 3b: Δ Oportunidades — Bernoulli(p) × Triangular(min, mode, max) — impacto negativo
            foreach (var r in oportunidades)
            {
                if (rng.NextDouble() < r.Probability)
                {
                    double impact = SampleTriangular(rng, r.Min, r.Mode, r.Max);
                    total    += impact;
                    oportSum += impact;
                }
            }

            totals[i]         = total;
            amenazasTotals[i] = amenSum;
            oportTotals[i]    = oportSum;
        }

        Array.Sort(totals);
        Array.Sort(amenazasTotals);
        Array.Sort(oportTotals);

        return PackCombinedEatResult(totals, baseFixed, amenazasTotals, oportTotals, incertItems.Count, risks.Count);
    }

    /// <summary>
    /// Una pasada MC: en cada iteración se calcula EAT solo con triángulos de ítems y, con los mismos sorteos de ítems,
    /// EAT completo añadiendo riesgos. Evita comparar «suma de percentiles por contrato» con una simulación conjunta
    /// y alinea percentiles entre ambos bloques del resumen.
    /// </summary>
    public CoupledEATSimulationResult RunCoupledSoloIncertidumbreYConRiesgos(
        double baseFixed,
        IList<CombinedEATItem> incertItems,
        IList<CombinedEATRisk> risks,
        int simulations = 10_000,
        int? seed = null)
    {
        var amenazas      = risks.Where(r => r.Mode >= 0).ToList();
        var oportunidades = risks.Where(r => r.Mode < 0).ToList();
        var rng           = seed.HasValue ? new Random(seed.Value) : new Random();

        var totalsSolo = new double[simulations];
        var totalsFull = new double[simulations];
        var amenFull   = new double[simulations];
        var oportFull  = new double[simulations];
        var amenZero   = new double[simulations];
        var oportZero  = new double[simulations];

        for (int i = 0; i < simulations; i++)
        {
            double itemsSum = 0;
            foreach (var item in incertItems)
                itemsSum += SampleTriangular(rng, item.Min, item.Probable, item.Max);

            double solo = baseFixed + itemsSum;
            totalsSolo[i] = solo;

            double total   = solo;
            double amenSum = 0, oportSum = 0;
            foreach (var r in amenazas)
            {
                if (rng.NextDouble() < r.Probability)
                {
                    double impact = SampleTriangular(rng, r.Min, r.Mode, r.Max);
                    total   += impact;
                    amenSum += impact;
                }
            }

            foreach (var r in oportunidades)
            {
                if (rng.NextDouble() < r.Probability)
                {
                    double impact = SampleTriangular(rng, r.Min, r.Mode, r.Max);
                    total    += impact;
                    oportSum += impact;
                }
            }

            totalsFull[i] = total;
            amenFull[i]   = amenSum;
            oportFull[i]  = oportSum;
        }

        var deltaRiesgos = new double[simulations];
        for (int i = 0; i < simulations; i++)
            deltaRiesgos[i] = totalsFull[i] - totalsSolo[i];

        Array.Sort(totalsSolo);
        Array.Sort(totalsFull);
        Array.Sort(amenFull);
        Array.Sort(oportFull);

        var resSolo = PackCombinedEatResult(totalsSolo, baseFixed, amenZero, oportZero, incertItems.Count, 0);
        var resFull = PackCombinedEatResult(totalsFull, baseFixed, amenFull, oportFull, incertItems.Count, risks.Count);

        if (risks.Count > 0)
        {
            Array.Sort(deltaRiesgos);
            resFull.HistogramaImpactoRiesgosNeto = BuildHistogram(deltaRiesgos, 30);
            resFull.DeltaRiesgosP10              = Percentile(deltaRiesgos, 10);
            resFull.DeltaRiesgosP50              = Percentile(deltaRiesgos, 50);
            resFull.DeltaRiesgosP80              = Percentile(deltaRiesgos, 80);
            resFull.DeltaRiesgosP90              = Percentile(deltaRiesgos, 90);
        }

        return new CoupledEATSimulationResult(resSolo, resFull);
    }

    private static CombinedEATResult PackCombinedEatResult(
        double[] sortedTotals,
        double baseFixed,
        double[] sortedAmenazas,
        double[] sortedOport,
        int nItems,
        int nRiesgos)
    {
        int    n       = sortedTotals.Length;
        double mean    = sortedTotals.Sum() / n;
        double varPop  = sortedTotals.Sum(t => (t - mean) * (t - mean)) / n;
        double std     = Math.Sqrt(varPop);
        double skew    = 0;
        double kurtExc = 0;
        if (std > 1e-10)
        {
            skew    = sortedTotals.Sum(t => Math.Pow((t - mean) / std, 3)) / n;
            kurtExc = sortedTotals.Sum(t => Math.Pow((t - mean) / std, 4)) / n - 3.0;
        }

        var histogram = BuildHistogram(sortedTotals, 30);
        double moda   = ModaFromHistogram(histogram);
        double p1     = Percentile(sortedTotals, 1);
        double p5     = Percentile(sortedTotals, 5);
        double p95    = Percentile(sortedTotals, 95);
        double p99    = Percentile(sortedTotals, 99);

        int    corte90 = (int)(n * 0.90);
        int    corte80 = (int)(n * 0.80);
        double cvar90  = corte90 < n ? sortedTotals.Skip(corte90).Average() : sortedTotals[n - 1];
        double cvar80  = corte80 < n ? sortedTotals.Skip(corte80).Average() : sortedTotals[n - 1];
        double p90val  = Percentile(sortedTotals, 90);

        return new CombinedEATResult
        {
            EatBase                   = baseFixed,
            Media                     = mean,
            P10                       = Percentile(sortedTotals, 10),
            P15                       = Percentile(sortedTotals, 15),
            P50                       = Percentile(sortedTotals, 50),
            P80                       = Percentile(sortedTotals, 80),
            P90                       = p90val,
            Min                       = sortedTotals[0],
            Max                       = sortedTotals[n - 1],
            SpreadP10P90              = p90val - Percentile(sortedTotals, 10),
            NSimulaciones             = n,
            NItems                    = nItems,
            NRiesgos                  = nRiesgos,
            DesviacionEstandar        = std,
            Skewness                  = skew,
            KurtosisExceso            = kurtExc,
            ModaEstimada              = moda,
            P1                        = p1,
            P5                        = p5,
            P95                       = p95,
            P99                       = p99,
            Ic90Semirango             = (p95 - p5) * 0.5,
            PercentilesTabla          = BuildPercentilesTabla(sortedTotals),
            ContingenciaAmenazasP10   = Percentile(sortedAmenazas, 10),
            ContingenciaAmenazasP50   = Percentile(sortedAmenazas, 50),
            ContingenciaAmenazasP80   = Percentile(sortedAmenazas, 80),
            ContingenciaAmenazasP90   = Percentile(sortedAmenazas, 90),
            ContingenciaAmenazasMedia = sortedAmenazas.Sum() / n,
            ImpactoOportunidadesP50   = Percentile(sortedOport, 50),
            ImpactoOportunidadesP80   = Percentile(sortedOport, 80),
            CVaR90                    = cvar90,
            CVaR80                    = cvar80,
            ExcessP90                 = cvar90 - p90val,
            Histograma                = histogram
        };
    }

    private static double ModaFromHistogram(List<RiskMcHistogramBin> hist)
    {
        if (hist is not { Count: > 0 }) return 0;
        var best = hist.OrderByDescending(b => b.Frequency).First();
        return (best.RangeMin + best.RangeMax) * 0.5;
    }

    private static readonly int[] PercentileGrid =
        { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 99 };

    private static List<McDistribPercentile> BuildPercentilesTabla(double[] sorted)
    {
        var list = new List<McDistribPercentile>(PercentileGrid.Length);
        foreach (var p in PercentileGrid)
            list.Add(new McDistribPercentile { P = p, V = Percentile(sorted, p) });
        return list;
    }

    private static List<RiskMcHistogramBin> BuildHistogram(double[] sorted, int bins)
    {
        int    n   = sorted.Length;
        double min = sorted[0];
        double max = sorted[n - 1];

        if (Math.Abs(max - min) < 1e-10)
            return new List<RiskMcHistogramBin>
            {
                new() { RangeMin = min, RangeMax = min, Frequency = 1.0 }
            };

        double width  = (max - min) / bins;
        var    result = new List<RiskMcHistogramBin>(bins);
        int    pos    = 0;

        for (int b = 0; b < bins; b++)
        {
            double lo  = min + b * width;
            double hi  = lo + width;
            bool isLast = b == bins - 1;
            int   start = pos;

            while (pos < n && (isLast ? sorted[pos] <= hi : sorted[pos] < hi)) pos++;

            result.Add(new RiskMcHistogramBin
            {
                RangeMin  = lo,
                RangeMax  = hi,
                Frequency = (double)(pos - start) / n
            });
        }

        return result;
    }
}
