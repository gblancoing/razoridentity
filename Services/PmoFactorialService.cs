using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.PmoFinance;
using System.Globalization;
using System.Text.Json;

namespace RazorIdentity.Services;

public sealed class PmoFactorialService
{
    private readonly ApplicationDbContext _db;
    private static readonly string[] TiposOrden = ["real", "v0", "npc", "api", "poa"];

    public PmoFactorialService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FactorialLineasBasesResponseDto> BuildLineasBasesAsync(
        int proyectoId,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        string? vector,
        string? tablaVisualizar,
        CancellationToken ct)
    {
        var tablaSel = NormalizeTabla(tablaVisualizar);
        var vecSel = string.IsNullOrWhiteSpace(vector) ? null : vector.Trim();

        var rawByType = await LoadRowsByTypeAsync(proyectoId, ct);
        var filteredByType = rawByType.ToDictionary(
            kv => kv.Key,
            kv => GetFilteredRows(kv.Value, fechaDesde, fechaHasta, vecSel).ToList(),
            StringComparer.OrdinalIgnoreCase);

        var chart = BuildCurvaSData(filteredByType, tablaSel);
        var periodoLineaCorte = GetPeriodoLineaCorte(chart);
        var cards = BuildCards(tablaSel, filteredByType, chart, periodoLineaCorte);
        var tableItems = BuildTableRows(tablaSel, filteredByType);

        var allVectors = rawByType.Values
            .SelectMany(x => x)
            .Select(x => x.Vector?.Trim() ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FactorialLineasBasesResponseDto
        {
            Subtitulo = $"Proyecto {proyectoId}",
            PeriodoLineaCorte = periodoLineaCorte,
            Vectores = allVectors,
            Cards = cards,
            Chart = new FactorialLineasBasesChartDto { Series = chart },
            Table = new FactorialLineasBasesTableDto
            {
                TablaVisualizar = tablaSel,
                TotalRegistros = tableItems.Count,
                Items = tableItems
            }
        };
    }

    public async Task<FactorialPredictividadResponseDto> BuildPredictividadAsync(
        int proyectoId,
        DateOnly hasta20,
        CancellationToken ct)
    {
        await EnsurePredictividadTableAsync(ct);
        var monthStart = new DateOnly(hasta20.Year, hasta20.Month, 1);
        var monthEnd = EndOfMonth(monthStart);
        var descripcion = MapDescripcionVersion(monthStart);

        var proyFin = await _db.PmoFinancieroSaps
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId &&
                        x.Periodo.HasValue &&
                        x.Periodo.Value >= monthStart &&
                        x.Periodo.Value <= monthEnd &&
                        x.Descripcion == descripcion)
            .SumAsync(x => (decimal?)(x.Mo + x.Ic + x.Em + x.Ie + x.Sc + x.Ad + x.Cl + x.Ct), ct) ?? 0m;

        var realFin = await _db.PmoRealParciales
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= monthStart && x.Periodo <= monthEnd)
            .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;

