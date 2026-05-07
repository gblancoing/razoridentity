using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RazorIdentity.Configuration;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RazorIdentity.Pages;

/// <summary>
/// PMO (Planning &amp; Control): proyectos desde RIT; datos financieros en PostgreSQL (predeterminado) o vía API PYC legada si <c>PycApi:UsePhpEndpoints</c> es <c>true</c>.
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken(Order = 1000)]
public class PmoModel : PageModel
{
    private readonly IPycApiClient _pyc;
    private readonly PmoFinancePostgresStore _pmoFinancePg;
    private readonly PmoReporte1CurvaSEvmService _reporte1CurvaSEvm;
    private readonly PmoFactorialService _pmoFactorial;
    private readonly IRitApiClient _ritApi;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly PycImportSettings _import;
    private readonly PycApiSettings _pycApi;
    private readonly ILogger<PmoModel> _logger;

    public PmoModel(
        IPycApiClient pyc,
        PmoFinancePostgresStore pmoFinancePg,
        PmoReporte1CurvaSEvmService reporte1CurvaSEvm,
        PmoFactorialService pmoFactorial,
        IRitApiClient ritApi,
        UserManager<IdentityUser> userManager,
        IOptions<PycImportSettings> importOpts,
        IOptions<PycApiSettings> pycApiOpts,
        ILogger<PmoModel> logger)
    {
        _pyc = pyc;
        _pmoFinancePg = pmoFinancePg;
        _reporte1CurvaSEvm = reporte1CurvaSEvm;
        _pmoFactorial = pmoFactorial;
        _ritApi = ritApi;
        _userManager = userManager;
        _import = importOpts.Value;
        _pycApi = pycApiOpts.Value;
        _logger = logger;
    }

    public string? ErrorCarga { get; set; }
    /// <summary>Si la URL trae <c>proyectoId</c> no autorizado para el usuario.</summary>
    public string? ErrorProyectoParametro { get; set; }
    /// <summary>Proyecto elegido en el paso previo (<c>/PMO?proyectoId=</c>). Si es null, solo se muestra la pantalla de selección.</summary>
    public int? ProyectoActivoId { get; set; }
    /// <summary><c>general</c> = MDG y jerarquía N1–N3 (entrada); <c>proyecto</c> = selector; <c>vectores</c> = KPI/importación.</summary>
    public string PmoSeccion { get; set; } = "general";
    /// <summary>True cuando hay proyecto y la sección activa es vectores (barra de modos, grid, scripts).</summary>
    public bool MostrarModuloPmo => ProyectoActivoId.HasValue &&
                                    string.Equals(PmoSeccion, "vectores", StringComparison.OrdinalIgnoreCase);

    /// <summary>Resumen del proyecto: KPI visual N1–N3 (EVM / curva desde PostgreSQL).</summary>
    public bool MostrarSeccionResumen => ProyectoActivoId.HasValue &&
        string.Equals(PmoSeccion, "resumen", StringComparison.OrdinalIgnoreCase);

    /// <summary>Control físico — líneas base Real/Proyectado (curva S, KPI, importación).</summary>
    public bool MostrarModuloControlFisico => ProyectoActivoId.HasValue &&
        string.Equals(PmoSeccion, "control_fisico", StringComparison.OrdinalIgnoreCase);
    public bool MostrarModuloFactorial => ProyectoActivoId.HasValue &&
        string.Equals(PmoSeccion, "factorial", StringComparison.OrdinalIgnoreCase);

    /// <summary>Título para secciones aún sin contenido (Control físico legacy placeholder, etc.).</summary>
    public string? PlaceholderTituloSeccion =>
        PmoSeccion switch
        {
            "control_fisico" => "Control Fisico",
            "factorial" => "Factorial",
            "gestion_proyecto" => "Gestión de Proyecto",
            "gestion_primavera" => "Gestión Primavera",
            "info_pmo" => "Info PMO",
            _ => null
        };

