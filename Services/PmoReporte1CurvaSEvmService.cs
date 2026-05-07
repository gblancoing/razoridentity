using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.PmoFinance;

namespace RazorIdentity.Services;

/// <summary>Curva S (parciales acumulados), indicadores EVM, tabla por categoría VP, cascadas e IEAC/ECD (lógica alineada al módulo PHP/React de referencia).</summary>
public sealed class PmoReporte1CurvaSEvmService
{
    private readonly ApplicationDbContext _db;

    public PmoReporte1CurvaSEvmService(ApplicationDbContext db) => _db = db;

    public async Task<Reporte1CurvaSEvmPackDto> BuildPackAsync(
        int proyectoId,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        DateOnly fechaSeguimiento,
        CancellationToken ct)
    {
        var bacTotal = await _db.PmoApiParciales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId)
            .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;

        var realRows = await QueryParcialRows<PmoRealParcial>(proyectoId, fechaDesde, fechaHasta, ct);
        var v0Rows = await QueryParcialRows<PmoV0Parcial>(proyectoId, fechaDesde, fechaHasta, ct);
        var npcRows = await QueryParcialRows<PmoNpcParcial>(proyectoId, fechaDesde, fechaHasta, ct);
        var apiRows = await QueryParcialRows<PmoApiParcial>(proyectoId, fechaDesde, fechaHasta, ct);

        var curva = BuildCurvaAcumulada(realRows, v0Rows, npcRows, apiRows);
        var fechaSegKey = fechaSeguimiento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var evmFechaOk = curva.Any(p => string.Equals(p.Periodo, fechaSegKey, StringComparison.Ordinal));
        var evmMsg = evmFechaOk
            ? null
            : "La fecha de seguimiento no coincide con ningún período de la serie filtrada. Ajuste Desde/Hasta o elija un mes presente en los datos.";

        var (pReal, pApi, pFuente) = await ResolveAvanceFisicoAsync(proyectoId, fechaSeguimiento, bacTotal, apiRows, ct);
        var avanceDto = new Reporte1AvanceFisicoDto
        {
            RealPct = decimal.Round(100m * pReal, 2, MidpointRounding.AwayFromZero),
            ApiPct = decimal.Round(100m * pApi, 2, MidpointRounding.AwayFromZero),
            DesviacionPct = decimal.Round(100m * (pReal - pApi), 2, MidpointRounding.AwayFromZero),
            Fuente = pFuente
        };

        var ac = SumParcialHastaConRecorte(realRows, fechaSeguimiento, fechaDesde, fechaHasta);
        var pv = SumParcialHastaConRecorte(apiRows, fechaSeguimiento, fechaDesde, fechaHasta);
        var ev = bacTotal * pReal;
        var cpi = ac > 0 ? ev / ac : 1m;
        if (cpi <= 0) cpi = 1m;
        var spi = pv > 0 ? ev / pv : 1m;
        if (spi <= 0) spi = 1m;
        var eac = cpi > 0 ? bacTotal / cpi : bacTotal;
        var etc = eac - ac;
        var vac = bacTotal - eac;
        var cv = ev - ac;
        var sv = ev - pv;
        var cvPct = ac != 0 ? (cv / ac) * 100m : (decimal?)null;
        var svPct = pv != 0 ? (sv / pv) * 100m : (decimal?)null;

        var ind = new Reporte1IndicadoresEvmDto
        {
            FechaSeguimiento = fechaSegKey,
            Ac = ac,
            Pv = pv,
            Ev = ev,
            Bac = bacTotal,
            Cv = cv,
            Sv = sv,
            CvPct = cvPct,
            SvPct = svPct,
            Cpi = decimal.Round(cpi, 4, MidpointRounding.AwayFromZero),
            Spi = decimal.Round(spi, 4, MidpointRounding.AwayFromZero),
            Eac = eac,
            Etc = etc,
            Vac = vac,
            PctEv = bacTotal > 0 ? decimal.Round(100m * ev / bacTotal, 2, MidpointRounding.AwayFromZero) : 0,
            PctPv = bacTotal > 0 ? decimal.Round(100m * pv / bacTotal, 2, MidpointRounding.AwayFromZero) : 0,
            PctAc = bacTotal > 0 ? decimal.Round(100m * ac / bacTotal, 2, MidpointRounding.AwayFromZero) : 0,
            EstadoCosto = cv >= 0 ? "Bajo presupuesto" : "Sobre presupuesto",
            EstadoCronograma = sv >= 0 ? "Adelantado" : "Atrasado",
            EstadoGeneral = EstadoGeneralEvm(cpi, spi)
        };

        var tabla = BuildTablaCategorias(realRows, v0Rows, npcRows, apiRows, bacTotal);
        var cascV0 = BuildCascadaParcial("Cascada V0 (A−B)", v0Rows, realRows);
        var cascApi = BuildCascadaParcial("Cascada API (A−D)", apiRows, realRows);

