using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.PmoFinance;

namespace RazorIdentity.Services;

/// <summary>
/// Lectura e importación de datos PMO en PostgreSQL (EF). Sustituye al host PHP/MySQL cuando <see cref="Configuration.PycApiSettings.UsePhpEndpoints"/> es <c>false</c>.
/// </summary>
public class PmoFinancePostgresStore
{
    private readonly ApplicationDbContext _db;

    public static readonly string[] TablasAvFisico =
    {
        "av_fisico_real", "av_fisico_npc", "av_fisico_poa", "av_fisico_v0", "av_fisico_api"
    };

    public PmoFinancePostgresStore(ApplicationDbContext db) => _db = db;

    public static bool EsTablaAvFisico(string? tabla) =>
        !string.IsNullOrWhiteSpace(tabla) && TablasAvFisico.Contains(tabla.Trim(), StringComparer.OrdinalIgnoreCase);

    public async Task<JsonElement> GetAvFisicoDatosJsonAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct)
    {
        var t = tabla.Trim();
        var desdeD = ParseDayOrMonthFirstDay(desde);
        var hastaD = ParseDayOrMonthFirstDay(hasta);

        object[] data = t.ToLowerInvariant() switch
        {
            "av_fisico_real" => await QueryAvFisicoAsync<PmoAvFisicoReal>(proyectoId, desdeD, hastaD, ct),
            "av_fisico_npc" => await QueryAvFisicoAsync<PmoAvFisicoNpc>(proyectoId, desdeD, hastaD, ct),
            "av_fisico_poa" => await QueryAvFisicoAsync<PmoAvFisicoPoa>(proyectoId, desdeD, hastaD, ct),
            "av_fisico_v0" => await QueryAvFisicoAsync<PmoAvFisicoV0>(proyectoId, desdeD, hastaD, ct),
            "av_fisico_api" => await QueryAvFisicoAsync<PmoAvFisicoApi>(proyectoId, desdeD, hastaD, ct),
            _ => Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(data, PmoJson.Options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public async Task<(bool Ok, string Message, int Inserted)> ImportAvFisicoAsync(string tabla, int proyectoId, IReadOnlyList<JsonElement> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return (true, "El archivo no contenía filas para procesar.", 0);

        var t = tabla.Trim().ToLowerInvariant();
        if (!EsTablaAvFisico(t))
            return (false, "La tabla de avance físico indicada no es válida.", 0);

        var now = DateTime.UtcNow;
        var n = t switch
        {
            "av_fisico_real" => await ImportAvFisicoListAsync<PmoAvFisicoReal>(rows, proyectoId, now, _db.PmoAvFisicoReales, ct),
            "av_fisico_npc" => await ImportAvFisicoListAsync<PmoAvFisicoNpc>(rows, proyectoId, now, _db.PmoAvFisicoNpcs, ct),
            "av_fisico_poa" => await ImportAvFisicoListAsync<PmoAvFisicoPoa>(rows, proyectoId, now, _db.PmoAvFisicoPoas, ct),
            "av_fisico_v0" => await ImportAvFisicoListAsync<PmoAvFisicoV0>(rows, proyectoId, now, _db.PmoAvFisicoV0s, ct),
            "av_fisico_api" => await ImportAvFisicoListAsync<PmoAvFisicoApi>(rows, proyectoId, now, _db.PmoAvFisicoApis, ct),
            _ => 0
        };

        if (n == 0)
            return (false, "Ninguna fila pudo importarse. Verifique id, periodo/fecha y vector.", 0);
        return (true, $"Importación en «{t}»: {n} fila(s). Las demás tablas av_fisico_* no se modifican.", n);
    }

    private async Task<int> ImportAvFisicoListAsync<T>(IReadOnlyList<JsonElement> rows, int proyectoId, DateTime now, DbSet<T> set, CancellationToken ct)
        where T : PmoAvFisicoFila, new()
    {
        await set.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct);

        var list = new List<T>();
        foreach (var el in rows)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var row = MapAvFisicoRow<T>(el, proyectoId, now);
            if (row != null) list.Add(row);
        }

        if (list.Count == 0) return 0;
        set.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return list.Count;
    }

    private static T? MapAvFisicoRow<T>(JsonElement el, int proyectoIdDefault, DateTime now) where T : PmoAvFisicoFila, new()
    {
        var id = CleanAvFisicoId(GetAvFisicoRowId(el, typeof(T).Name));
        if (string.IsNullOrWhiteSpace(id)) return null;

        var periodo = GetDateOnly(el, "periodo", "fecha", "Periodo", "Fecha");
        if (!periodo.HasValue) return null;

        var vector = GetString(el, null, "vector", "Vector")?.Trim();
        if (string.IsNullOrEmpty(vector)) vector = "GEN";

        var row = new T
        {
            Id = id.Length > 20 ? id[..20] : id,
            ProyectoId = GetIntProp(el, proyectoIdDefault, "proyecto_id", "proyectoId", "ProyectoId"),
            Periodo = periodo.Value,
            Vector = vector.Length > 10 ? vector[..10] : vector,
            IeParcial = GetDecimal(el, "ie_parcial", "ie", "IeParcial"),
            IeAcumulado = GetDecimal(el, "ie_acumulado", "ie_acum", "IeAcumulado"),
            EmParcial = GetDecimal(el, "em_parcial", "em", "EmParcial"),
            EmAcumulado = GetDecimal(el, "em_acumulado", "em_acum", "EmAcumulado"),
            MoParcial = GetDecimal(el, "mo_parcial", "mo", "MoParcial"),
            MoAcumulado = GetDecimal(el, "mo_acumulado", "mo_acum", "MoAcumulado"),
            ApiParcial = GetDecimal(el, "api_parcial", "api", "ApiParcial"),
            ApiAcum = GetDecimal(el, "api_acum", "api_acumulado", "ApiAcum"),
            CreatedAt = now,
            UpdatedAt = now
        };
        return row;
    }

    private static string? GetAvFisicoRowId(JsonElement el, string typeName)
    {
        // typeName: PmoAvFisicoReal → id_av_real, etc.
        return typeName switch
        {
            nameof(PmoAvFisicoReal) => GetString(el, null, "id", "id_av_real", "idAvReal", "Id"),
            nameof(PmoAvFisicoNpc) => GetString(el, null, "id", "id_av_npc", "idAvNpc", "Id"),
            nameof(PmoAvFisicoPoa) => GetString(el, null, "id", "id_av_poa", "idAvPoa", "Id"),
            nameof(PmoAvFisicoV0) => GetString(el, null, "id", "id_av_v0", "id_av_vo", "idAvV0", "idAvVo", "Id"),
            nameof(PmoAvFisicoApi) => GetString(el, null, "id", "id_av_api", "idAvApi", "Id"),
            _ => GetString(el, null, "id", "Id")
        };
    }

    private static string? CleanAvFisicoId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        var s = id.Trim();
        s = Regex.Replace(s, @"\s+(real|npc|poa|v0|vo|api)\s*$", "", RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private async Task<object[]> QueryAvFisicoAsync<T>(int proyectoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct) where T : PmoAvFisicoFila
    {
        var q = _db.Set<T>().AsNoTracking().Where(x => x.ProyectoId == proyectoId);
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        var list = await q.OrderBy(x => x.Periodo).ThenBy(x => x.Id).ToListAsync(ct);
        return list.Select(x => new
        {
            id = x.Id,
            proyecto_id = x.ProyectoId,
            periodo = x.Periodo.ToString("yyyy-MM-dd"),
            vector = x.Vector,
            ie_parcial = x.IeParcial,
            ie_acumulado = x.IeAcumulado,
            em_parcial = x.EmParcial,
            em_acumulado = x.EmAcumulado,
            mo_parcial = x.MoParcial,
            mo_acumulado = x.MoAcumulado,
            api_parcial = x.ApiParcial,
            api_acum = x.ApiAcum
        }).Cast<object>().ToArray();
    }

    private static DateOnly? ParseDayOrMonthFirstDay(string? yyyymmOrIso)
    {
        if (string.IsNullOrWhiteSpace(yyyymmOrIso)) return null;
        var s = yyyymmOrIso.Trim();
        if (s.Length >= 10 && DateOnly.TryParse(s.AsSpan(0, 10), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return ParseMonthFirstDay(s);
    }

    public async Task<JsonElement> GetDatosJsonAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct)
    {
        var desdeD = ParseMonthFirstDay(desde);
        var hastaD = ParseMonthFirstDay(hasta);

        object[] data = tabla switch
        {
            "real_parcial" => await QueryVectorsAsync<PmoRealParcial>(proyectoId, desdeD, hastaD, ct),
            "real_acumulado" => await QueryVectorsAsync<PmoRealAcumulado>(proyectoId, desdeD, hastaD, ct),
            "v0_parcial" => await QueryVectorsAsync<PmoV0Parcial>(proyectoId, desdeD, hastaD, ct),
            "v0_acumulada" => await QueryVectorsAsync<PmoV0Acumulada>(proyectoId, desdeD, hastaD, ct),
            "npc_parcial" => await QueryVectorsAsync<PmoNpcParcial>(proyectoId, desdeD, hastaD, ct),
            "npc_acumulado" => await QueryVectorsAsync<PmoNpcAcumulado>(proyectoId, desdeD, hastaD, ct),
            "api_parcial" => await QueryVectorsAsync<PmoApiParcial>(proyectoId, desdeD, hastaD, ct),
            "api_acumulada" => await QueryVectorsAsync<PmoApiAcumulada>(proyectoId, desdeD, hastaD, ct),
            "financiero_sap" => await QuerySapAsync(proyectoId, desdeD, hastaD, ct),
            "vc_project_9c" => await Query9cAsync(proyectoId, desdeD, hastaD, ct),
            _ => Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(data, PmoJson.Options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public async Task<(bool Ok, string Message, int Inserted)> ImportVectorsAsync(string modoId, int proyectoId, IReadOnlyList<JsonElement> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return (true, "El archivo no contenía filas para procesar.", 0);

        await DeleteVectorsForProyectoAsync(modoId, proyectoId, ct);

        var n = modoId switch
        {
            "real_parcial" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoRealParcial()), _db.PmoRealParciales, ct),
            "real_acumulado" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoRealAcumulado()), _db.PmoRealAcumulados, ct),
            "v0_parcial" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoV0Parcial()), _db.PmoV0Parciales, ct),
            "v0_acumulada" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoV0Acumulada()), _db.PmoV0Acumuladas, ct),
            "npc_parcial" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoNpcParcial()), _db.PmoNpcParciales, ct),
            "npc_acumulado" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoNpcAcumulado()), _db.PmoNpcAcumulados, ct),
            "api_parcial" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoApiParcial()), _db.PmoApiParciales, ct),
            "api_acumulada" => await ImportVectorListAsync(rows, el => MapVector(el, proyectoId, new PmoApiAcumulada()), _db.PmoApiAcumuladas, ct),
            _ => 0
        };

        if (modoId is not ("real_parcial" or "real_acumulado" or "v0_parcial" or "v0_acumulada" or "npc_parcial" or "npc_acumulado" or "api_parcial" or "api_acumulada"))
            return (false, "El tipo de vector seleccionado no es válido para la importación.", 0);
        if (n == 0)
            return (false, "Ninguna fila pudo importarse. Verifique las columnas CENTRO_COSTO, PERIODO y MONTO.", 0);
        return (true, "Importación completada.", n);
    }

    private async Task<int> ImportVectorListAsync<T>(IReadOnlyList<JsonElement> rows, Func<JsonElement, T?> map, DbSet<T> set, CancellationToken ct) where T : PmoVectorFinancieroRow
    {
        var list = new List<T>();
        foreach (var el in rows)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var row = map(el);
            if (row != null) list.Add(row);
        }
        if (list.Count == 0) return 0;
        set.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return list.Count;
    }

    public async Task<(bool Ok, string Message, int Inserted)> ImportSapAsync(int proyectoId, IReadOnlyList<JsonElement> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return (true, "El archivo no contenía filas para procesar.", 0);
        var now = DateTime.UtcNow;
        var list = new List<PmoFinancieroSap>();
        foreach (var el in rows)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var idSap = GetString(el, null, "id_sap", "idSap", "IdSap") ?? "";
            if (string.IsNullOrWhiteSpace(idSap)) continue;
            list.Add(new PmoFinancieroSap
            {
                IdSap = idSap.Trim(),
                ProyectoId = GetIntProp(el, proyectoId, "proyecto_id", "proyectoId", "ProyectoId"),
                CentroCostoNombre = GetString(el, null, "centro_costo_nombre", "centroCostoNombre"),
                VersionSap = GetString(el, null, "version_sap", "versionSap", "VersionSap"),
                Descripcion = GetString(el, null, "descripcion", "Descripcion"),
                GrupoVersion = GetString(el, null, "grupo_version", "grupoVersion"),
                Periodo = GetDateOnly(el, "periodo", "Periodo"),
                Mo = GetDecimal(el, "MO", "mo"),
                Ic = GetDecimal(el, "IC", "ic"),
                Em = GetDecimal(el, "EM", "em"),
                Ie = GetDecimal(el, "IE", "ie"),
                Sc = GetDecimal(el, "SC", "sc"),
                Ad = GetDecimal(el, "AD", "ad"),
                Cl = GetDecimal(el, "CL", "cl"),
                Ct = GetDecimal(el, "CT", "ct"),
                FechaCreacion = now,
                FechaActualizacion = now
            });
        }

        if (list.Count == 0) return (false, "No se encontraron filas SAP con identificador (id_sap) válido.", 0);
        var ids = list.Select(x => x.IdSap).Distinct(StringComparer.Ordinal).ToList();
        var existing = await _db.PmoFinancieroSaps
            .Where(x => x.ProyectoId == proyectoId && ids.Contains(x.IdSap))
            .ToListAsync(ct);
        var byId = existing.ToDictionary(x => x.IdSap, StringComparer.Ordinal);
        var inserted = 0;
        var updated = 0;
        foreach (var row in list)
        {
            if (byId.TryGetValue(row.IdSap, out var ex))
            {
                ex.CentroCostoNombre = row.CentroCostoNombre;
                ex.VersionSap = row.VersionSap;
                ex.Descripcion = row.Descripcion;
                ex.GrupoVersion = row.GrupoVersion;
                ex.Periodo = row.Periodo;
                ex.Mo = row.Mo;
                ex.Ic = row.Ic;
                ex.Em = row.Em;
                ex.Ie = row.Ie;
                ex.Sc = row.Sc;
                ex.Ad = row.Ad;
                ex.Cl = row.Cl;
                ex.Ct = row.Ct;
                ex.FechaActualizacion = now;
                updated++;
            }
            else
            {
                _db.PmoFinancieroSaps.Add(row);
                inserted++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return (true, $"Importación SAP completada. Insertadas: {inserted}. Actualizadas: {updated}.", inserted + updated);
    }

    public async Task<int> DeleteSapAsync(int proyectoId, string? versionSap, string? descripcion, CancellationToken ct)
    {
        var q = _db.PmoFinancieroSaps.Where(x => x.ProyectoId == proyectoId);
        if (!string.IsNullOrWhiteSpace(versionSap))
        {
            var v = versionSap.Trim();
            q = q.Where(x => x.VersionSap == v);
        }
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            var d = descripcion.Trim();
            q = q.Where(x => x.Descripcion == d);
        }
        return await q.ExecuteDeleteAsync(ct);
    }

    public async Task<(bool Ok, string Message, int Inserted)> Import9cAsync(int proyectoId, IReadOnlyList<JsonElement> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return (true, "El archivo no contenía filas para procesar.", 0);
        var now = DateTime.UtcNow;
        var list = new List<PmoVcProject9c>();
        foreach (var el in rows)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var idC9 = GetString(el, null, "id_c9", "idC9", "IdC9") ?? "";
            if (string.IsNullOrWhiteSpace(idC9)) continue;
            var per = GetDateOnly(el, "periodo", "Periodo");
            if (!per.HasValue) continue;
            list.Add(new PmoVcProject9c
            {
                IdC9 = idC9.Trim(),
                ProyectoId = GetIntProp(el, proyectoId, "proyecto_id", "proyectoId"),
                Periodo = per.Value,
                CatVp = GetString(el, "", "cat_vp", "catVp", "CatVp") ?? "",
                MonedaBase = Math.Max(1, (int)GetDecimal(el, "moneda_base", "monedaBase", "MonedaBase")),
                Base = GetDecimal(el, "base", "Base"),
                Cambio = GetDecimal(el, "cambio", "Cambio"),
                Control = GetDecimal(el, "control", "Control"),
                Tendencia = GetDecimal(el, "tendencia", "Tendencia"),
                Eat = GetDecimal(el, "eat", "Eat", "EAT"),
                Compromiso = GetDecimal(el, "compromiso", "Compromiso"),
                Incurrido = GetDecimal(el, "incurrido", "Incurrido"),
                Financiero = GetDecimal(el, "financiero", "Financiero"),
                PorComprometer = GetDecimal(el, "por_comprometer", "porComprometer", "PorComprometer"),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (list.Count == 0) return (false, "No se encontraron filas 9C con identificador (id_c9) y período válidos.", 0);

        await _db.PmoVcProject9cs.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct);
        _db.PmoVcProject9cs.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return (true, "Importación 9C completada.", list.Count);
    }

    private async Task DeleteVectorsForProyectoAsync(string modoId, int proyectoId, CancellationToken ct)
    {
        switch (modoId)
        {
            case "real_parcial": await _db.PmoRealParciales.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "real_acumulado": await _db.PmoRealAcumulados.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "v0_parcial": await _db.PmoV0Parciales.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "v0_acumulada": await _db.PmoV0Acumuladas.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "npc_parcial": await _db.PmoNpcParciales.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "npc_acumulado": await _db.PmoNpcAcumulados.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "api_parcial": await _db.PmoApiParciales.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
            case "api_acumulada": await _db.PmoApiAcumuladas.Where(x => x.ProyectoId == proyectoId).ExecuteDeleteAsync(ct); break;
        }
    }

    private static T? MapVector<T>(JsonElement el, int proyectoIdDefault, T row) where T : PmoVectorFinancieroRow
    {
        var periodo = GetDateOnly(el, "periodo", "Periodo");
        if (!periodo.HasValue) return null;

        row.ProyectoId = GetIntProp(el, proyectoIdDefault, "proyecto_id", "proyectoId", "ProyectoId");
        row.CentroCosto = GetString(el, "", "centro_costo", "centroCosto", "CentroCosto") ?? "";
        row.Periodo = periodo.Value;
        row.Tipo = GetString(el, "", "tipo", "Tipo") ?? "";
        row.CatVp = GetString(el, "", "cat_vp", "catVp", "CatVp") ?? "";
        row.DetalleFactorial = GetString(el, "", "detalle_factorial", "detalleFactorial", "DetalleFactorial") ?? "";
        row.Monto = GetDecimal(el, "monto", "Monto");
        return row;
    }

    private async Task<object[]> QueryVectorsAsync<T>(int proyectoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct) where T : PmoVectorFinancieroRow
    {
        var q = _db.Set<T>().AsNoTracking().Where(x => x.ProyectoId == proyectoId);
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        var list = await q.OrderBy(x => x.Periodo).ThenBy(x => x.Id).ToListAsync(ct);
        return list.Select(x => new
        {
            x.Id,
            x.ProyectoId,
            centroCosto = x.CentroCosto,
            periodo = x.Periodo.ToString("yyyy-MM-dd"),
            x.Tipo,
            catVp = x.CatVp,
            detalleFactorial = x.DetalleFactorial,
            x.Monto
        }).Cast<object>().ToArray();
    }

    private async Task<object[]> QuerySapAsync(int proyectoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var q = _db.PmoFinancieroSaps.AsNoTracking().Where(x => x.ProyectoId == proyectoId);
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        var list = await q.OrderBy(x => x.Periodo).ThenBy(x => x.IdSap).ToListAsync(ct);
        return list.Select(x => new
        {
            x.Id,
            idSap = x.IdSap,
            x.ProyectoId,
            centroCostoNombre = x.CentroCostoNombre,
            versionSap = x.VersionSap,
            descripcion = x.Descripcion,
            grupoVersion = x.GrupoVersion,
            periodo = x.Periodo?.ToString("yyyy-MM-dd"),
            x.Mo,
            x.Ic,
            x.Em,
            x.Ie,
            x.Sc,
            x.Ad,
            x.Cl,
            x.Ct
        }).Cast<object>().ToArray();
    }

    private async Task<object[]> Query9cAsync(int proyectoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var q = _db.PmoVcProject9cs.AsNoTracking().Where(x => x.ProyectoId == proyectoId);
        if (desde.HasValue) q = q.Where(x => x.Periodo >= desde.Value);
        if (hasta.HasValue) q = q.Where(x => x.Periodo <= hasta.Value);
        var list = await q.OrderBy(x => x.Periodo).ThenBy(x => x.IdC9).ToListAsync(ct);
        return list.Select(x => new
        {
            idC9 = x.IdC9,
            x.ProyectoId,
            periodo = x.Periodo.ToString("yyyy-MM-dd"),
            catVp = x.CatVp,
            monedaBase = x.MonedaBase,
            x.Base,
            x.Cambio,
            x.Control,
            x.Tendencia,
            x.Eat,
            x.Compromiso,
            x.Incurrido,
            x.Financiero,
            porComprometer = x.PorComprometer
        }).Cast<object>().ToArray();
    }

    private static DateOnly? ParseMonthFirstDay(string? yyyymmOrIso)
    {
        if (string.IsNullOrWhiteSpace(yyyymmOrIso)) return null;
        var s = yyyymmOrIso.Trim();
        if (s.Length >= 10 && DateOnly.TryParse(s.AsSpan(0, 10), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (s.Length >= 7 && DateOnly.TryParse(s[..7] + "-01", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
            return d2;
        return null;
    }

    private static string? GetString(JsonElement el, string? fallback, params string[] names)
    {
        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(el, n, out var p))
            {
                if (p.ValueKind == JsonValueKind.String) return p.GetString();
                if (p.ValueKind == JsonValueKind.Number) return p.GetRawText();
            }
        }
        return fallback;
    }

    private static decimal GetDecimal(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(el, n, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
                if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2)) return d2;
            }
        }
        return 0m;
    }

    private static DateOnly? GetDateOnly(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(el, n, out var p))
            {
                var s = p.ValueKind == JsonValueKind.String ? p.GetString() : p.GetRawText();
                if (!string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s.AsSpan(0, Math.Min(10, s!.Length)), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return d;
            }
        }
        return null;
    }

    private static int GetIntProp(JsonElement el, int defaultVal, params string[] names)
    {
        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(el, n, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
                if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var j)) return j;
            }
        }
        return defaultVal;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement el, string name, out JsonElement value)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static class PmoJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