    /// <summary>Entrada al módulo: gobierno de datos maestros (MDG) e indicadores N1–N3 (PMI / PMO).</summary>
    public bool MostrarSeccionGeneral => string.Equals(PmoSeccion, "general", StringComparison.OrdinalIgnoreCase);

    /// <summary>Muestra tarjeta “en preparación” para secciones distintas de proyecto y vectores.</summary>
    public bool MostrarSeccionPlaceholder => ProyectoActivoId.HasValue
        && PlaceholderTituloSeccion != null
        && !MostrarModuloPmo
        && !MostrarModuloControlFisico
        && !MostrarModuloFactorial
        && !string.Equals(PmoSeccion, "proyecto", StringComparison.OrdinalIgnoreCase)
        && !MostrarSeccionGeneral;
    /// <summary>Nombre del proyecto activo (listado RIT).</summary>
    public string? ProyectoActivoNombre =>
        ProyectoActivoId is { } id ? ProyectosDisponibles.FirstOrDefault(p => p.Id == id)?.Nombre : null;
    /// <summary>Proyectos que el usuario puede elegir (RIT). País/región se definen en Ajustes, no en esta pantalla.</summary>
    public IReadOnlyList<ProyectoApi> ProyectosDisponibles { get; set; } = Array.Empty<ProyectoApi>();
    public string FinancierosCatalogJson { get; set; } = "{}";

    public async Task OnGetAsync(int? proyectoId, string? seccion, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            ErrorCarga = "No se pudo identificar al usuario.";
            return;
        }