        var realFisRow = await _db.PmoAvFisicoReales
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= monthStart && x.Periodo <= monthEnd)
            .OrderByDescending(x => x.Periodo)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        var realFis = (realFisRow?.ApiParcial ?? 0m) * 100m;

        var prevMonthEnd = monthStart.AddMonths(-1).AddDays(-1);
        var proyFisRow = await _db.PmoPredictividades
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.PeriodoPrediccion == prevMonthEnd)
            .OrderByDescending(x => x.IdPredictivo)
            .FirstOrDefaultAsync(ct);
        var proyFis = proyFisRow?.PorcentajePredicido ?? 0m;

        var desvFin = proyFin != 0m ? ((realFin - proyFin) / proyFin) * 100m : 0m;
        var desvFis = proyFis != 0m ? ((realFis - proyFis) / proyFis) * 100m : 0m;
        var precFin = 100m - Math.Abs(desvFin);
        var precFis = 100m - Math.Abs(desvFis);
        var precProm = (precFin + precFis) / 2m;

        var historialDesde = new DateOnly(monthStart.Year, 1, 1);
        var histFinRaw = await _db.PmoFinancieroSaps
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.Periodo.HasValue && x.Periodo.Value >= historialDesde && x.Periodo.Value <= monthEnd)
            .ToListAsync(ct);
        var histFin = histFinRaw
            .GroupBy(x => new DateOnly(x.Periodo!.Value.Year, x.Periodo.Value.Month, 1))
            .Select(g => new FactorialPredictividadHistItemDto
            {
                Periodo = g.Key.ToString("yyyy-MM"),
                Valor = Round2(g.Sum(x => x.Mo + x.Ic + x.Em + x.Ie + x.Sc + x.Ad + x.Cl + x.Ct))
            })
            .OrderBy(x => x.Periodo)
            .ToList();

        var histFisRaw = await _db.PmoPredictividades
            .AsNoTracking()
            .Where(x => x.ProyectoId == proyectoId && x.PeriodoCierreReal.HasValue && x.PeriodoCierreReal.Value >= historialDesde && x.PeriodoCierreReal.Value <= monthEnd)
            .ToListAsync(ct);
        var histFis = histFisRaw
            .GroupBy(x => new DateOnly(x.PeriodoCierreReal!.Value.Year, x.PeriodoCierreReal.Value.Month, 1))
            .Select(g => new FactorialPredictividadHistItemDto
            {
                Periodo = g.Key.ToString("yyyy-MM"),
                Valor = Round2(g.Sum(x => x.PorcentajePredicido))
            })
            .OrderBy(x => x.Periodo)
            .ToList();

        var tendenciaFin = new List<FactorialPredictividadTrendItemDto>();
        var tendenciaFis = new List<FactorialPredictividadTrendItemDto>();
        var historialMensual = new List<FactorialPredictividadHistorialMensualDto>();
        for (var m = 1; m <= monthStart.Month; m++)
        {
            var d = new DateOnly(monthStart.Year, m, 1);
            var dEnd = EndOfMonth(d);
            var descM = MapDescripcionVersion(d);

            var pFin = await _db.PmoFinancieroSaps
                .AsNoTracking()
                .Where(x => x.ProyectoId == proyectoId && x.Periodo == d && x.Descripcion == descM)
                .SumAsync(x => (decimal?)(x.Mo + x.Ic + x.Em + x.Ie + x.Sc + x.Ad + x.Cl + x.Ct), ct) ?? 0m;
            var rFin = await _db.PmoRealParciales
                .AsNoTracking()
                .Where(x => x.ProyectoId == proyectoId && x.Periodo >= d && x.Periodo <= dEnd)
                .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;
            var dFin = pFin != 0m ? ((rFin - pFin) / pFin) * 100m : 0m;
            var precFinM = Round2(100m - Math.Abs(dFin));
            var notaFinM = CalcularNotaFinanciera(dFin);
            tendenciaFin.Add(new FactorialPredictividadTrendItemDto
            {
                Periodo = d.ToString("yyyy-MM"),
                Proyeccion = Round2(pFin / 1_000_000m),
                Real = Round2(rFin / 1_000_000m),
                DesviacionPct = Round2(dFin)
            });

            var prevEnd = d.AddDays(-1);
            var pFisRow = await _db.PmoPredictividades
                .AsNoTracking()
                .Where(x => x.ProyectoId == proyectoId && x.PeriodoPrediccion == prevEnd)
                .OrderByDescending(x => x.IdPredictivo)
                .FirstOrDefaultAsync(ct);
            var pFis = pFisRow?.PorcentajePredicido ?? 0m;
            var rFisRow = await _db.PmoAvFisicoReales
                .AsNoTracking()
                .Where(x => x.ProyectoId == proyectoId && x.Periodo >= d && x.Periodo <= dEnd)
                .OrderByDescending(x => x.Periodo)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            var rFis = (rFisRow?.ApiParcial ?? 0m) * 100m;
            var dFis = pFis != 0m ? ((rFis - pFis) / pFis) * 100m : 0m;
            var precFisM = Round2(100m - Math.Abs(dFis));
            var notaFisM = CalcularNotaFisica(dFis);
            tendenciaFis.Add(new FactorialPredictividadTrendItemDto
            {
                Periodo = d.ToString("yyyy-MM"),
                Proyeccion = Round2(pFis),
                Real = Round2(rFis),
                DesviacionPct = Round2(dFis)
            });

            historialMensual.Add(new FactorialPredictividadHistorialMensualDto
            {
                PeriodoIso = d.ToString("yyyy-MM"),
                PeriodoEtiqueta = PeriodoEtiquetaMes(d),
                FisProyeccion = Round2(pFis),
                FisReal = Round2(rFis),
                FisDesviacionPct = Round2(dFis),
                FisPrecision = precFisM,
                FisNota = notaFisM,
                FisNotaTexto = TextoNotaPredictividad(notaFisM),
                FinProyeccion = Round2(pFin),
                FinReal = Round2(rFin),
                FinDesviacionPct = Round2(dFin),
                FinPrecision = precFinM,
                FinNota = notaFinM,
                FinNotaTexto = TextoNotaPredictividad(notaFinM)
            });
        }

        var prom = BuildHistorialPromedios(historialMensual);

        return new FactorialPredictividadResponseDto
        {
            Subtitulo = $"Proyecto {proyectoId}",
            Hasta20 = monthStart.ToString("yyyy-MM"),
            FiltroDescripcion = descripcion,
            Resumen = new FactorialPredictividadResumenDto
            {
                ProyeccionFinanciera = Round2(proyFin),
                RealFinanciera = Round2(realFin),
                DesviacionFinancieraPct = Round2(desvFin),
                ProyeccionFisica = Round2(proyFis),
                RealFisica = Round2(realFis),
                DesviacionFisicaPct = Round2(desvFis),
                PrecisionFinanciera = Round2(precFin),
                PrecisionFisica = Round2(precFis),
                PrecisionPromedio = Round2(precProm),
                NotaFinanciera = CalcularNotaFinanciera(desvFin),
                NotaFisica = CalcularNotaFisica(desvFis)
            },
            HistorialFinanciero = histFin,
            HistorialFisico = histFis,
            HistorialMensual = historialMensual,
            HistorialPromedios = prom,
            TendenciaFinanciera = tendenciaFin,
            TendenciaFisica = tendenciaFis
        };
    }

    public async Task<FactorialEficienciaResponseDto> BuildEficienciaGastoAsync(int proyectoId, DateOnly mesCorte, CancellationToken ct)
    {
        var mesDesde = new DateOnly(mesCorte.Year, mesCorte.Month, 1);
        var mesHasta = EndOfMonth(mesDesde);
        var acumDesde = new DateOnly(mesDesde.Year, 1, 1);
        var anualDesde = new DateOnly(mesDesde.Year, 1, 1);
        var anualHasta = new DateOnly(mesDesde.Year, 12, 31);

        var filas = new List<FactorialEficienciaFilaDto>
        {
            await CalculateEfficiencyRowAsync(proyectoId, "Mensual", mesDesde, mesHasta, ct),
            await CalculateEfficiencyRowAsync(proyectoId, "Acumulado", acumDesde, mesHasta, ct),
            await CalculateEfficiencyRowAsync(proyectoId, "Anual", anualDesde, anualHasta, ct)
        };

        var historico = new List<FactorialEficienciaFilaDto>();
        for (var m = 1; m <= mesDesde.Month; m++)
        {
            var d = new DateOnly(mesDesde.Year, m, 1);
            var h = await CalculateEfficiencyRowAsync(proyectoId, d.ToString("MM-yyyy"), d, EndOfMonth(d), ct);
            h.MesIso = d.ToString("yyyy-MM");
            historico.Add(h);
        }

        var promedio = BuildPromedio(historico);

        return new FactorialEficienciaResponseDto
        {
            MesCorte = mesDesde.ToString("yyyy-MM"),
            Subtitulo = $"Proyecto {proyectoId} - corte {mesDesde:MM-yyyy}",
            Filas = filas,
            HistoricoMensual = historico,
            PromedioHistorico = promedio
        };
    }

    public async Task<FactorialEficienciaFilaDto> CalculateEfficiencyRowAsync(int proyectoId, string periodo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var (planV0, gastoReal) = await GetFinancialMetricsAsync(proyectoId, desde, hasta, ct);
        var (progV0Pct, avanceFisicoPct) = await GetPhysicalMetricsAsync(proyectoId, desde, hasta, ct);

        var cumplimientoA = planV0 > 0m ? (gastoReal / planV0) * 100m : 0m;
        var cumplimientoB = progV0Pct > 0m ? (avanceFisicoPct / progV0Pct) * 100m : 0m;
        var eficiencia = cumplimientoA > 0m ? (cumplimientoB / cumplimientoA) * 100m : 0m;

        return new FactorialEficienciaFilaDto
        {
            Periodo = periodo,
            PlanV0Usd = Round2(planV0),
            GastoRealUsd = Round2(gastoReal),
            CumplimientoA = Round2(cumplimientoA),
            ProgV0Pct = Round2(progV0Pct),
            AvanceFisicoPct = Round2(avanceFisicoPct),
            CumplimientoB = Round2(cumplimientoB),
            EficienciaGasto = Round2(eficiencia),
            Nota = MapNota(eficiencia)
        };
    }

    public async Task<(decimal PlanV0Usd, decimal GastoRealUsd)> GetFinancialMetricsAsync(int proyectoId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var planV0 = await _db.PmoV0Parciales
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= desde && x.Periodo <= hasta)
            .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;

        var gastoReal = await _db.PmoRealParciales
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= desde && x.Periodo <= hasta)
            .SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;

        return (planV0, gastoReal);
    }

    public async Task<(decimal ProgV0Pct, decimal AvanceFisicoPct)> GetPhysicalMetricsAsync(int proyectoId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        // En legado api_parcial viene en fracción (0-1). Aquí se escala a porcentaje (0-100).
        var progV0Pct = 100m * ((await _db.PmoAvFisicoV0s
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= desde && x.Periodo <= hasta)
            .SumAsync(x => (decimal?)x.ApiParcial, ct)) ?? 0m);

        var avanceFisicoPct = 100m * ((await _db.PmoAvFisicoReales
            .Where(x => x.ProyectoId == proyectoId && x.Periodo >= desde && x.Periodo <= hasta)
            .SumAsync(x => (decimal?)x.ApiParcial, ct)) ?? 0m);

        return (progV0Pct, avanceFisicoPct);
    }

    private static DateOnly EndOfMonth(DateOnly d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal MapNota(decimal eficienciaPct)
    {
        if (eficienciaPct < 90m) return 1m;
        if (eficienciaPct < 100m) return 2m;
        if (eficienciaPct < 105m) return 3m;
        if (eficienciaPct <= 110m) return 4m;
        return 5m;
    }

    private static FactorialEficienciaFilaDto BuildPromedio(IReadOnlyList<FactorialEficienciaFilaDto> rows)
    {
        if (rows.Count == 0)
            return new FactorialEficienciaFilaDto { Periodo = "PROMEDIO" };

        decimal Avg(Func<FactorialEficienciaFilaDto, decimal> sel) => rows.Average(sel);

        return new FactorialEficienciaFilaDto
        {
            Periodo = "PROMEDIO",
            MesIso = null,
            PlanV0Usd = Round2(rows.Sum(x => x.PlanV0Usd)),
            GastoRealUsd = Round2(rows.Sum(x => x.GastoRealUsd)),
            CumplimientoA = Round2(Avg(x => x.CumplimientoA)),
            ProgV0Pct = Round2(Avg(x => x.ProgV0Pct)),
            AvanceFisicoPct = Round2(Avg(x => x.AvanceFisicoPct)),
            CumplimientoB = Round2(Avg(x => x.CumplimientoB)),
            EficienciaGasto = Round2(Avg(x => x.EficienciaGasto)),
            Nota = Round2(Avg(x => x.Nota))
        };
    }

    private async Task<Dictionary<string, List<PmoAvFisicoFila>>> LoadRowsByTypeAsync(int proyectoId, CancellationToken ct)
    {
        var real = await _db.PmoAvFisicoReales.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Periodo).ThenBy(x => x.Id).Cast<PmoAvFisicoFila>().ToListAsync(ct);
        var npc = await _db.PmoAvFisicoNpcs.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Periodo).ThenBy(x => x.Id).Cast<PmoAvFisicoFila>().ToListAsync(ct);
        var poa = await _db.PmoAvFisicoPoas.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Periodo).ThenBy(x => x.Id).Cast<PmoAvFisicoFila>().ToListAsync(ct);
        var v0 = await _db.PmoAvFisicoV0s.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Periodo).ThenBy(x => x.Id).Cast<PmoAvFisicoFila>().ToListAsync(ct);
        var api = await _db.PmoAvFisicoApis.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Periodo).ThenBy(x => x.Id).Cast<PmoAvFisicoFila>().ToListAsync(ct);

        return new Dictionary<string, List<PmoAvFisicoFila>>(StringComparer.OrdinalIgnoreCase)
        {
            ["real"] = real,
            ["npc"] = npc,
            ["poa"] = poa,
            ["v0"] = v0,
            ["api"] = api
        };
    }

    private static IEnumerable<PmoAvFisicoFila> GetFilteredRows(IEnumerable<PmoAvFisicoFila> rows, DateOnly? desde, DateOnly? hasta, string? vector)
    {
        var q = rows;
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        if (!string.IsNullOrWhiteSpace(vector))
            q = q.Where(x => string.Equals(x.Vector?.Trim(), vector, StringComparison.OrdinalIgnoreCase));
        return q;
    }

    private static List<FactorialLineasBasesChartPointDto> BuildCurvaSData(
        IReadOnlyDictionary<string, List<PmoAvFisicoFila>> filteredByType,
        string tablaSel)
    {
        var source = tablaSel == "todas"
            ? TiposOrden.ToArray()
            : [tablaSel];

        var periodos = source
            .Where(filteredByType.ContainsKey)
            .SelectMany(t => filteredByType[t])
            .Select(r => r.Periodo)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        decimal GetVal(string tipo, DateOnly p)
        {
            if (!filteredByType.TryGetValue(tipo, out var rows)) return 0m;
            var row = rows.FirstOrDefault(x => x.Periodo == p);
            return row is null ? 0m : row.ApiAcum * 100m;
        }

        return periodos.Select(p => new FactorialLineasBasesChartPointDto
        {
            Periodo = p.ToString("yyyy-MM-dd"),
            Real = tablaSel is "todas" or "real" ? Round2(GetVal("real", p)) : 0m,
            V0 = tablaSel is "todas" or "v0" ? Round2(GetVal("v0", p)) : 0m,
            Npc = tablaSel is "todas" or "npc" ? Round2(GetVal("npc", p)) : 0m,
            Api = tablaSel is "todas" or "api" ? Round2(GetVal("api", p)) : 0m,
            Poa = tablaSel is "todas" or "poa" ? Round2(GetVal("poa", p)) : 0m
        }).ToList();
    }

    private static string? GetPeriodoLineaCorte(IReadOnlyList<FactorialLineasBasesChartPointDto> chart)
    {
        if (chart.Count == 0) return null;
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var exact = chart.FirstOrDefault(x => x.Periodo == hoy.ToString("yyyy-MM-dd"));
        if (exact is not null) return exact.Periodo;

        DateOnly ParsePeriodo(string s) => DateOnly.TryParse(s, out var d) ? d : new DateOnly(1900, 1, 1);
        return chart
            .OrderBy(x => Math.Abs((ParsePeriodo(x.Periodo).DayNumber - hoy.DayNumber)))
            .Select(x => x.Periodo)
            .FirstOrDefault();
    }

    private static FactorialLineasBasesCardsDto BuildCards(
        string tablaSel,
        IReadOnlyDictionary<string, List<PmoAvFisicoFila>> filteredByType,
        IReadOnlyList<FactorialLineasBasesChartPointDto> chart,
        string? periodoLineaCorte)
    {
        var chartAtCutoff = chart.FirstOrDefault(x => x.Periodo == periodoLineaCorte);

        FactorialLineasBasesCardItemDto Build(string tipo)
        {
            var disabled = tablaSel != "todas" && tablaSel != tipo;
            if (disabled)
                return new FactorialLineasBasesCardItemDto();

            var acum = tipo switch
            {
                "real" => chartAtCutoff?.Real ?? 0m,
                "npc" => chartAtCutoff?.Npc ?? 0m,
                "poa" => chartAtCutoff?.Poa ?? 0m,
                "v0" => chartAtCutoff?.V0 ?? 0m,
                "api" => chartAtCutoff?.Api ?? 0m,
                _ => 0m
            };

            decimal parcial = 0m;
            if (filteredByType.TryGetValue(tipo, out var rows) && !string.IsNullOrWhiteSpace(periodoLineaCorte))
            {
                if (DateOnly.TryParse(periodoLineaCorte, out var corte))
                    parcial = rows.Where(x => x.Periodo == corte).Sum(x => x.ApiParcial) * 100m;
            }

            return new FactorialLineasBasesCardItemDto
            {
                ApiAcum = Round2(acum),
                ApiParcial = Round2(parcial)
            };
        }

        return new FactorialLineasBasesCardsDto
        {
            Real = Build("real"),
            Npc = Build("npc"),
            Poa = Build("poa"),
            V0 = Build("v0"),
            Api = Build("api")
        };
    }

    private static List<FactorialLineasBasesTableItemDto> BuildTableRows(string tablaSel, IReadOnlyDictionary<string, List<PmoAvFisicoFila>> filteredByType)
    {
        var outRows = new List<FactorialLineasBasesTableItemDto>();

        IEnumerable<(string Tipo, PmoAvFisicoFila Row)> allRows = tablaSel == "todas"
            ? filteredByType.GetValueOrDefault("real", []).Select(x => ("REAL", x))
                .Concat(filteredByType.GetValueOrDefault("v0", []).Select(x => ("V0", x)))
                .Concat(filteredByType.GetValueOrDefault("npc", []).Select(x => ("NPC", x)))
                .Concat(filteredByType.GetValueOrDefault("api", []).Select(x => ("API", x)))
                .Concat(filteredByType.GetValueOrDefault("poa", []).Select(x => ("POA", x)))
            : filteredByType.GetValueOrDefault(tablaSel, []).Select(x => (tablaSel.ToUpperInvariant(), x));

        foreach (var (tipo, row) in allRows)
        {
            outRows.Add(new FactorialLineasBasesTableItemDto
            {
                Tipo = tablaSel == "todas" ? tipo : null,
                Id = row.Id,
                Vector = row.Vector,
                Periodo = row.Periodo.ToString("yyyy-MM-dd"),
                IeParcial = row.IeParcial,
                IeAcumulado = row.IeAcumulado,
                EmParcial = row.EmParcial,
                EmAcumulado = row.EmAcumulado,
                MoParcial = row.MoParcial,
                MoAcumulado = row.MoAcumulado,
                ApiParcial = row.ApiParcial,
                ApiAcum = row.ApiAcum
            });
        }

        return outRows
            .OrderBy(x => x.Periodo, StringComparer.Ordinal)
            .ThenBy(x => x.Tipo, StringComparer.Ordinal)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeTabla(string? tablaVisualizar)
    {
        var t = (tablaVisualizar ?? "todas").Trim().ToLowerInvariant();
        return t is "real" or "npc" or "poa" or "v0" or "api" or "todas" ? t : "todas";
    }

    private static string PeriodoEtiquetaMes(DateOnly d)
    {
        var mes = d.Month switch
        {
            1 => "ENE", 2 => "FEB", 3 => "MAR", 4 => "ABR", 5 => "MAY", 6 => "JUN",
            7 => "JUL", 8 => "AGO", 9 => "SEP", 10 => "OCT", 11 => "NOV", 12 => "DIC",
            _ => "???"
        };
        return $"{mes}/{d.Year}";
    }

    private static string TextoNotaPredictividad(int nota) => nota switch
    {
        >= 5 => "Excelente predictividad",
        4 => "Buena predictividad",
        3 => "Predictividad aceptable",
        2 => "Predictividad baja",
        _ => "Predictividad crítica"
    };

    private static FactorialPredictividadHistorialPromediosDto BuildHistorialPromedios(
        IReadOnlyList<FactorialPredictividadHistorialMensualDto> rows)
    {
        if (rows.Count == 0)
            return new FactorialPredictividadHistorialPromediosDto();

        var sumaProy = rows.Sum(x => x.FinProyeccion);
        var sumaReal = rows.Sum(x => x.FinReal);
        var desvFin = sumaProy != 0m ? ((sumaReal - sumaProy) / sumaProy) * 100m : 0m;

        return new FactorialPredictividadHistorialPromediosDto
        {
            FisPromedioProyeccion = Round2(rows.Average(x => x.FisProyeccion)),
            FisPromedioReal = Round2(rows.Average(x => x.FisReal)),
            FisPromedioDesviacionPct = Round2(rows.Average(x => x.FisDesviacionPct)),
            FisPromedioPrecision = Round2(rows.Average(x => x.FisPrecision)),
            FisPromedioNota = Round2(rows.Average(x => (decimal)x.FisNota)),
            FinSumaProyeccion = Round2(sumaProy),
            FinSumaReal = Round2(sumaReal),
            FinDesviacionPct = Round2(desvFin),
            FinPromedioPrecision = Round2(rows.Average(x => x.FinPrecision)),
            FinPromedioNota = Round2(rows.Average(x => (decimal)x.FinNota))
        };
    }

    private static int CalcularNotaFinanciera(decimal desviacion)
    {
        if (desviacion < 0m) return 5;
        if (desviacion <= 10m) return 5;
        if (desviacion <= 15m) return 3;
        return 1;
    }

    private static int CalcularNotaFisica(decimal desviacion)
    {
        var abs = Math.Abs(desviacion);
        if (abs <= 5m) return 5;
        if (abs <= 10m) return 4;
        if (abs <= 20m) return 3;
        if (abs <= 50m) return 2;
        return 1;
    }

    private static string MapDescripcionVersion(DateOnly month)
    {
        return month.Month switch
        {
            1 => $"Version L Diciembre {month.Year - 1}",
            2 => $"Version A Enero {month.Year}",
            3 => $"Version B Febrero {month.Year}",
            4 => $"Version C Marzo {month.Year}",
            5 => $"Version D Abril {month.Year}",
            6 => $"Version E Mayo {month.Year}",
            7 => $"Version F Junio {month.Year}",
            8 => $"Version G Julio {month.Year}",
            9 => $"Version H Agosto {month.Year}",
            10 => $"Version I Septiembre {month.Year}",
            11 => $"Version J Octubre {month.Year}",
            12 => $"Version K Noviembre {month.Year}",
            _ => $"Version L Diciembre {month.Year - 1}"
        };
    }

    public async Task<(bool Ok, string Message, int Inserted, int Deleted)> ImportPredictividadAsync(
        int proyectoId,
        IReadOnlyList<JsonElement> rows,
        CancellationToken ct)
    {
        await EnsurePredictividadTableAsync(ct);
        if (rows.Count == 0)
            return (false, "El archivo no contenía filas.", 0, 0);

        var deleted = await _db.PmoPredictividades.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct);
        var list = new List<PmoPredictividad>();

        foreach (var el in rows)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var map = NormalizeJsonObject(el);
            var periodoPred = ParseDateFlexible(GetMap(map, "periodo_prediccion"));
            var periodoCierre = ParseDateFlexible(GetMap(map, "periodo_cierre_real"));
            if (!periodoPred.HasValue) continue;

            var porcentaje = ParsePctFlexible(GetMap(map, "porcentaje_predicido"));
            var valorReal = ParsePctFlexible(GetMap(map, "valor_real_porcentaje"));

            list.Add(new PmoPredictividad
            {
                ProyectoId = proyectoId,
                CentroCostoId = null,
                PeriodoPrediccion = periodoPred.Value,
                PorcentajePredicido = porcentaje,
                PeriodoCierreReal = periodoCierre,
                ValorRealPorcentaje = valorReal
            });
        }

        if (list.Count == 0)
            return (false, "No se encontraron filas válidas para importar (periodo_prediccion requerido).", 0, deleted);

        _db.PmoPredictividades.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return (true, $"Importación predictividad completada.", list.Count, deleted);
    }

    private async Task EnsurePredictividadTableAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS predictividad (
                id_predictivo serial PRIMARY KEY,
                proyecto_id integer NOT NULL,
                id integer NULL,
                periodo_prediccion date NOT NULL,
                porcentaje_predicido numeric(12,6) NOT NULL DEFAULT 0,
                periodo_cierre_real date NULL,
                valor_real_porcentaje numeric(12,6) NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_predictividad_proyecto_id ON predictividad(proyecto_id);
            CREATE INDEX IF NOT EXISTS ix_predictividad_periodo_prediccion ON predictividad(periodo_prediccion);
        ", ct);
    }

    private static Dictionary<string, string> NormalizeJsonObject(JsonElement el)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in el.EnumerateObject())
        {
            var k = p.Name.Trim().ToLowerInvariant().Replace(" ", "_");
            var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
            map[k] = v.Trim();
        }
        return map;
    }

    private static string? GetMap(Dictionary<string, string> map, string key)
    {
        if (map.TryGetValue(key, out var v)) return v;
        return null;
    }

    private static DateOnly? ParseDateFlexible(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 20000 && serial < 60000)
        {
            var epoch = new DateTime(1899, 12, 30);
            var d = epoch.AddDays(Math.Round(serial));
            return DateOnly.FromDateTime(d);
        }
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
        if (DateOnly.TryParseExact(s, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)) return d2;
        if (DateOnly.TryParse(s, out var d3)) return d3;
        return null;
    }

    private static decimal ParsePctFlexible(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var s = raw.Trim().Replace("%", "").Replace(",", ".");
        if (!decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return 0m;
        if (v > 0m && v < 1m) v *= 100m;
        return v;
    }
}