        var porGanar = bacTotal - ev;
        var ieac = await BuildIeacAsync(proyectoId, fechaSeguimiento, bacTotal, ac, ev, pv, porGanar, cpi, spi, ct);
        var ecd = await BuildEcdAsync(proyectoId, fechaSeguimiento, bacTotal, ev, pv, spi, porGanar, ct);

        return new Reporte1CurvaSEvmPackDto
        {
            EvmFechaOk = evmFechaOk,
            EvmFechaMensaje = evmMsg,
            Curva = curva,
            Indicadores = ind,
            AvanceFisico = avanceDto,
            TablaCategorias = tabla.Filas,
            TablaTotales = tabla.Totales,
            CascadaV0 = cascV0,
            CascadaApi = cascApi,
            Ieac = ieac,
            Ecd = ecd,
            BacTotalProyecto = bacTotal
        };
    }

    private static string EstadoGeneralEvm(decimal cpi, decimal spi)
    {
        if (cpi >= 1 && spi >= 1) return "Excelente";
        if (cpi >= 1 && spi < 1) return "Bueno (costo); cronograma a revisar";
        if (cpi < 1 && spi >= 1) return "Aceptable (cronograma); costo a revisar";
        return "Requiere atención";
    }

    private async Task<List<PmoVectorFinancieroRow>> QueryParcialRows<T>(
        int proyectoId,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct) where T : PmoVectorFinancieroRow
    {
        var q = _db.Set<T>().AsNoTracking().Where(x => x.ProyectoId == proyectoId);
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        var list = await q.OrderBy(x => x.Periodo).ToListAsync(ct);
        return list.Select(x => (PmoVectorFinancieroRow)x).ToList();
    }

    private static decimal SumParcialHastaConRecorte(
        IReadOnlyList<PmoVectorFinancieroRow> rows,
        DateOnly fechaSeg,
        DateOnly? desde,
        DateOnly? hasta)
    {
        return rows.Where(r =>
                r.Periodo <= fechaSeg &&
                (!desde.HasValue || r.Periodo >= desde.Value) &&
                (!hasta.HasValue || r.Periodo <= hasta.Value))
            .Sum(r => r.Monto);
    }

    private async Task<(decimal pReal, decimal pApi, string fuente)> ResolveAvanceFisicoAsync(
        int proyectoId,
        DateOnly fechaSeg,
        decimal bacTotal,
        IReadOnlyList<PmoVectorFinancieroRow> apiRowsFiltered,
        CancellationToken ct)
    {
        var realLast = await _db.PmoAvFisicoReales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .OrderByDescending(x => x.Periodo)
            .FirstOrDefaultAsync(ct);
        var apiLast = await _db.PmoAvFisicoApis.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .OrderByDescending(x => x.Periodo)
            .FirstOrDefaultAsync(ct);

        var pReal = Clamp01(realLast?.ApiAcum ?? 0m);
        var pApi = Clamp01(apiLast?.ApiAcum ?? 0m);
        if (realLast != null)
        {
            if (apiLast != null)
                return (pReal, pApi, "av_fisico_real / av_fisico_api (api_acum)");
            return (pReal, pApi, "av_fisico_real (api_acum); API sin fila en fecha");
        }

        if (bacTotal > 0)
        {
            var npcHasta = await _db.PmoNpcParciales.AsNoTracking()
                .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
                .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;
            if (npcHasta > 0)
            {
                var pNpc = Clamp01(npcHasta / bacTotal);
                return (pNpc, pNpc, "Fallback NPC: Σ npc_parcial hasta fecha / BAC (como PHP sin av_fisico_real)");
            }
        }

        if (bacTotal > 0 && apiRowsFiltered.Count > 0)
        {
            var minP = apiRowsFiltered.Min(x => x.Periodo);
            var maxP = apiRowsFiltered.Max(x => x.Periodo);
            if (maxP > minP)
            {
                var span = (maxP.DayNumber - minP.DayNumber);
                if (span > 0)
                {
                    var t = (fechaSeg.DayNumber - minP.DayNumber) / (decimal)span;
                    var p = Clamp01(t);
                    return (p, p, "Fallback temporal (primer–último período API en filtro)");
                }
            }
        }

        return (0m, 0m, "Sin avance físico ni fallback");
    }

    private static decimal Clamp01(decimal v)
    {
        if (v < 0) return 0;
        if (v > 1) return 1;
        return v;
    }

    private static IReadOnlyList<Reporte1CurvaPuntoDto> BuildCurvaAcumulada(
        IReadOnlyList<PmoVectorFinancieroRow> realRows,
        IReadOnlyList<PmoVectorFinancieroRow> v0Rows,
        IReadOnlyList<PmoVectorFinancieroRow> npcRows,
        IReadOnlyList<PmoVectorFinancieroRow> apiRows)
    {
        var byPeriod = new Dictionary<string, (decimal R, decimal V, decimal N, decimal A)>(StringComparer.Ordinal);
        void add(IReadOnlyList<PmoVectorFinancieroRow> rows, int idx)
        {
            foreach (var r in rows)
            {
                var k = r.Periodo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!byPeriod.TryGetValue(k, out var t))
                    t = (0, 0, 0, 0);
                var m = r.Monto;
                t = idx switch
                {
                    0 => (t.R + m, t.V, t.N, t.A),
                    1 => (t.R, t.V + m, t.N, t.A),
                    2 => (t.R, t.V, t.N + m, t.A),
                    _ => (t.R, t.V, t.N, t.A + m)
                };
                byPeriod[k] = t;
            }
        }

        add(realRows, 0);
        add(v0Rows, 1);
        add(npcRows, 2);
        add(apiRows, 3);

        var keys = byPeriod.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        decimal ar = 0, av = 0, an = 0, aa = 0;
        var list = new List<Reporte1CurvaPuntoDto>();
        foreach (var k in keys)
        {
            var p = byPeriod[k];
            ar += p.R;
            av += p.V;
            an += p.N;
            aa += p.A;
            list.Add(new Reporte1CurvaPuntoDto
            {
                Periodo = k,
                RealAcum = ar,
                V0Acum = av,
                NpcAcum = an,
                ApiAcum = aa
            });
        }

        return list;
    }

    private static (IReadOnlyList<Reporte1TablaCategoriaDto> Filas, Reporte1TablaCategoriaTotalesDto Totales) BuildTablaCategorias(
        IReadOnlyList<PmoVectorFinancieroRow> realRows,
        IReadOnlyList<PmoVectorFinancieroRow> v0Rows,
        IReadOnlyList<PmoVectorFinancieroRow> npcRows,
        IReadOnlyList<PmoVectorFinancieroRow> apiRows,
        decimal bacTotalAbsoluto)
    {
        var cats = PycFinancierosCatalog.CategoriasKpi;
        var mapR = SumByCategory(realRows, cats);
        var mapV = SumByCategory(v0Rows, cats);
        var mapN = SumByCategory(npcRows, cats);
        var mapA = SumByCategory(apiRows, cats);

        var filas = new List<Reporte1TablaCategoriaDto>();
        decimal tR = 0, tV = 0, tN = 0, tA = 0;
        foreach (var c in cats)
        {
            var r = mapR.GetValueOrDefault(c);
            var v = mapV.GetValueOrDefault(c);
            var n = mapN.GetValueOrDefault(c);
            var a = mapA.GetValueOrDefault(c);
            tR += r;
            tV += v;
            tN += n;
            tA += a;
            filas.Add(new Reporte1TablaCategoriaDto
            {
                Categoria = c,
                RealUsd = r,
                V0Usd = v,
                NpcUsd = n,
                ApiUsd = a,
                CascadaV0 = r - v,
                CascadaApi = r - a
            });
        }

        var denom = bacTotalAbsoluto > 0 ? bacTotalAbsoluto : 1m;
        var tot = new Reporte1TablaCategoriaTotalesDto
        {
            TotalReal = tR,
            TotalV0 = tV,
            TotalNpc = tN,
            TotalApi = tA,
            PctReal = decimal.Round(100m * tR / denom, 2, MidpointRounding.AwayFromZero),
            PctV0 = decimal.Round(100m * tV / denom, 2, MidpointRounding.AwayFromZero),
            PctNpc = decimal.Round(100m * tN / denom, 2, MidpointRounding.AwayFromZero),
            PctApi = decimal.Round(100m * tA / denom, 2, MidpointRounding.AwayFromZero)
        };
        return (filas, tot);
    }

    private static Dictionary<string, decimal> SumByCategory(IReadOnlyList<PmoVectorFinancieroRow> rows, IReadOnlyList<string> cats)
    {
        var d = cats.ToDictionary(c => c, _ => 0m, StringComparer.Ordinal);
        foreach (var r in rows)
        {
            var cat = RowToKpiCategoria(r.Tipo, r.CatVp, r.DetalleFactorial, cats);
            if (cat == null) continue;
            d[cat] += r.Monto;
        }

        return d;
    }

    private static Reporte1CascadaDto BuildCascadaParcial(string titulo, IReadOnlyList<PmoVectorFinancieroRow> baseRows, IReadOnlyList<PmoVectorFinancieroRow> realRows)
    {
        var cats = PycFinancierosCatalog.CategoriasKpi;
        var baseMap = SumByCategory(baseRows, cats);
        var realMap = SumByCategory(realRows, cats);
        var barras = new List<Reporte1CascadaBarDto>();
        var totalBaseMs = decimal.Round(cats.Sum(c => baseMap.GetValueOrDefault(c)) / 1_000_000m, 0, MidpointRounding.AwayFromZero);
        barras.Add(new Reporte1CascadaBarDto
        {
            Etiqueta = titulo.Contains("V0", StringComparison.Ordinal) ? "Versión V0" : "Versión API",
            Tipo = "inicio",
            BaseMs = 0,
            DeltaMs = totalBaseMs,
            ColorDelta = "#4a90e2"
        });
        var run = totalBaseMs;
        foreach (var c in cats)
        {
            var deltaUsd = realMap.GetValueOrDefault(c) - baseMap.GetValueOrDefault(c);
            var deltaMs = decimal.Round(deltaUsd / 1_000_000m, 0, MidpointRounding.AwayFromZero);
            var color = deltaMs > 0 ? "#4a90e2" : (deltaMs < 0 ? "#ff9800" : "#9e9e9e");
            var baseMs = deltaMs >= 0 ? run : run + deltaMs;
            barras.Add(new Reporte1CascadaBarDto
            {
                Etiqueta = c,
                Tipo = "delta",
                BaseMs = baseMs,
                DeltaMs = deltaMs,
                ColorDelta = color
            });
            run += deltaMs;
        }

        var totalRealMs = decimal.Round(cats.Sum(c => realMap.GetValueOrDefault(c)) / 1_000_000m, 0, MidpointRounding.AwayFromZero);
        barras.Add(new Reporte1CascadaBarDto
        {
            Etiqueta = "Av. Real",
            Tipo = "total",
            BaseMs = 0,
            DeltaMs = totalRealMs,
            ColorDelta = "#43a047"
        });
        return new Reporte1CascadaDto { Titulo = titulo, Barras = barras };
    }

    private async Task<Reporte1IeacDto> BuildIeacAsync(
        int proyectoId,
        DateOnly fechaSeg,
        decimal bac,
        decimal ac,
        decimal ev,
        decimal pv,
        decimal porGanar,
        decimal cpi,
        decimal spi,
        CancellationToken ct)
    {
        var cpi3 = await RollingCpiAverageAsync(proyectoId, fechaSeg, bac, 3, ct);
        var cpi6 = await RollingCpiAverageAsync(proyectoId, fechaSeg, bac, 6, ct);
        var a = 0.7m;
        var b = 0.3m;
        var denomI = a * cpi + b * spi;
        if (denomI <= 0) denomI = 1m;

        decimal ieacA = ac + porGanar;
        decimal ieacB = cpi > 0 ? bac / cpi : bac;
        decimal ieacC = cpi > 0 ? ac + porGanar / cpi : ac + porGanar;
        decimal ieacD = cpi3 > 0 ? ac + porGanar / cpi3 : ieacC;
        decimal ieacE = cpi6 > 0 ? ac + porGanar / cpi6 : ieacC;
        decimal ieacF = cpi * spi > 0 ? ac + porGanar / (cpi * spi) : ieacC;
        decimal ieacG = cpi3 * spi > 0 ? ac + porGanar / (cpi3 * spi) : ieacF;
        decimal ieacH = cpi6 * spi > 0 ? ac + porGanar / (cpi6 * spi) : ieacF;
        decimal ieacI = ac + porGanar / denomI;

        var items = new[]
        {
            new Reporte1IeacItemDto { Id = "a", Etiqueta = "IEAC (a) AC + por ganar", Valor = ieacA },
            new Reporte1IeacItemDto { Id = "b", Etiqueta = "IEAC (b) BAC / CPI", Valor = ieacB },
            new Reporte1IeacItemDto { Id = "c", Etiqueta = "IEAC (c) AC + (BAC−EV)/CPI", Valor = ieacC },
            new Reporte1IeacItemDto { Id = "d", Etiqueta = "IEAC (d) AC + (BAC−EV)/CPI(3m)", Valor = ieacD },
            new Reporte1IeacItemDto { Id = "e", Etiqueta = "IEAC (e) AC + (BAC−EV)/CPI(6m)", Valor = ieacE },
            new Reporte1IeacItemDto { Id = "f", Etiqueta = "IEAC (f) AC + (BAC−EV)/(CPI·SPI)", Valor = ieacF },
            new Reporte1IeacItemDto { Id = "g", Etiqueta = "IEAC (g) AC + (BAC−EV)/(CPI3m·SPI)", Valor = ieacG },
            new Reporte1IeacItemDto { Id = "h", Etiqueta = "IEAC (h) AC + (BAC−EV)/(CPI6m·SPI)", Valor = ieacH },
            new Reporte1IeacItemDto { Id = "i", Etiqueta = "IEAC (i) AC + (BAC−EV)/(0,7·CPI+0,3·SPI)", Valor = ieacI }
        };

        var vals = items.Select(x => x.Valor).ToList();
        return new Reporte1IeacDto
        {
            Metodologias = items,
            Promedio = vals.Count > 0 ? vals.Average() : 0,
            Maximo = vals.Count > 0 ? vals.Max() : 0,
            Minimo = vals.Count > 0 ? vals.Min() : 0,
            TrabajoPorGanar = porGanar
        };
    }

    /// <summary>Promedio de «CPI mensual» en los últimos <paramref name="months"/> meses con datos (aprox. PHP).</summary>
    private async Task<decimal> RollingCpiAverageAsync(
        int proyectoId,
        DateOnly fechaSeg,
        decimal bac,
        int months,
        CancellationToken ct)
    {
        var reals = await _db.PmoRealParciales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .OrderByDescending(x => x.Periodo)
            .ToListAsync(ct);
        if (reals.Count == 0) return 1m;

        var avRows = await _db.PmoAvFisicoReales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .ToListAsync(ct);
        var avByMonth = avRows
            .GroupBy(x => MonthKey(x.Periodo))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(z => z.Periodo).First().ApiAcum, StringComparer.Ordinal);

        var monthKeys = reals.Select(r => MonthKey(r.Periodo)).Distinct().OrderByDescending(x => x, StringComparer.Ordinal).Take(months).ToList();
        if (monthKeys.Count == 0) return 1m;

        var cpis = new List<decimal>();
        foreach (var mk in monthKeys)
        {
            if (!DateOnly.TryParse(mk + "-01", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
                continue;
            var acCum = reals.Where(x => x.Periodo <= LastDayOfMonth(monthStart)).Sum(x => x.Monto);
            avByMonth.TryGetValue(mk, out var p);
            p = Clamp01(p);
            var evSnap = bac * p;
            if (acCum > 0)
                cpis.Add(evSnap / acCum);
        }

        if (cpis.Count == 0) return 1m;
        return cpis.Average() is var avg && avg > 0 ? avg : 1m;
    }

    private static string MonthKey(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static DateOnly LastDayOfMonth(DateOnly firstOfMonth) =>
        firstOfMonth.AddMonths(1).AddDays(-1);

    private async Task<Reporte1EcdDto> BuildEcdAsync(
        int proyectoId,
        DateOnly fechaSeg,
        decimal bac,
        decimal ev,
        decimal pv,
        decimal spi,
        decimal porGanar,
        CancellationToken ct)
    {
        var primerApi = await _db.PmoApiParciales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId)
            .OrderBy(x => x.Periodo)
            .Select(x => x.Periodo)
            .FirstOrDefaultAsync(ct);

        var durPlan = await _db.PmoApiParciales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId)
            .Select(x => x.Periodo)
            .Distinct()
            .CountAsync(ct);

        var plazoControl = await _db.PmoAvFisicoApis.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .Select(x => x.Periodo)
            .Distinct()
            .CountAsync(ct);

        var apiHasta = await _db.PmoApiParciales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .ToListAsync(ct);

        var pvUltimoMes = SumLastCalendarMonth(apiHasta, fechaSeg);
        var pv3 = AvgMonthlySumLastN(apiHasta, fechaSeg, 3);
        var pv6 = AvgMonthlySumLastN(apiHasta, fechaSeg, 6);
        var pv12 = AvgMonthlySumLastN(apiHasta, fechaSeg, 12);

        var ev3 = await AvgEvPhysicalAsync(proyectoId, fechaSeg, 3, ct);
        var ev6 = await AvgEvPhysicalAsync(proyectoId, fechaSeg, 6, ct);
        var ev12 = await AvgEvPhysicalAsync(proyectoId, fechaSeg, 12, ct);

        decimal ecdA = spi > 0 ? durPlan / spi : durPlan;
        decimal ecdB = plazoControl + SafeDiv(porGanar, pvUltimoMes);
        decimal ecdC = plazoControl + SafeDiv(porGanar, pv3);
        decimal ecdD = plazoControl + SafeDiv(porGanar, pv6);
        decimal ecdE = plazoControl + SafeDiv(porGanar, pv12);

        decimal ecdG = plazoControl + SafeDiv(porGanar, bac * ev3);
        decimal ecdH = plazoControl + SafeDiv(porGanar, bac * ev6);
        decimal ecdI = plazoControl + SafeDiv(porGanar, bac * ev12);

        var ecdF = plazoControl + SafeDiv(bac - ev, pv3);
        var ecdJ = durPlan + SafeDiv(bac - ev, pv6);
        var ecdK = plazoControl + SafeDiv(bac - ev, pvUltimoMes);

        var meses = new[] { ecdA, ecdB, ecdC, ecdD, ecdE, ecdF, ecdG, ecdH, ecdI, ecdJ, ecdK };
        var fechas = meses.Select(m => MesesDesdeInicioAFecha(primerApi, (double)m)).Where(d => d.HasValue).Select(d => d!.Value).ToList();

        Reporte1EcdItemDto Item(string id, string label, decimal mesVal) =>
            new() { Id = id, Etiqueta = label, FechaIso = MesesDesdeInicioAFecha(primerApi, (double)mesVal)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };

        var list = new List<Reporte1EcdItemDto>
        {
            Item("a", "ECD (a) Duración / SPI", ecdA),
            Item("b", "ECD (b) Plazo + (BAC−EV)/PV último mes", ecdB),
            Item("c", "ECD (c) Plazo + (BAC−EV)/PV(3m)", ecdC),
            Item("d", "ECD (d) Plazo + (BAC−EV)/PV(6m)", ecdD),
            Item("e", "ECD (e) Plazo + (BAC−EV)/PV(12m)", ecdE),
            Item("f", "ECD (f) Plazo + (BAC−EV)/PV(3m) alt.", ecdF),
            Item("g", "ECD (g) Plazo + por ganar / (BAC·EV3m)", ecdG),
            Item("h", "ECD (h) Plazo + por ganar / (BAC·EV6m)", ecdH),
            Item("i", "ECD (i) Plazo + por ganar / (BAC·EV12m)", ecdI),
            Item("j", "ECD (j) Duración + (BAC−EV)/PV(6m)", ecdJ),
            Item("k", "ECD (k) Plazo + (BAC−EV)/PV último mes alt.", ecdK)
        };

        var validDates = list.Select(x => x.FechaIso).Where(s => !string.IsNullOrEmpty(s))
            .Select(s => DateOnly.Parse(s!, CultureInfo.InvariantCulture)).ToList();

        DateOnly? minD = validDates.Count > 0 ? validDates.Min() : null;
        DateOnly? maxD = validDates.Count > 0 ? validDates.Max() : null;
        DateOnly? avgD = null;
        if (validDates.Count > 0)
        {
            var avgDays = (int)Math.Round(validDates.Average(d => d.DayNumber));
            avgD = DateOnly.FromDayNumber(avgDays);
        }

        int? rango = minD.HasValue && maxD.HasValue
            ? Math.Max(0, (maxD.Value.Year - minD.Value.Year) * 12 + (maxD.Value.Month - minD.Value.Month))
            : null;

        return new Reporte1EcdDto
        {
            Metodologias = list,
            FechaPromedio = avgD?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            FechaMaxima = maxD?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            FechaMinima = minD?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RangoMeses = rango
        };
    }

    private static DateOnly? MesesDesdeInicioAFecha(DateOnly primerPeriodo, double mesesTotales)
    {
        if (mesesTotales <= 0 || primerPeriodo == default) return null;
        var n = (int)Math.Round(mesesTotales, MidpointRounding.AwayFromZero);
        try
        {
            return primerPeriodo.AddMonths(Math.Max(0, n - 1));
        }
        catch
        {
            return null;
        }
    }

    private static decimal SafeDiv(decimal num, decimal den) => den > 0 ? num / den : 0m;

    private async Task<decimal> AvgEvPhysicalAsync(int proyectoId, DateOnly fechaSeg, int n, CancellationToken ct)
    {
        var rows = await _db.PmoAvFisicoReales.AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo <= fechaSeg)
            .ToListAsync(ct);
        var apiParciales = rows
            .GroupBy(x => MonthKey(x.Periodo))
            .Select(g => new { Mk = g.Key, Row = g.OrderByDescending(z => z.Periodo).First() })
            .OrderByDescending(x => x.Mk, StringComparer.Ordinal)
            .Take(n)
            .Select(x => x.Row.ApiParcial)
            .ToList();
        if (apiParciales.Count == 0) return 0m;
        return apiParciales.Average();
    }

    private static decimal SumLastCalendarMonth(IReadOnlyList<PmoApiParcial> rows, DateOnly fechaSeg)
    {
        var mk = MonthKey(fechaSeg);
        return rows.Where(x => MonthKey(x.Periodo) == mk).Sum(x => x.Monto);
    }

    private static decimal AvgMonthlySumLastN(IReadOnlyList<PmoApiParcial> rows, DateOnly fechaSeg, int n)
    {
        var sums = rows
            .GroupBy(x => MonthKey(x.Periodo))
            .Where(g => string.Compare(g.Key, MonthKey(fechaSeg), StringComparison.Ordinal) <= 0)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .Take(n)
            .Select(g => g.Sum(x => x.Monto))
            .ToList();
        if (sums.Count == 0) return 0m;
        return sums.Average();
    }

    public static string? RowToKpiCategoria(string? tipo, string? catVp, string? detalleFactorial, IReadOnlyList<string> categoriasKpi)
    {
        var fromTipo = CategoriaPorCodigoSap(tipo, categoriasKpi);
        if (fromTipo != null) return fromTipo;
        var fromCat = CategoriaPorCodigoSap(catVp, categoriasKpi);
        if (fromCat != null) return fromCat;
        return MapDetalleToCategoria(detalleFactorial, categoriasKpi);
    }

    private static string? CategoriaPorCodigoSap(string? texto, IReadOnlyList<string> categoriasKpi)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var upper = texto.Trim().ToUpperInvariant().Replace("  ", " ", StringComparison.Ordinal);
        foreach (var kv in PycFinancierosCatalog.CodigoSapACategoria.OrderByDescending(x => x.Key.Length))
        {
            var code = kv.Key.ToUpperInvariant();
            if (upper == code) return kv.Value;
            var escaped = Regex.Escape(code);
            if (Regex.IsMatch(upper, $@"(^|[^A-Z0-9]){escaped}([^A-Z0-9]|$)", RegexOptions.IgnoreCase))
                return kv.Value;
        }

        return null;
    }

    private static string? MapDetalleToCategoria(string? detalle, IReadOnlyList<string> categoriasKpi)
    {
        var n = Normalize(detalle);
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var c in categoriasKpi)
        {
            var cn = Normalize(c).Replace(".", "", StringComparison.Ordinal);
            var nn = n.Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            if (nn.Contains(cn.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal) ||
                cn.Replace(" ", "", StringComparison.Ordinal).Contains(nn, StringComparison.Ordinal))
                return c;
        }

        if (n.Contains("CONSTR", StringComparison.Ordinal)) return "CONSTRUCCION";
        if (n.Contains("INDIRECT", StringComparison.Ordinal)) return "INDIRECTOS DE CONTRATISTAS";
        if (n.Contains("EQUIP", StringComparison.Ordinal) || n.Contains("MATERIAL", StringComparison.Ordinal)) return "EQUIPOS Y MATERIALES";
        if (n.Contains("INGEN", StringComparison.Ordinal)) return "INGENIERIA";
        if (n.Contains("SERVIC", StringComparison.Ordinal) || n.Contains("APOYO", StringComparison.Ordinal)) return "SERVICIOS DE APOYO A LA CONSTRUCCION";
        if (n.Contains("ADM", StringComparison.Ordinal) || n.Contains("ADMIN", StringComparison.Ordinal)) return "ADM. DEL PROYECTO";
        if (n.Contains("ESPECIAL", StringComparison.Ordinal)) return "COSTOS ESPECIALES";
        if (n.Contains("CONTING", StringComparison.Ordinal)) return "CONTINGENCIA";
        return null;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim().ToUpperInvariant();
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    public async Task<Reporte1CascadaDetalleDto> BuildCascadaDetalleAsync(
        int proyectoId,
        string categoria,
        string tipo,
        decimal montoObjetivoUsd,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        CancellationToken ct)
    {
        var cats = PycFinancierosCatalog.CategoriasKpi;
        if (!cats.Contains(categoria, StringComparer.Ordinal))
            throw new InvalidOperationException("Categoría VP no válida para análisis de cascada.");

        var tipoNorm = string.Equals(tipo, "API", StringComparison.OrdinalIgnoreCase) ? "API" : "V0";
        var baseRows = tipoNorm == "API"
            ? await QueryParcialRows<PmoApiAcumulada>(proyectoId, fechaDesde, fechaHasta, ct)
            : await QueryParcialRows<PmoV0Acumulada>(proyectoId, fechaDesde, fechaHasta, ct);
        var realRows = await QueryParcialRows<PmoRealAcumulado>(proyectoId, fechaDesde, fechaHasta, ct);

        var baseByP = SumByPeriodForCategoria(baseRows, categoria, cats);
        var realByP = SumByPeriodForCategoria(realRows, categoria, cats);
        var periodos = baseByP.Keys.Union(realByP.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // Fallback defensivo: si tablas acumuladas no tienen datos útiles para la categoría,
        // rehacer con parciales acumulando por período para mantener el drill-down operativo.
        if (periodos.Count == 0)
        {
            var baseParcRows = tipoNorm == "API"
                ? await QueryParcialRows<PmoApiParcial>(proyectoId, fechaDesde, fechaHasta, ct)
                : await QueryParcialRows<PmoV0Parcial>(proyectoId, fechaDesde, fechaHasta, ct);
            var realParcRows = await QueryParcialRows<PmoRealParcial>(proyectoId, fechaDesde, fechaHasta, ct);

            baseByP = ToCumulativeByPeriod(SumByPeriodForCategoria(baseParcRows, categoria, cats));
            realByP = ToCumulativeByPeriod(SumByPeriodForCategoria(realParcRows, categoria, cats));
            periodos = baseByP.Keys.Union(realByP.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        var filas = new List<Reporte1CascadaDetalleFilaDto>();
        foreach (var p in periodos)
        {
            var b = baseByP.GetValueOrDefault(p);
            var r = realByP.GetValueOrDefault(p);
            var d = r - b;
            filas.Add(new Reporte1CascadaDetalleFilaDto
            {
                Periodo = p,
                BaseUsd = b,
                RealUsd = r,
                DiferenciaUsd = d,
                DiferenciaPct = b != 0 ? decimal.Round(100m * d / b, 2, MidpointRounding.AwayFromZero) : (decimal?)null
            });
        }

        var totalBase = filas.Count > 0 ? filas[^1].BaseUsd : 0m;
        var totalReal = filas.Count > 0 ? filas[^1].RealUsd : 0m;
        var difTotal = totalReal - totalBase;
        var pctTotal = totalBase != 0 ? decimal.Round(100m * difTotal / totalBase, 2, MidpointRounding.AwayFromZero) : (decimal?)null;

        return new Reporte1CascadaDetalleDto
        {
            Categoria = categoria,
            Tipo = tipoNorm,
            MontoObjetivoUsd = montoObjetivoUsd,
            TotalBaseUsd = totalBase,
            TotalRealUsd = totalReal,
            DiferenciaTotalUsd = difTotal,
            DiferenciaTotalPct = pctTotal,
            Conclusiones = BuildConclusionesCascada(filas, difTotal, pctTotal),
            Filas = filas
        };
    }

    private static Dictionary<string, decimal> SumByPeriodForCategoria(
        IReadOnlyList<PmoVectorFinancieroRow> rows,
        string categoria,
        IReadOnlyList<string> cats)
    {
        var d = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var catNorm = Normalize(categoria);
        foreach (var r in rows)
        {
            var cat = RowToKpiCategoria(r.Tipo, r.CatVp, r.DetalleFactorial, cats);
            var matchCanon = string.Equals(cat, categoria, StringComparison.Ordinal);
            var matchDetalle = Normalize(r.DetalleFactorial) == catNorm;
            var matchCatVp = Normalize(r.CatVp) == catNorm;
            if (!matchCanon && !matchDetalle && !matchCatVp) continue;
            var p = r.Periodo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            d[p] = d.GetValueOrDefault(p) + r.Monto;
        }

        return d;
    }

    private static Dictionary<string, decimal> ToCumulativeByPeriod(Dictionary<string, decimal> byPeriod)
    {
        var outMap = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal run = 0m;
        foreach (var p in byPeriod.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            run += byPeriod[p];
            outMap[p] = run;
        }
        return outMap;
    }

    private static string BuildConclusionesCascada(
        IReadOnlyList<Reporte1CascadaDetalleFilaDto> filas,
        decimal difTotal,
        decimal? pctTotal)
    {
        if (filas.Count == 0)
            return "Sin datos para la categoría y rango seleccionados. Revise filtros de fecha o disponibilidad en tablas acumuladas.";

        var absPct = Math.Abs(pctTotal ?? 0m);
        var magnitud = absPct > 50m ? "desviación significativa" : (absPct >= 20m ? "desviación moderada" : "desviación controlada");
        var direccion = difTotal >= 0 ? "sobre la base" : "bajo la base";

        var positivos = filas.Where(x => x.DiferenciaUsd > 0).ToList();
        var primeroPos = positivos.Count > 0 ? positivos.OrderBy(x => x.Periodo, StringComparer.Ordinal).First() : null;
        var mayorPos = positivos.Count > 0 ? positivos.OrderByDescending(x => x.DiferenciaUsd).First() : null;

        var ult3 = filas.TakeLast(3).Select(x => x.DiferenciaUsd).ToList();
        var tendencia = "variable";
        if (ult3.Count >= 2)
        {
            var monoUp = true;
            var monoDown = true;
            for (var i = 1; i < ult3.Count; i++)
            {
                if (ult3[i] < ult3[i - 1]) monoUp = false;
                if (ult3[i] > ult3[i - 1]) monoDown = false;
            }
            if (monoUp) tendencia = "creciente";
            else if (monoDown) tendencia = "decreciente";
        }

        var sb = new StringBuilder();
        sb.Append($"Resultado global: {magnitud} ({(pctTotal ?? 0m):0.##}%) {direccion}.");
        if (primeroPos != null)
            sb.Append($" Primer aumento observado en {primeroPos.Periodo}.");
        if (mayorPos != null)
            sb.Append($" Mayor aumento en {mayorPos.Periodo} por {mayorPos.DiferenciaUsd:0,0} USD.");
        sb.Append($" Tendencia últimos 3 períodos: {tendencia}.");
        sb.Append(" Interpretación: validar anticipos, reasignaciones o sobrecostos de ejecución respecto de la base.");
        return sb.ToString();
    }
}