        var isSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
        try
        {
            ProyectosDisponibles = await LoadProyectosPmoAsync(userId, isSuperAdmin, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RIT proyectos para PMO");
            ErrorCarga = "No se pudo cargar proyectos desde RIT. Compruebe que Rit_Api esté en ejecución y que el usuario tenga app PMO asignada con proyecto (Ajustes).";
        }

        if (proyectoId is > 0)
        {
            if (ProyectosDisponibles.Any(p => p.Id == proyectoId.Value))
                ProyectoActivoId = proyectoId;
            else
                ErrorProyectoParametro = "El proyecto indicado no está disponible para su usuario o no existe en el listado cargado.";
        }

        var sec = seccion?.Trim();
        if (string.IsNullOrEmpty(sec))
            PmoSeccion = ProyectoActivoId.HasValue ? "resumen" : "general";
        else
        {
            var n = sec.ToLowerInvariant().Replace('-', '_');
            PmoSeccion = n switch
            {
                "general" => "general",
                "proyecto" => "proyecto",
                "resumen" => "resumen",
                "vectores" => "vectores",
                "control_fisico" => "control_fisico",
                "factorial" => "factorial",
                "gestion_proyecto" => "gestion_proyecto",
                "gestion_primavera" => "gestion_primavera",
                "info_pmo" => "info_pmo",
                _ => ProyectoActivoId.HasValue ? "resumen" : "proyecto"
            };
        }

        if (!string.Equals(PmoSeccion, "proyecto", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(PmoSeccion, "general", StringComparison.OrdinalIgnoreCase)
            && !ProyectoActivoId.HasValue)
            PmoSeccion = "proyecto";

        if (MostrarModuloPmo || MostrarSeccionResumen)
        {
            FinancierosCatalogJson = JsonSerializer.Serialize(new
            {
                modosVector = PycFinancierosCatalog.ModosVectorBarraPmo.Select(m => new { id = m.Id, etiqueta = m.Etiqueta, tabla = m.Tabla }).ToList(),
                modosAnalisis = PycFinancierosCatalog.ModosAnalisis.Select(m => new { id = m.Id, etiqueta = m.Etiqueta, tabla = m.Tabla }).ToList(),
                categoriasKpi = PycFinancierosCatalog.CategoriasKpi.ToList(),
                codigoSap = PycFinancierosCatalog.CodigoSapACategoria
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        else
            FinancierosCatalogJson = "{}";
    }

    private async Task<IReadOnlyList<ProyectoApi>> LoadProyectosPmoAsync(string userId, bool isSuperAdmin, CancellationToken ct)
    {
        var all = await _ritApi.GetListAsync<ProyectoApi>("api/Proyectos", ct) ?? new List<ProyectoApi>();
        if (isSuperAdmin)
            return all.OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        var apps = await _ritApi.GetListAsync<AppApi>("api/Apps", ct) ?? new List<AppApi>();
        var pmoApp = apps.FirstOrDefault(a => IsPmoApp(a.Nombre));
        var ids = new HashSet<int>();

        if (pmoApp != null)
        {
            var uaList = await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp", ct) ?? new List<UsuarioAppApi>();
            foreach (var ua in uaList.Where(x => x.UserId == userId && x.AppId == pmoApp.Id && x.ProyectoId is > 0))
                ids.Add(ua.ProyectoId!.Value);
        }

        if (ids.Count == 0 && pmoApp != null)
        {
            var one = await GetProyectoIdPreferenteAsync(userId, pmoApp.Id, ct);
            if (one.HasValue)
                ids.Add(one.Value);
        }

        if (ids.Count == 0)
        {
            var fromCentro = await GetProyectoIdFromCentroAsync(userId, ct);
            if (fromCentro.HasValue)
                ids.Add(fromCentro.Value);
        }

        return all.Where(p => ids.Contains(p.Id)).OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>UsuariosApp.ProyectoId o, si no viene, proyecto del centro asignado (mismo criterio que RitWeb).</summary>
    private async Task<int?> GetProyectoIdPreferenteAsync(string userId, int pmoAppId, CancellationToken ct)
    {
        try
        {
            var list = await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp", ct);
            var ua = list?.FirstOrDefault(x => x.UserId == userId && x.AppId == pmoAppId);
            if (ua?.ProyectoId != null && ua.ProyectoId.Value > 0)
                return ua.ProyectoId;

            return await GetProyectoIdFromCentroAsync(userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PMO proyecto preferente");
            return null;
        }
    }

    private async Task<int?> GetProyectoIdFromCentroAsync(string userId, CancellationToken ct)
    {
        try
        {
            var usuariosCentro = await _ritApi.GetListAsync<UsuarioCentroApi>("api/UsuariosCentro", ct);
            var asignacion = usuariosCentro?.FirstOrDefault(uc => string.Equals(uc.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (asignacion == null || asignacion.CentroCostoId <= 0)
                return null;

            var centros = await _ritApi.GetListAsync<CentroCostoApi>("api/CentrosCosto", ct);
            var centro = centros?.FirstOrDefault(c => c.Id == asignacion.CentroCostoId);
            if (centro != null && centro.ProyectoId > 0)
                return centro.ProyectoId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PMO proyecto desde centro");
            return null;
        }
    }

    private static bool IsPmoApp(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;
        return string.Equals(nombre, "PMO", StringComparison.OrdinalIgnoreCase)
               || string.Equals(nombre, "P & C", StringComparison.OrdinalIgnoreCase)
               || string.Equals(nombre, "P&C", StringComparison.OrdinalIgnoreCase)
               || string.Equals(nombre, "PYC", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IActionResult> OnGetDatosFinancierosAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct)
    {
        try
        {
            if (!_pycApi.UsePhpEndpoints)
            {
                var el = await _pmoFinancePg.GetDatosJsonAsync(proyectoId, tabla, desde, hasta, ct);
                return Content(el.GetRawText(), "application/json");
            }

            var elRemote = await _pyc.GetDatosFinancierosAsync(proyectoId, tabla, desde, hasta, ct);
            if (elRemote == null)
                return new JsonResult(Array.Empty<object>());
            return new JsonResult(elRemote.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PYC datos financieros {Tabla}", tabla);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    /// <summary>Curva S acumulada desde parciales, panel EVM, tabla por categoría VP, cascadas e IEAC/ECD (solo PostgreSQL local).</summary>
    public async Task<IActionResult> OnGetReporte1CurvaSEvmAsync(int proyectoId, string? desde, string? hasta, string fechaSeguimiento, CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { error = "El reporte Curva S / EVM requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (string.IsNullOrWhiteSpace(fechaSeguimiento) || !TryParseFechaPmo(fechaSeguimiento.Trim(), out var fechaSeg))
            return new JsonResult(new { error = "Indique fechaSeguimiento válida (yyyy-MM o yyyy-MM-dd)." }) { StatusCode = 400 };

        DateOnly? desdeD = string.IsNullOrWhiteSpace(desde) ? null : TryParseFechaPmo(desde.Trim(), out var d0) ? d0 : null;
        DateOnly? hastaD = string.IsNullOrWhiteSpace(hasta) ? null : TryParseFechaPmo(hasta.Trim(), out var d1) ? d1 : null;
        if (desde is { Length: > 0 } && !desdeD.HasValue)
            return new JsonResult(new { error = "Parámetro desde inválido." }) { StatusCode = 400 };
        if (hasta is { Length: > 0 } && !hastaD.HasValue)
            return new JsonResult(new { error = "Parámetro hasta inválido." }) { StatusCode = 400 };

        try
        {
            var pack = await _reporte1CurvaSEvm.BuildPackAsync(proyectoId, desdeD, hastaD, fechaSeg, ct);
            return new JsonResult(pack, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reporte1 Curva S / EVM proyecto {ProyectoId}", proyectoId);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnGetReporte1CascadaDetalleAsync(int proyectoId, string categoria, string tipo, decimal monto, string? desde, string? hasta, CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { error = "El análisis detallado de cascada requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (string.IsNullOrWhiteSpace(categoria))
            return new JsonResult(new { error = "Parámetro categoria requerido." }) { StatusCode = 400 };

        var tipoNorm = (tipo ?? "").Trim().ToUpperInvariant();
        if (tipoNorm != "V0" && tipoNorm != "API")
            return new JsonResult(new { error = "Parámetro tipo inválido. Use V0 o API." }) { StatusCode = 400 };

        DateOnly? desdeD = string.IsNullOrWhiteSpace(desde) ? null : TryParseFechaPmo(desde.Trim(), out var d0) ? d0 : null;
        DateOnly? hastaD = string.IsNullOrWhiteSpace(hasta) ? null : TryParseFechaPmo(hasta.Trim(), out var d1) ? d1 : null;
        if (desde is { Length: > 0 } && !desdeD.HasValue)
            return new JsonResult(new { error = "Parámetro desde inválido." }) { StatusCode = 400 };
        if (hasta is { Length: > 0 } && !hastaD.HasValue)
            return new JsonResult(new { error = "Parámetro hasta inválido." }) { StatusCode = 400 };

        try
        {
            var payload = await _reporte1CurvaSEvm.BuildCascadaDetalleAsync(proyectoId, categoria.Trim(), tipoNorm, monto, desdeD, hastaD, ct);
            return new JsonResult(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reporte1 cascada detalle proyecto {ProyectoId} cat={Categoria} tipo={Tipo}", proyectoId, categoria, tipoNorm);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnGetFactorialEficienciaAsync(int proyectoId, string? mes, CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { error = "Factorial/Eficiencia requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (proyectoId <= 0)
            return new JsonResult(new { error = "ProyectoId inválido." }) { StatusCode = 400 };

        DateOnly corte;
        if (string.IsNullOrWhiteSpace(mes))
        {
            var now = DateTime.Now;
            corte = new DateOnly(now.Year, now.Month, 1).AddMonths(-1);
        }
        else if (!TryParseFechaPmo(mes.Trim(), out corte))
            return new JsonResult(new { error = "Parámetro mes inválido (yyyy-MM o yyyy-MM-dd)." }) { StatusCode = 400 };
        else
            corte = new DateOnly(corte.Year, corte.Month, 1);

        try
        {
            var payload = await _pmoFactorial.BuildEficienciaGastoAsync(proyectoId, corte, ct);
            return new JsonResult(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Factorial/Eficiencia proyecto {ProyectoId}", proyectoId);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnGetFactorialLineasBasesAsync(
        int proyectoId,
        string? desde,
        string? hasta,
        string? vector,
        string? tabla,
        CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { error = "Factorial/Líneas Bases requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (proyectoId <= 0)
            return new JsonResult(new { error = "ProyectoId inválido." }) { StatusCode = 400 };

        DateOnly? desdeD = string.IsNullOrWhiteSpace(desde) ? null : TryParseFechaPmo(desde.Trim(), out var d0) ? d0 : null;
        DateOnly? hastaD = string.IsNullOrWhiteSpace(hasta) ? null : TryParseFechaPmo(hasta.Trim(), out var d1) ? d1 : null;
        if (desde is { Length: > 0 } && !desdeD.HasValue)
            return new JsonResult(new { error = "Parámetro desde inválido (yyyy-MM o yyyy-MM-dd)." }) { StatusCode = 400 };
        if (hasta is { Length: > 0 } && !hastaD.HasValue)
            return new JsonResult(new { error = "Parámetro hasta inválido (yyyy-MM o yyyy-MM-dd)." }) { StatusCode = 400 };

        try
        {
            var payload = await _pmoFactorial.BuildLineasBasesAsync(proyectoId, desdeD, hastaD, vector, tabla, ct);
            return new JsonResult(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Factorial/Líneas Bases proyecto {ProyectoId}", proyectoId);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnGetFactorialPredictividadAsync(
        int proyectoId,
        string? hasta20,
        CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { error = "Factorial/Predictividad requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (proyectoId <= 0)
            return new JsonResult(new { error = "ProyectoId inválido." }) { StatusCode = 400 };

        DateOnly corte;
        if (string.IsNullOrWhiteSpace(hasta20))
            corte = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        else if (!TryParseFechaPmo(hasta20.Trim(), out corte))
            return new JsonResult(new { error = "Parámetro hasta20 inválido (yyyy-MM o yyyy-MM-dd)." }) { StatusCode = 400 };
        else
            corte = new DateOnly(corte.Year, corte.Month, 1);

        try
        {
            var payload = await _pmoFactorial.BuildPredictividadAsync(proyectoId, corte, ct);
            return new JsonResult(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Factorial/Predictividad proyecto {ProyectoId}", proyectoId);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnPostFactorialImportPredictividadAsync([FromBody] PmoFactorialPredictividadImportDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_import.Clave) ||
            !string.Equals(dto.Clave?.Trim(), _import.Clave.Trim(), StringComparison.Ordinal))
            return new JsonResult(new { ok = false, error = "No tiene autorización para importar. Verifique la clave indicada por el administrador." }) { StatusCode = 403 };

        if (dto.ProyectoId <= 0)
            return new JsonResult(new { ok = false, error = "ProyectoId inválido." }) { StatusCode = 400 };

        if (dto.Rows.ValueKind != JsonValueKind.Array)
            return new JsonResult(new { ok = false, error = "Rows debe ser un arreglo." }) { StatusCode = 400 };

        var rows = new List<JsonElement>();
        foreach (var r in dto.Rows.EnumerateArray())
            rows.Add(r.Clone());

        try
        {
            var (ok, message, inserted, deleted) = await _pmoFactorial.ImportPredictividadAsync(dto.ProyectoId, rows, ct);
            if (!ok)
                return new JsonResult(new { ok = false, error = message }) { StatusCode = 400 };
            return new JsonResult(new { ok = true, message, inserted, deleted });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Factorial import predictividad proyecto {ProyectoId}", dto.ProyectoId);
            return new JsonResult(new { ok = false, error = ex.Message }) { StatusCode = 502 };
        }
    }

    private static bool TryParseFechaPmo(string s, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length == 7 && s[4] == '-' && DateOnly.TryParse(s + "-01", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (s.Length >= 10 && DateOnly.TryParse(s.AsSpan(0, 10), CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        return false;
    }

    public async Task<IActionResult> OnGetDatosAvFisicoAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct)
    {
        if (!PmoFinancePostgresStore.EsTablaAvFisico(tabla))
            return new JsonResult(new { error = "Tabla de avance físico no válida." }) { StatusCode = 400 };

        try
        {
            if (!_pycApi.UsePhpEndpoints)
            {
                var el = await _pmoFinancePg.GetAvFisicoDatosJsonAsync(proyectoId, tabla, desde, hasta, ct);
                return Content(el.GetRawText(), "application/json");
            }

            var remote = await _pyc.GetAvFisicoDatosAsync(proyectoId, tabla, desde, hasta, ct);
            if (!remote.HasValue)
                return Content("[]", "application/json");
            return Content(remote.Value.GetRawText(), "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PMO avance físico {Tabla}", tabla);
            return new JsonResult(new { error = ex.Message }) { StatusCode = 502 };
        }
    }

    public async Task<IActionResult> OnPostEliminarFinancieroSapAsync([FromBody] PmoEliminarSapDto dto, CancellationToken ct)
    {
        if (_pycApi.UsePhpEndpoints)
            return new JsonResult(new { ok = false, error = "La eliminación SAP requiere datos en PostgreSQL (UsePhpEndpoints: false)." }) { StatusCode = 400 };

        if (string.IsNullOrWhiteSpace(_import.Clave) ||
            !string.Equals(dto.Clave?.Trim(), _import.Clave.Trim(), StringComparison.Ordinal))
            return new JsonResult(new { ok = false, error = "No tiene autorización para eliminar. Verifique la clave indicada por el administrador." }) { StatusCode = 403 };

        if (dto.ProyectoId <= 0)
            return new JsonResult(new { ok = false, error = "ProyectoId inválido." }) { StatusCode = 400 };

        if (string.IsNullOrWhiteSpace(dto.VersionSap) && string.IsNullOrWhiteSpace(dto.Descripcion))
            return new JsonResult(new { ok = false, error = "Seleccione al menos versión o descripción para eliminar." }) { StatusCode = 400 };

        try
        {
            var deleted = await _pmoFinancePg.DeleteSapAsync(dto.ProyectoId, dto.VersionSap, dto.Descripcion, ct);
            return new JsonResult(new { ok = true, deleted, message = $"Registros eliminados: {deleted}." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eliminar SAP proyecto {ProyectoId}", dto.ProyectoId);
            return new JsonResult(new { ok = false, error = ex.Message }) { StatusCode = 502 };
        }
    }

    public IActionResult OnPostVerificarClaveImportacion([FromBody] PycClaveImportDto dto)
    {
        if (string.IsNullOrWhiteSpace(_import.Clave))
            return new JsonResult(new { ok = false, error = "El servicio de importación no está configurado. Contacte al administrador del sistema." }) { StatusCode = 503 };

        var ok = string.Equals(dto.Clave?.Trim(), _import.Clave.Trim(), StringComparison.Ordinal);
        if (!ok)
            return new JsonResult(new { ok = false, error = "La clave de importación no es válida." }) { StatusCode = 403 };

        return new JsonResult(new { ok = true });
    }

    public async Task<IActionResult> OnPostImportarFinancieroAsync([FromBody] PycImportUiDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_import.Clave) ||
            !string.Equals(dto.Clave?.Trim(), _import.Clave.Trim(), StringComparison.Ordinal))
            return new JsonResult(new { ok = false, error = "No tiene autorización para importar. Verifique la clave indicada por el administrador." }) { StatusCode = 403 };

        if (dto.Rows.ValueKind != JsonValueKind.Array)
            return new JsonResult(new { ok = false, error = "El formato del archivo enviado no es válido." }) { StatusCode = 400 };

        try
        {
            var rows = new List<JsonElement>();
            foreach (var r in dto.Rows.EnumerateArray())
                rows.Add(r.Clone());

            if (!_pycApi.UsePhpEndpoints)
            {
                var (ok, message, inserted) = (false, "", 0);
                if (string.Equals(dto.Kind, "vector", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(dto.ModoId))
                        return new JsonResult(new { ok = false, error = "Debe seleccionar un modo de vector antes de importar." }) { StatusCode = 400 };
                    (ok, message, inserted) = await _pmoFinancePg.ImportVectorsAsync(dto.ModoId.Trim(), dto.ProyectoId, rows, ct);
                }
                else if (string.Equals(dto.Kind, "sap", StringComparison.OrdinalIgnoreCase))
                    (ok, message, inserted) = await _pmoFinancePg.ImportSapAsync(dto.ProyectoId, rows, ct);
                else if (string.Equals(dto.Kind, "c9", StringComparison.OrdinalIgnoreCase))
                    (ok, message, inserted) = await _pmoFinancePg.Import9cAsync(dto.ProyectoId, rows, ct);
                else if (string.Equals(dto.Kind, "av_fisico", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(dto.ModoId) || !PmoFinancePostgresStore.EsTablaAvFisico(dto.ModoId))
                        return new JsonResult(new { ok = false, error = "Debe indicar una tabla válida (av_fisico_real, …, av_fisico_api)." }) { StatusCode = 400 };
                    (ok, message, inserted) = await _pmoFinancePg.ImportAvFisicoAsync(dto.ModoId.Trim(), dto.ProyectoId, rows, ct);
                }
                else
                    return new JsonResult(new { ok = false, error = "El tipo de importación indicado no es reconocido." }) { StatusCode = 400 };

                if (!ok)
                    return new JsonResult(new { ok = false, error = message }) { StatusCode = 400 };

                var payload = JsonSerializer.Serialize(new { ok = true, message, inserted },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                return Content(payload, "application/json");
            }

            if (string.Equals(dto.Kind, "av_fisico", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dto.ModoId) || !PmoFinancePostgresStore.EsTablaAvFisico(dto.ModoId))
                    return new JsonResult(new { ok = false, error = "Debe indicar una tabla válida para avance físico." }) { StatusCode = 400 };
                var bodyAv = new PycAvFisicoImportPostBody
                {
                    ProyectoId = dto.ProyectoId,
                    Tabla = dto.ModoId.Trim(),
                    Rows = rows
                };
                var respAv = await _pyc.PostImportacionAsync("api/importaciones/importar_av_real_proyectado.php", bodyAv, ct);
                return Content(respAv, "application/json");
            }

            var segment = ResolveImportSegment(dto.Kind, dto.ModoId);
            var path = $"api/importaciones/{segment}";
            var body = new PycImportPostBody { ProyectoId = dto.ProyectoId, Rows = rows };
            var resp = await _pyc.PostImportacionAsync(path, body, ct);
            return Content(resp, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PYC import kind={Kind} modo={Modo}", dto.Kind, dto.ModoId);
            return new JsonResult(new { ok = false, error = ex.Message }) { StatusCode = 502 };
        }
    }

    private string ResolveImportSegment(string kind, string? modoId)
    {
        var php = _pycApi.UsePhpEndpoints;
        if (string.Equals(kind, "vector", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(modoId))
            return php
                ? PycFinancierosCatalog.ImportacionSegmentForVector(modoId)
                : PycFinancierosCatalog.ImportacionSegmentForVectorAspNet(modoId);
        if (string.Equals(kind, "sap", StringComparison.OrdinalIgnoreCase))
            return php ? PycFinancierosCatalog.ImportSapSegment : PycFinancierosCatalog.ImportSapSegmentAspNet;
        if (string.Equals(kind, "c9", StringComparison.OrdinalIgnoreCase))
            return php ? PycFinancierosCatalog.Import9cSegment : PycFinancierosCatalog.Import9cSegmentAspNet;
        throw new ArgumentException("Tipo de importación no reconocido.");
    }
}

public sealed class PmoEliminarSapDto
{
    public int ProyectoId { get; set; }
    public string? Clave { get; set; }
    public string? VersionSap { get; set; }
    public string? Descripcion { get; set; }
}

public sealed class PmoFactorialPredictividadImportDto
{
    public int ProyectoId { get; set; }
    public string? Clave { get; set; }
    public JsonElement Rows { get; set; }
}
