using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Api;
using RazorIdentity.Models.ApiRitweb;
using RazorIdentity.Services;
using System.Text;

namespace RazorIdentity.Pages
{
    [Authorize]
    public class RitWebModel : PageModel
    {
        private readonly IRitApiClient _ritApi;
        private readonly IApiRitwebClient _ritwebApi;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RitWebModel> _logger;
        private readonly IConfiguration _configuration;

        public RitWebModel(
            IRitApiClient ritApi,
            IApiRitwebClient ritwebApi,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext db,
            ILogger<RitWebModel> logger,
            IConfiguration configuration)
        {
            _ritApi = ritApi;
            _ritwebApi = ritwebApi;
            _userManager = userManager;
            _db = db;
            _logger = logger;
            _configuration = configuration;
        }

        public string Tab { get; set; } = "dashboard";
        public string? CurrentUserEmail { get; set; }
        public string? CurrentUserInitials { get; set; }
        /// <summary>Nombre completo del usuario actual (UserProfile.FullName). Para rellenar "Profesional notificado".</summary>
        public string? CurrentUserFullName { get; set; }
        /// <summary>Cargo del usuario actual (UserProfile.Cargo). Para rellenar "Profesional notificado".</summary>
        public string? CurrentUserCargo { get; set; }
        public string? ErrorApi { get; set; }

        public List<TipoEventoDto> TiposEvento { get; set; } = new();
        public List<TipoEventoDetalleDto> TiposEventoDetalle { get; set; } = new();
        public List<SectorDto> Sectores { get; set; } = new();
        public List<TipoDocumentoRespaldoDto> TiposDocumentoRespaldo { get; set; } = new();
        public List<TipoAlertaDto> TiposAlerta { get; set; } = new();
        public List<CriticidadDto> Criticidades { get; set; } = new();
        public List<TurnoApi> Turnos { get; set; } = new();
        public List<EventoDto> Eventos { get; set; } = new();
        /// <summary>UserId -> nombre para mostrar en tabla Ver Eventos (resuelto desde UserProfile/Identity).</summary>
        public Dictionary<string, string> EventoUsuarioNombres { get; set; } = new();
        /// <summary>UserId -> nombre de disciplina (desde RIT API UsuariosDisciplina + Disciplinas).</summary>
        public Dictionary<string, string> EventoUsuarioDisciplinaNombres { get; set; } = new();
        /// <summary>UserId -> turno desde ficha del usuario (RIT_API), para mostrar cuando el evento no trae Turno desde API_Ritweb.</summary>
        public Dictionary<string, string> EventoUsuarioTurno { get; set; } = new();
        /// <summary>Clave "ProyectoId_EmpresaColaboradoraId" -> nombre empresa (desde RIT API).</summary>
        public Dictionary<string, string> EventoEmpresaNombres { get; set; } = new();
        /// <summary>Filtro por tipo de evento en Ver Eventos (null = todos).</summary>
        public int? TipoEventoIdFilter { get; set; }
        /// <summary>Eventos filtrados por TipoEventoIdFilter para la tabla Ver Eventos.</summary>
        public IReadOnlyList<EventoDto> FilteredEventos => TipoEventoIdFilter.HasValue && Eventos != null
            ? Eventos.Where(e => e.TipoEventoId == TipoEventoIdFilter.Value).ToList()
            : (IReadOnlyList<EventoDto>)(Eventos ?? new List<EventoDto>());

        /// <summary>JSON de eventos abiertos con ubicación para la pestaña Georeferencia.</summary>
        public string GeoEventosJson { get; set; } = "[]";
        /// <summary>Cantidad de eventos abiertos que no tienen lat/long (para aviso en mapa).</summary>
        public int GeoAbiertosSinUbicacion { get; set; }

        /// <summary>Para Kanban: solo eventos tipo Alerta, máx. 100. Abiertos sin comentarios en foro.</summary>
        public List<EventoDto> KanbanAperturados { get; set; } = new();
        /// <summary>Para Kanban: alertas abiertas con al menos un comentario en foro de seguimiento.</summary>
        public List<EventoDto> KanbanEnProceso { get; set; } = new();
        /// <summary>Para Kanban: alertas cerradas.</summary>
        public List<EventoDto> KanbanCerrados { get; set; } = new();
        /// <summary>Criticidad Id → nombre para Kanban (y vistas que lo usen).</summary>
        public Dictionary<int, string> CriticidadesDict { get; set; } = new();
        /// <summary>Sector Id → nombre para Kanban (y vistas que lo usen).</summary>
        public Dictionary<int, string> SectoresDict { get; set; } = new();

        /// <summary>Disciplinas desde RIT API para dropdowns en Tipos evento, Tipos evento detalle y Tipos documento.</summary>
        public List<DisciplinaApi> Disciplinas { get; set; } = new();
        /// <summary>Proyectos desde RIT API para dropdown Sectores.</summary>
        public List<ProyectoApi> Proyectos { get; set; } = new();

        /// <summary>Id del ítem en edición (muestra formulario prellenado).</summary>
        public int? EditTipoEventoId { get; set; }
        public int? EditEventoId { get; set; }
        /// <summary>Evento cargado para edición o detalle (GET api/Eventos/{id}).</summary>
        public EventoDto? EventoEditar { get; set; }
        public int? EditSectorId { get; set; }
        public int? EditTipoDocumentoId { get; set; }
        public int? EditTipoEventoDetalleId { get; set; }
        public int? EditTipoAlertaId { get; set; }
        public int? EditCriticidadId { get; set; }
        public int? EditTurnoId { get; set; }

        public string? MensajeAjustes { get; set; }
        public string? MensajeAjustesError { get; set; }
        /// <summary>ID del evento recién registrado (desde TempData) para mostrar mensaje de éxito.</summary>
        public int? RegistroEventoIdSuccess { get; set; }

        /// <summary>True si el usuario tiene rol Super_admin: puede elegir cualquier proyecto y ver Rit de todos.</summary>
        public bool IsSuperAdmin { get; set; }
        /// <summary>True si el usuario es rol "usuario" (ni Super_admin ni Admin): solo ve Dashboard, Ver eventos y Kanban; eventos filtrados por misma empresa y proyecto.</summary>
        public bool EsSoloUsuario { get; set; }
        /// <summary>Proyecto asignado al usuario para RitWeb (desde UsuariosApp.ProyectoId). Usado por defecto al registrar evento si no es super_admin.</summary>
        public int? UserProyectoId { get; set; }
        /// <summary>Proyecto preseleccionado en la página (query proyectoId para super_admin o UserProyectoId para el resto).</summary>
        public int? ProyectoIdContext { get; set; }
        /// <summary>Turno del usuario (ficha en RIT_API: GET api/Usuarios/{userId}/Ficha).</summary>
        public string? TurnoUsuario { get; set; }

        public async Task<IActionResult> OnGetAsync(
            string? tab,
            int? editTipoEventoId,
            int? editEventoId,
            int? editSectorId,
            int? editTipoDocumentoId,
            int? editTipoEventoDetalleId,
            int? editTipoAlertaId,
            int? editCriticidadId,
            int? editTurnoId,
            int? proyectoId,
            int? tipoEventoId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            CurrentUserEmail = user.Email ?? user.UserName ?? "Usuario";
            CurrentUserInitials = CurrentUserEmail.Length >= 2
                ? CurrentUserEmail.Substring(0, 2).ToUpperInvariant()
                : "U";

            var profile = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            CurrentUserFullName = profile?.FullName?.Trim();
            CurrentUserCargo = profile?.Cargo?.Trim();

            IsSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
            EsSoloUsuario = !IsSuperAdmin && !User.IsInRole("Admin");

            if (!string.IsNullOrWhiteSpace(tab))
                Tab = tab.ToLowerInvariant();

            // Usuario (rol usuario): solo Dashboard, Ver eventos, Kanban
            var tabsSoloAdminOSuperAdmin = new[] { "registrarevento", "georeferencia" };
            if (EsSoloUsuario && tabsSoloAdminOSuperAdmin.Contains(Tab ?? ""))
            {
                TempData["MensajeAjustesError"] = "Su rol solo tiene acceso a Dashboard, Ver eventos y Kanban.";
                return RedirectToPage("/RitWeb", new { tab = "dashboard" });
            }

            // Ajustes (tipos evento, sectores, documentos, alertas, criticidades, turnos): solo Super_admin
            var tabsSoloSuperAdmin = new[] { "tiposevento", "tiposeventodetalle", "sectores", "tiposdocumento", "tiposalerta", "criticidades", "turnos" };
            if (!IsSuperAdmin && tabsSoloSuperAdmin.Contains(Tab ?? ""))
            {
                TempData["MensajeAjustesError"] = "Solo usuarios con rol Super_admin pueden acceder a Ajustes.";
                return RedirectToPage("/RitWeb", new { tab = "eventos" });
            }

            EditTipoEventoId = editTipoEventoId;
            EditEventoId = editEventoId;
            TipoEventoIdFilter = tipoEventoId;
            EditSectorId = editSectorId;
            EditTipoDocumentoId = editTipoDocumentoId;
            EditTipoEventoDetalleId = editTipoEventoDetalleId;
            EditTipoAlertaId = editTipoAlertaId;
            EditCriticidadId = editCriticidadId;
            EditTurnoId = editTurnoId;
            MensajeAjustes = TempData["MensajeAjustes"] as string;
            MensajeAjustesError = TempData["MensajeAjustesError"] as string;
            RegistroEventoIdSuccess = TempData["RegistroEventoId"] is int id && id > 0 ? id : (int?)null;

            // Autorización: solo usuarios con acceso a la app RitWeb
            var ritWebAppId = await GetRitWebAppIdAsync();
            if (ritWebAppId == null)
            {
                TempData["Error"] = "No se encontró la aplicación RitWeb en el mantenedor. Contacte al administrador.";
                return RedirectToPage("/App");
            }

            var userAppIds = await GetUserAppIdsAsync(user.Id);
            if (!userAppIds.Contains(ritWebAppId.Value))
            {
                TempData["Error"] = "No tiene acceso a la aplicación RitWeb. Solicite asignación en Ajustes → Usuarios por app.";
                return RedirectToPage("/App");
            }

            UserProyectoId = await GetUserProyectoIdForRitWebAsync(user.Id, ritWebAppId.Value);
            if (IsSuperAdmin && proyectoId.HasValue && proyectoId.Value > 0)
                ProyectoIdContext = proyectoId.Value;
            else if (!IsSuperAdmin)
                ProyectoIdContext = UserProyectoId;

            // Ficha del usuario (Turno, Cargo, Nombre, Email) desde RIT_API
            try
            {
                var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{user.Id}/Ficha");
                if (ficha != null)
                {
                    TurnoUsuario = ficha.Turno;
                    if (!string.IsNullOrWhiteSpace(ficha.NombreCompleto)) CurrentUserFullName = ficha.NombreCompleto.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.NCargo)) CurrentUserCargo = ficha.NCargo.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.Email)) CurrentUserEmail = ficha.Email.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar ficha del usuario desde RIT_API");
            }

            await CargarDatosRitwebAsync();
            return Page();
        }

        /// <summary>Obtiene el proyecto asignado al usuario para la app RitWeb: primero UsuariosApp.ProyectoId; si no hay, el proyecto del centro de costo asignado (UsuariosCentro → CentroCosto.ProyectoId).</summary>
        private async Task<int?> GetUserProyectoIdForRitWebAsync(string userId, int ritWebAppId)
        {
            try
            {
                var list = await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp");
                var ua = list?.FirstOrDefault(x => x.UserId == userId && x.AppId == ritWebAppId);
                if (ua?.ProyectoId != null && ua.ProyectoId.Value > 0)
                    return ua.ProyectoId;

                // Fallback: proyecto del centro de costo asignado al usuario (Ajustes → Usuarios por centro)
                var usuariosCentro = await _ritApi.GetListAsync<UsuarioCentroApi>("api/UsuariosCentro");
                var asignacion = usuariosCentro?.FirstOrDefault(uc => string.Equals(uc.UserId, userId, StringComparison.OrdinalIgnoreCase));
                if (asignacion == null || asignacion.CentroCostoId <= 0) return null;

                var centros = await _ritApi.GetListAsync<CentroCostoApi>("api/CentrosCosto");
                var centro = centros?.FirstOrDefault(c => c.Id == asignacion.CentroCostoId);
                if (centro != null && centro.ProyectoId > 0)
                    return centro.ProyectoId;

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener proyecto asignado para RitWeb");
                return null;
            }
        }

        private async Task<int?> GetRitWebAppIdAsync()
        {
            try
            {
                var apps = await _ritApi.GetListAsync<AppApi>("api/Apps");
                var ritweb = apps.FirstOrDefault(a => string.Equals(a.Nombre, "RitWeb", StringComparison.OrdinalIgnoreCase));
                return ritweb?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar apps para RitWeb");
                return null;
            }
        }

        private async Task<List<int>> GetUserAppIdsAsync(string userId)
        {
            try
            {
                var list = await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp");
                return list.Where(ua => ua.UserId == userId).Select(ua => ua.AppId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar UsuariosApp");
                return new List<int>();
            }
        }

        private async Task CargarDatosRitwebAsync()
        {
            ErrorApi = null;
            try
            {
                switch (Tab)
                {
                    case "tiposevento":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        await CargarDisciplinasAsync();
                        break;
                    case "tiposeventodetalle":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        TiposEventoDetalle = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
                        await CargarDisciplinasAsync();
                        break;
                    case "sectores":
                        Sectores = await _ritwebApi.GetListAsync<SectorDto>("api/Sectores");
                        await CargarProyectosAsync();
                        break;
                    case "tiposdocumento":
                        TiposDocumentoRespaldo = await _ritwebApi.GetListAsync<TipoDocumentoRespaldoDto>("api/TiposDocumentoRespaldo");
                        await CargarDisciplinasAsync();
                        break;
                    case "tiposalerta":
                        TiposAlerta = await _ritwebApi.GetListAsync<TipoAlertaDto>("api/TiposAlerta");
                        break;
                    case "criticidades":
                        Criticidades = await _ritwebApi.GetListAsync<CriticidadDto>("api/Criticidades");
                        break;
                    case "turnos":
                        Turnos = await _ritApi.GetListAsync<TurnoApi>("api/Turnos");
                        break;
                    case "registrarevento":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        TiposEventoDetalle = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
                        Sectores = await _ritwebApi.GetListAsync<SectorDto>("api/Sectores");
                        TiposDocumentoRespaldo = await _ritwebApi.GetListAsync<TipoDocumentoRespaldoDto>("api/TiposDocumentoRespaldo");
                        TiposAlerta = await _ritwebApi.GetListAsync<TipoAlertaDto>("api/TiposAlerta");
                        Criticidades = await _ritwebApi.GetListAsync<CriticidadDto>("api/Criticidades");
                        await CargarProyectosAsync();
                        if (EditEventoId.HasValue && EditEventoId.Value > 0)
                            EventoEditar = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{EditEventoId.Value}");
                        break;
                    case "eventos":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        TiposEventoDetalle = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
                        Sectores = await _ritwebApi.GetListAsync<SectorDto>("api/Sectores");
                        TiposAlerta = await _ritwebApi.GetListAsync<TipoAlertaDto>("api/TiposAlerta");
                        Criticidades = await _ritwebApi.GetListAsync<CriticidadDto>("api/Criticidades");
                        TiposDocumentoRespaldo = await _ritwebApi.GetListAsync<TipoDocumentoRespaldoDto>("api/TiposDocumentoRespaldo");
                        await CargarProyectosAsync();
                        Eventos = await _ritwebApi.GetListAsync<EventoDto>("api/Eventos");
                        await FiltrarEventosPorDisciplinaUsuarioAsync();
                        if (EditEventoId.HasValue && EditEventoId.Value > 0)
                            EventoEditar = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{EditEventoId.Value}");
                        await CargarEventoUsuarioYNombresEmpresaAsync();
                        break;
                    case "georeferencia":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        Eventos = await _ritwebApi.GetListAsync<EventoDto>("api/Eventos");
                        await FiltrarEventosPorDisciplinaUsuarioAsync();
                        var eventosList = Eventos ?? new List<EventoDto>();
                        var abiertos = eventosList.Where(e => e.RequiereCierre && !e.FechaCierre.HasValue).ToList();
                        var abiertosConUbicacion = eventosList
                            .Where(e => e.RequiereCierre && !e.FechaCierre.HasValue && e.Latitude.HasValue && e.Longitude.HasValue)
                            .Select(e => new
                            {
                                e.Id,
                                Lat = (double)e.Latitude!.Value,
                                Lng = (double)e.Longitude!.Value,
                                TipoNombre = TiposEvento?.FirstOrDefault(t => t.Id == e.TipoEventoId)?.Nombre ?? "Evento",
                                e.FechaCreacion
                            })
                            .ToList();
                        GeoEventosJson = System.Text.Json.JsonSerializer.Serialize(abiertosConUbicacion);
                        GeoAbiertosSinUbicacion = abiertos.Count - abiertosConUbicacion.Count;
                        break;
                    case "kanban":
                        TiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                        TiposEventoDetalle = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
                        Sectores = await _ritwebApi.GetListAsync<SectorDto>("api/Sectores");
                        TiposAlerta = await _ritwebApi.GetListAsync<TipoAlertaDto>("api/TiposAlerta");
                        Criticidades = await _ritwebApi.GetListAsync<CriticidadDto>("api/Criticidades");
                        Eventos = await _ritwebApi.GetListAsync<EventoDto>("api/Eventos");
                        await FiltrarEventosPorDisciplinaUsuarioAsync();
                        await CargarEventoUsuarioYNombresEmpresaAsync();
                        var tipoAlerta = TiposEvento?.FirstOrDefault(t => string.Equals(t.Nombre?.Trim(), "Alerta", StringComparison.OrdinalIgnoreCase));
                        var alertas = (Eventos ?? new List<EventoDto>()).Where(e => tipoAlerta != null && e.TipoEventoId == tipoAlerta.Id).Take(100).ToList();
                        var cerrados = alertas.Where(e => e.FechaCierre.HasValue || !e.RequiereCierre).ToList();
                        var abiertosKanban = alertas.Where(e => e.RequiereCierre && !e.FechaCierre.HasValue).ToList();
                        var eventosConForo = new HashSet<int>();
                        if (abiertosKanban.Count > 0)
                        {
                            var foroTasks = abiertosKanban.Select(async e =>
                            {
                                try
                                {
                                    var posts = await _ritwebApi.GetListAsync<EventoForoPostDto>($"api/Eventos/{e.Id}/Foro");
                                    return (e.Id, (posts?.Count ?? 0) > 0);
                                }
                                catch { return (e.Id, false); }
                            }).ToList();
                            var foroResults = await Task.WhenAll(foroTasks);
                            foreach (var r in foroResults.Where(x => x.Item2)) eventosConForo.Add(r.Item1);
                        }
                        KanbanCerrados = cerrados;
                        KanbanEnProceso = abiertosKanban.Where(e => eventosConForo.Contains(e.Id)).ToList();
                        KanbanAperturados = abiertosKanban.Where(e => !eventosConForo.Contains(e.Id)).ToList();
                        CriticidadesDict = Criticidades?.ToDictionary(c => c.Id, c => c.Nombre) ?? new Dictionary<int, string>();
                        SectoresDict = Sectores?.ToDictionary(s => s.Id, s => s.Nombre) ?? new Dictionary<int, string>();
                        break;
                    case "dashboard":
                    default:
                        Eventos = await _ritwebApi.GetListAsync<EventoDto>("api/Eventos");
                        await FiltrarEventosPorDisciplinaUsuarioAsync();
                        if (EditEventoId.HasValue && EditEventoId.Value > 0)
                            EventoEditar = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{EditEventoId.Value}");
                        break;
                }
            }
            catch (Exception ex)
            {
                var baseUrl = _configuration["ApiRitweb:BaseUrl"] ?? "(no configurada)";
                _logger.LogWarning(ex, "Error al cargar datos API_Ritweb. URL configurada: {BaseUrl}. Mensaje: {Message}", baseUrl, ex.Message);
                var msg = ex.Message;
                bool esErrorHttp = ex is HttpRequestException && (msg.Contains("401") || msg.Contains("403") || msg.Contains("404") || msg.Contains("500") || msg.Contains("StatusCode") || msg.Contains(" ("));
                string? codigo = null;
                string? detalleApi = null;
                if (esErrorHttp)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"(\d{3})\s*\(([^)]*)\)");
                    if (match.Success) codigo = $"{match.Groups[1].Value} ({match.Groups[2].Value})";
                    else if (msg.Contains("401")) codigo = "401 (Unauthorized)";
                    else if (msg.Contains("403")) codigo = "403 (Forbidden)";
                    else if (msg.Contains("404")) codigo = "404 (Not Found)";
                    else if (msg.Contains("500")) codigo = "500 (Error interno del servidor)";
                    int idxColon = msg.IndexOf("): ");
                    if (idxColon >= 0 && idxColon + 3 < msg.Length)
                    {
                        var cuerpo = msg.Substring(idxColon + 3).Trim();
                        if (cuerpo.Length > 0)
                        {
                            detalleApi = cuerpo.Length > 350 ? cuerpo.Substring(0, 350) + "…" : cuerpo;
                            detalleApi = detalleApi.Replace("\r", " ").Replace("\n", " ");
                        }
                    }
                }
                if (esErrorHttp)
                    ErrorApi = $"API_Ritweb: error {codigo ?? "HTTP"}. {(detalleApi != null ? " Detalle desde la API: " + detalleApi : " Revise la ventana 'Salida' del IDE o los logs de API_Ritweb al depurar.")}";
                else
                    ErrorApi = $"No se pudo conectar con API_Ritweb. URL: {baseUrl}. Compruebe que la API esté en ejecución (Swagger en {baseUrl}/swagger).";
            }
        }

        private async Task CargarDisciplinasAsync()
        {
            try
            {
                Disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar Disciplinas desde RIT API");
            }
        }

        private async Task CargarProyectosAsync()
        {
            try
            {
                Proyectos = await _ritApi.GetListAsync<ProyectoApi>("api/Proyectos");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar Proyectos desde RIT API");
            }
        }

        /// <summary>Filtra eventos según rol: Super_admin ve todos. Admin: creados por él + misma empresa (todas disciplinas). Usuario: solo misma empresa, mismo proyecto (y centro de costo implícito).</summary>
        private async Task FiltrarEventosPorDisciplinaUsuarioAsync()
        {
            if (Eventos == null) return;
            if (IsSuperAdmin) return;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;
            try
            {
                var misEmpresaIds = new HashSet<int>();
                var usuariosEmpresa = await _ritApi.GetListAsync<UsuarioEmpresaColaboradoraApi>("api/UsuariosEmpresaColaboradora");
                foreach (var ue in usuariosEmpresa ?? new List<UsuarioEmpresaColaboradoraApi>())
                {
                    if (string.Equals(ue.UserId, user.Id, StringComparison.OrdinalIgnoreCase) && ue.EmpresaColaboradoraId > 0)
                        misEmpresaIds.Add(ue.EmpresaColaboradoraId);
                }

                var isAdmin = User.IsInRole("Admin");

                if (isAdmin)
                {
                    // Admin: eventos creados por él + de su misma empresa (todas las disciplinas)
                    Eventos = Eventos.Where(e =>
                        string.Equals(e.UserId, user.Id, StringComparison.OrdinalIgnoreCase)
                        || (misEmpresaIds.Count > 0 && e.EmpresaColaboradoraId > 0 && misEmpresaIds.Contains(e.EmpresaColaboradoraId))
                    ).ToList();
                }
                else
                {
                    // Usuario: misma empresa, mismo proyecto y misma disciplina (si ambos tienen disciplina asignada)
                    int? miDisciplinaId = null;
                    var usuariosDisciplina = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina");
                    var asignacionDisc = usuariosDisciplina?.FirstOrDefault(ud => string.Equals(ud.UserId, user.Id, StringComparison.OrdinalIgnoreCase));
                    if (asignacionDisc != null)
                        miDisciplinaId = asignacionDisc.DisciplinaId;

                    var ritWebAppId = await GetRitWebAppIdAsync();
                    var userProyectoId = ritWebAppId.HasValue ? await GetUserProyectoIdForRitWebAsync(user.Id, ritWebAppId.Value) : null;

                    Eventos = Eventos.Where(e =>
                        misEmpresaIds.Count > 0 && e.EmpresaColaboradoraId > 0 && misEmpresaIds.Contains(e.EmpresaColaboradoraId)
                            && userProyectoId.HasValue && userProyectoId.Value > 0 && e.ProyectoId == userProyectoId.Value
                            && (!miDisciplinaId.HasValue || !e.DisciplinaId.HasValue || e.DisciplinaId.Value == miDisciplinaId.Value)
                    ).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al filtrar eventos por disciplina/empresa del usuario");
                Eventos = new List<EventoDto>();
            }
        }

        /// <summary>Resuelve nombres de usuario (Registrado por) y nombres de empresa para la tabla Ver Eventos.</summary>
        private async Task CargarEventoUsuarioYNombresEmpresaAsync()
        {
            EventoUsuarioNombres.Clear();
            EventoUsuarioDisciplinaNombres.Clear();
            EventoUsuarioTurno.Clear();
            EventoEmpresaNombres.Clear();
            if (Eventos == null || !Eventos.Any()) return;

            var userIds = Eventos.Select(e => e.UserId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (userIds.Count > 0)
            {
                var perfiles = await _db.UserProfiles
                    .AsNoTracking()
                    .Where(p => userIds.Contains(p.UserId))
                    .Select(p => new { p.UserId, p.FullName })
                    .ToListAsync();
                var perfilPorUserId = perfiles.ToDictionary(p => p.UserId, p => p.FullName);

                foreach (var uid in userIds)
                {
                    var identity = await _userManager.FindByIdAsync(uid);
                    var nombre = (perfilPorUserId.TryGetValue(uid, out var fullName) && !string.IsNullOrWhiteSpace(fullName))
                        ? fullName!.Trim()
                        : (identity?.Email ?? identity?.UserName ?? uid);
                    EventoUsuarioNombres[uid] = nombre;
                }

                try
                {
                    var usuariosDisciplina = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina");
                    var disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas");
                    var disciplinaById = disciplinas?.ToDictionary(d => d.Id, d => d.Nombre) ?? new Dictionary<int, string>();
                    foreach (var uid in userIds)
                    {
                        var asignacion = usuariosDisciplina?.FirstOrDefault(ud => string.Equals(ud.UserId, uid, StringComparison.OrdinalIgnoreCase));
                        if (asignacion != null && disciplinaById.TryGetValue(asignacion.DisciplinaId, out var discNombre) && !string.IsNullOrWhiteSpace(discNombre))
                            EventoUsuarioDisciplinaNombres[uid] = discNombre.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al cargar disciplinas de usuarios para tabla Ver Eventos");
                }

                try
                {
                    foreach (var uid in userIds)
                    {
                        var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{uid}/Ficha");
                        if (!string.IsNullOrWhiteSpace(ficha?.Turno))
                            EventoUsuarioTurno[uid] = ficha.Turno.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error al cargar turno de ficha de usuarios para tabla Ver Eventos");
                }
            }

            var proyectoEmpresaPairs = Eventos
                .Where(e => e.EmpresaColaboradoraId > 0)
                .Select(e => (e.ProyectoId, e.EmpresaColaboradoraId))
                .Distinct()
                .ToList();
            foreach (var (proyectoId, empresaId) in proyectoEmpresaPairs)
            {
                var key = $"{proyectoId}_{empresaId}";
                if (EventoEmpresaNombres.ContainsKey(key)) continue;
                try
                {
                    var empresas = await _ritApi.GetListAsync<EmpresaColaboradoraApi>($"api/EmpresasColaboradoras?proyectoId={proyectoId}");
                    var emp = empresas?.FirstOrDefault(e => e.Id == empresaId);
                    EventoEmpresaNombres[key] = emp?.Nombre?.Trim() ?? empresaId.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al cargar empresa ProyectoId={ProyectoId} EmpresaId={EmpresaId}", proyectoId, empresaId);
                    EventoEmpresaNombres[key] = empresaId.ToString();
                }
            }
        }

        private async Task<IActionResult?> VerificarAccesoRitWebAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var ritWebAppId = await GetRitWebAppIdAsync();
            if (ritWebAppId == null) { TempData["MensajeAjustesError"] = "No se encontró la aplicación RitWeb."; return RedirectToPage("/App"); }
            var userAppIds = await GetUserAppIdsAsync(user.Id);
            if (!userAppIds.Contains(ritWebAppId.Value)) { TempData["MensajeAjustesError"] = "No tiene acceso a RitWeb."; return RedirectToPage("/App"); }
            return null;
        }

        /// <summary>Redirige a eventos con mensaje si el usuario no es Super_admin. Usar al inicio de handlers POST de Ajustes.</summary>
        private IActionResult? RedirSiNoSuperAdminAjustes()
        {
            if (User.IsInRole("Super_admin") || User.IsInRole("Super_Admin")) return null;
            TempData["MensajeAjustesError"] = "Solo usuarios con rol Super_admin pueden realizar esta acción.";
            return RedirectToPage("/RitWeb", new { tab = "eventos" });
        }

        public async Task<IActionResult> OnGetEmpresasPorProyectoAsync(int proyectoId)
        {
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (proyectoId <= 0) return new JsonResult(Array.Empty<object>());

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var isSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
            if (!isSuperAdmin)
            {
                var ritWebAppId = await GetRitWebAppIdAsync();
                var userProy = ritWebAppId.HasValue ? await GetUserProyectoIdForRitWebAsync(user.Id, ritWebAppId.Value) : null;
                if (!userProy.HasValue || userProy.Value != proyectoId)
                    return new JsonResult(Array.Empty<object>());
            }

            try
            {
                var empresas = await _ritApi.GetListAsync<EmpresaColaboradoraApi>($"api/EmpresasColaboradoras?proyectoId={proyectoId}");
                var payload = empresas
                    .OrderBy(e => e.Nombre)
                    .Select(e => new { id = e.Id, nombre = e.Nombre })
                    .ToList();
                return new JsonResult(payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar empresas por proyecto ProyectoId={ProyectoId}", proyectoId);
                return new JsonResult(Array.Empty<object>());
            }
        }

        public async Task<IActionResult> OnGetProfesionalesPorEmpresaAsync(int empresaColaboradoraId)
        {
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (empresaColaboradoraId <= 0) return new JsonResult(Array.Empty<object>());

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Seguridad básica: usuarios no super_admin solo pueden consultar empresas de su proyecto asignado.
            var isSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
            if (!isSuperAdmin)
            {
                var ritWebAppId = await GetRitWebAppIdAsync();
                var userProy = ritWebAppId.HasValue ? await GetUserProyectoIdForRitWebAsync(user.Id, ritWebAppId.Value) : null;
                if (!userProy.HasValue || userProy.Value <= 0) return new JsonResult(Array.Empty<object>());
                try
                {
                    var empresasPermitidas = await _ritApi.GetListAsync<EmpresaColaboradoraApi>($"api/EmpresasColaboradoras?proyectoId={userProy.Value}");
                    if (!empresasPermitidas.Any(e => e.Id == empresaColaboradoraId))
                        return new JsonResult(Array.Empty<object>());
                }
                catch
                {
                    return new JsonResult(Array.Empty<object>());
                }
            }

            try
            {
                var asignaciones = await _ritApi.GetListAsync<UsuarioEmpresaColaboradoraApi>("api/UsuariosEmpresaColaboradora");
                var usersEmpresa = asignaciones
                    .Where(x => x.EmpresaColaboradoraId == empresaColaboradoraId)
                    .ToList();

                if (usersEmpresa.Count == 0) return new JsonResult(Array.Empty<object>());

                // Profesión: usamos Disciplina (si viene en la asignación).
                var disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas");
                var disciplinaById = disciplinas.ToDictionary(d => d.Id, d => d.Nombre);

                var userIds = usersEmpresa.Select(x => x.UserId).Distinct().ToList();
                var perfiles = _db.UserProfiles
                    .Where(p => userIds.Contains(p.UserId))
                    .Select(p => new { p.UserId, p.FullName })
                    .ToList()
                    .ToDictionary(x => x.UserId, x => x.FullName);

                var payload = new List<object>();
                foreach (var ua in usersEmpresa)
                {
                    var identity = await _userManager.FindByIdAsync(ua.UserId);
                    var nombre = (perfiles.TryGetValue(ua.UserId, out var fullName) && !string.IsNullOrWhiteSpace(fullName))
                        ? fullName!
                        : (identity?.Email ?? identity?.UserName ?? ua.UserId);

                    // Cargo: por ahora no hay catálogo de roles en RIT_API en este proyecto; mostramos RolId si existe.
                    var cargo = ua.RolId.HasValue ? $"Rol #{ua.RolId.Value}" : null;
                    var profesion = (ua.DisciplinaId.HasValue && disciplinaById.TryGetValue(ua.DisciplinaId.Value, out var dn)) ? dn : null;

                    payload.Add(new
                    {
                        userId = ua.UserId,
                        nombre = nombre,
                        cargo = cargo,
                        profesion = profesion
                    });
                }

                return new JsonResult(payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar profesionales por empresa EmpresaId={EmpresaId}", empresaColaboradoraId);
                return new JsonResult(Array.Empty<object>());
            }
        }

        /// <summary>Parsea latitud/longitud desde el formulario (acepta punto o coma decimal).</summary>
        private static decimal? ParseLatLong(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var s = value.Trim().Replace(',', '.');
            if (decimal.TryParse(s, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return null;
        }

        /// <summary>Registra un nuevo evento (POST) o actualiza uno existente (PUT). En edición la fecha del evento no se modifica.</summary>
        public async Task<IActionResult> OnPostRegistrarEventoAsync(
            int? eventoId,
            int tipoEventoId,
            int? tipoEventoDetalleId,
            DateTime? fechaEvento,
            string? turno,
            int proyectoId,
            int? sectorId,
            int? disciplinaId,
            int? tipoAlertaId,
            int? criticidadId,
            int empresaColaboradoraId,
            string? profesionalNotificadoNombre,
            string? profesionalNotificadoCargo,
            int? tipoDocumentoRespaldoId,
            string? descripcion,
            string? referenciaDocumentoRespaldo,
            string? numeroDocumentoProtocolo,
            string? codigoVp,
            string? latitude,
            string? longitude,
            List<IFormFile>? fotos)
        {
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (!User.IsInRole("Admin") && !User.IsInRole("Super_admin") && !User.IsInRole("Super_Admin"))
            {
                TempData["MensajeAjustesError"] = "Su rol solo tiene acceso a Dashboard, Ver eventos y Kanban.";
                return RedirectToPage("/RitWeb", new { tab = "dashboard" });
            }
            if (tipoEventoId <= 0) { TempData["MensajeAjustesError"] = "Seleccione un tipo de evento."; return RedirectToPage(new { tab = "registrarevento", editEventoId = eventoId }); }
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var ritWebAppId = await GetRitWebAppIdAsync();
            var isSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
            int proyectoFinal = proyectoId;
            if (!isSuperAdmin)
            {
                var userProy = ritWebAppId.HasValue ? await GetUserProyectoIdForRitWebAsync(user.Id, ritWebAppId.Value) : null;
                if (userProy.HasValue && userProy.Value > 0)
                    proyectoFinal = userProy.Value;
                else
                { TempData["MensajeAjustesError"] = "No tiene un proyecto asignado para RitWeb. Contacte al administrador (Ajustes → Usuarios por app)."; return RedirectToPage(new { tab = "registrarevento", editEventoId = eventoId }); }
            }
            else if (proyectoId <= 0)
            { TempData["MensajeAjustesError"] = "Seleccione un proyecto."; return RedirectToPage(new { tab = "registrarevento", editEventoId = eventoId }); }

            // Si el turno llegó vacío, intentar obtenerlo de la ficha del usuario (RIT_API) para enviarlo a API_Ritweb
            if (string.IsNullOrWhiteSpace(turno))
            {
                try
                {
                    var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{user.Id}/Ficha");
                    if (!string.IsNullOrWhiteSpace(ficha?.Turno))
                        turno = ficha.Turno.Trim();
                }
                catch (Exception ex) { _logger.LogDebug(ex, "No se pudo obtener turno de la ficha para rellenar automático"); }
            }

            // Si el tipo de evento no requiere cierre, no registrar tipo alerta, criticidad ni jerarquía
            try
            {
                var tiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
                var tipoEvento = tiposEvento?.FirstOrDefault(t => t.Id == tipoEventoId);
                if (tipoEvento != null && !tipoEvento.RequiereCierre)
                {
                    tipoAlertaId = null;
                    criticidadId = null;
                    tipoEventoDetalleId = null;
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "No se pudo verificar RequiereCierre del tipo de evento"); }

            decimal? latParsed = ParseLatLong(latitude);
            decimal? lngParsed = ParseLatLong(longitude);

            var esEdicion = eventoId.HasValue && eventoId.Value > 0;
            DateTime? fechaEventoUtc = null;
            if (esEdicion)
            {
                var existente = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{eventoId!.Value}");
                if (existente?.FechaEvento == null) { TempData["MensajeAjustesError"] = "No se encontró el evento o no tiene fecha."; return RedirectToPage(new { tab = "eventos" }); }
                var d = existente.FechaEvento.Value;
                fechaEventoUtc = d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc);
            }
            else
            {
                if (fechaEvento.HasValue)
                {
                    var d = fechaEvento.Value;
                    fechaEventoUtc = d.Kind == DateTimeKind.Utc
                        ? d
                        : (d.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(d, DateTimeKind.Local).ToUniversalTime()
                            : d.ToUniversalTime());
                }
            }

            try
            {
                if (esEdicion)
                {
                    var updated = await _ritwebApi.PutAsync<object, EventoDto>($"api/Eventos/{eventoId!.Value}", new
                    {
                        userId = user.Id,
                        empresaColaboradoraId = empresaColaboradoraId,
                        tipoEventoId = tipoEventoId,
                        tipoEventoDetalleId = tipoEventoDetalleId,
                        proyectoId = proyectoFinal,
                        sectorId = sectorId,
                        disciplinaId = disciplinaId,
                        fechaEvento = fechaEventoUtc,
                        turno = string.IsNullOrWhiteSpace(turno) ? null : turno.Trim(),
                        tipoAlertaId = tipoAlertaId,
                        criticidadId = criticidadId,
                        profesionalNotificadoNombre = string.IsNullOrWhiteSpace(profesionalNotificadoNombre) ? null : profesionalNotificadoNombre.Trim(),
                        profesionalNotificadoCargo = string.IsNullOrWhiteSpace(profesionalNotificadoCargo) ? null : profesionalNotificadoCargo.Trim(),
                        tipoDocumentoRespaldoId = tipoDocumentoRespaldoId,
                        descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                        referenciaDocumentoRespaldo = string.IsNullOrWhiteSpace(referenciaDocumentoRespaldo) ? null : referenciaDocumentoRespaldo.Trim(),
                        numeroDocumentoProtocolo = string.IsNullOrWhiteSpace(numeroDocumentoProtocolo) ? null : numeroDocumentoProtocolo.Trim(),
                        codigoVp = string.IsNullOrWhiteSpace(codigoVp) ? null : codigoVp.Trim(),
                        latitude = (latParsed.HasValue && latParsed.Value >= -90m && latParsed.Value <= 90m) ? latParsed : null,
                        longitude = (lngParsed.HasValue && lngParsed.Value >= -180m && lngParsed.Value <= 180m) ? lngParsed : null
                    });
                    if (updated != null && fotos is { Count: > 0 })
                    {
                        foreach (var f in fotos.Where(x => x != null && x.Length > 0))
                        {
                            try
                            {
                                using var content = new MultipartFormDataContent();
                                content.Add(new StringContent("FotoEvento"), "tipoAdjunto");
                                var fileContent = new StreamContent(f.OpenReadStream());
                                if (!string.IsNullOrWhiteSpace(f.ContentType))
                                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(f.ContentType);
                                content.Add(fileContent, "archivo", f.FileName);
                                await _ritwebApi.PostMultipartAsync<EventoAdjuntoDto>($"api/Eventos/{eventoId.Value}/Adjuntos/subir", content);
                            }
                            catch (Exception ex) { _logger.LogWarning(ex, "Error al subir adjunto eventoId={EventoId}", eventoId.Value); }
                        }
                    }
                    TempData["MensajeAjustes"] = "Evento actualizado correctamente.";
                    return RedirectToPage(new { tab = "eventos" });
                }

                var created = await _ritwebApi.PostAsync<object, EventoDto>("api/Eventos", new
                {
                    userId = user.Id,
                    empresaColaboradoraId = empresaColaboradoraId,
                    tipoEventoId = tipoEventoId,
                    tipoEventoDetalleId = tipoEventoDetalleId,
                    proyectoId = proyectoFinal,
                    sectorId = sectorId,
                    disciplinaId = disciplinaId,
                    fechaEvento = fechaEventoUtc,
                    turno = string.IsNullOrWhiteSpace(turno) ? null : turno.Trim(),
                    tipoAlertaId = tipoAlertaId,
                    criticidadId = criticidadId,
                    profesionalNotificadoNombre = string.IsNullOrWhiteSpace(profesionalNotificadoNombre) ? null : profesionalNotificadoNombre.Trim(),
                    profesionalNotificadoCargo = string.IsNullOrWhiteSpace(profesionalNotificadoCargo) ? null : profesionalNotificadoCargo.Trim(),
                    tipoDocumentoRespaldoId = tipoDocumentoRespaldoId,
                    descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                    referenciaDocumentoRespaldo = string.IsNullOrWhiteSpace(referenciaDocumentoRespaldo) ? null : referenciaDocumentoRespaldo.Trim(),
                    numeroDocumentoProtocolo = string.IsNullOrWhiteSpace(numeroDocumentoProtocolo) ? null : numeroDocumentoProtocolo.Trim(),
                    codigoVp = string.IsNullOrWhiteSpace(codigoVp) ? null : codigoVp.Trim(),
                    latitude = (latParsed.HasValue && latParsed.Value >= -90m && latParsed.Value <= 90m) ? latParsed : null,
                    longitude = (lngParsed.HasValue && lngParsed.Value >= -180m && lngParsed.Value <= 180m) ? lngParsed : null
                });

                var erroresAdjuntos = new List<string>();
                if (created?.Id > 0 && fotos is { Count: > 0 })
                {
                    foreach (var f in fotos.Where(x => x != null && x.Length > 0))
                    {
                        try
                        {
                            using var content = new MultipartFormDataContent();
                            content.Add(new StringContent("FotoEvento"), "tipoAdjunto");
                            var fileContent = new StreamContent(f.OpenReadStream());
                            if (!string.IsNullOrWhiteSpace(f.ContentType))
                                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(f.ContentType);
                            content.Add(fileContent, "archivo", f.FileName);
                            await _ritwebApi.PostMultipartAsync<EventoAdjuntoDto>($"api/Eventos/{created.Id}/Adjuntos/subir", content);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error al subir adjunto eventoId={EventoId}", created.Id);
                            erroresAdjuntos.Add($"{f.FileName}: {ex.Message}");
                        }
                    }
                }

                TempData["MensajeAjustes"] = "Evento registrado correctamente.";
                if (created?.Id > 0) TempData["RegistroEventoId"] = created.Id;
                if (erroresAdjuntos.Count > 0)
                    TempData["MensajeAjustesError"] = "Evento creado, pero algunos adjuntos no se pudieron subir: " + string.Join(" | ", erroresAdjuntos);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, esEdicion ? "Error al actualizar evento" : "Error al registrar evento");
                TempData["MensajeAjustesError"] = (esEdicion ? "No se pudo actualizar el evento: " : "No se pudo registrar el evento: ") + ex.Message;
                return RedirectToPage(new { tab = "registrarevento", editEventoId = eventoId });
            }
            return RedirectToPage(new { tab = "eventos" });
        }

        /// <summary>Elimina un evento (DELETE api/Eventos/{id}).</summary>
        public async Task<IActionResult> OnPostEliminarEventoAsync(int id)
        {
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) { TempData["MensajeAjustesError"] = "ID de evento no válido."; return RedirectToPage(new { tab = "eventos" }); }
            try
            {
                await _ritwebApi.DeleteAsync($"api/Eventos/{id}");
                TempData["MensajeAjustes"] = "Evento eliminado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar evento Id={EventoId}", id);
                TempData["MensajeAjustesError"] = "No se pudo eliminar el evento: " + ex.Message;
            }
            return RedirectToPage(new { tab = "eventos" });
        }

        // ---------- CRUD Tipos de alerta ----------
        public async Task<IActionResult> OnPostCreateTipoAlertaAsync(string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposalerta" }); }
            try
            {
                await _ritwebApi.PostAsync<object, TipoAlertaDto>("api/TiposAlerta", new
                {
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden = orden,
                    activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de alerta creado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al crear TipoAlerta");
                TempData["MensajeAjustesError"] = "No se pudo crear: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposalerta" });
        }

        public async Task<IActionResult> OnPostEditTipoAlertaAsync(int id, string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "tiposalerta" });
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposalerta", editTipoAlertaId = id }); }
            try
            {
                await _ritwebApi.PutAsync<object, TipoAlertaDto>($"api/TiposAlerta/{id}", new
                {
                    id = id,
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden = orden,
                    activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de alerta actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al editar TipoAlerta id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposalerta" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoAlertaAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "tiposalerta" });
            try
            {
                await _ritwebApi.PatchAsync($"api/TiposAlerta/{id}", new { activo });
                TempData["MensajeAjustes"] = "Estado actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error toggle activo TipoAlerta id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposalerta" });
        }

        public async Task<IActionResult> OnPostDeleteTipoAlertaAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "tiposalerta" });
            try
            {
                await _ritwebApi.DeleteAsync($"api/TiposAlerta/{id}");
                TempData["MensajeAjustes"] = "Tipo de alerta eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error delete TipoAlerta id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposalerta" });
        }

        // ---------- CRUD Criticidades ----------
        public async Task<IActionResult> OnPostCreateCriticidadAsync(string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "criticidades" }); }
            try
            {
                await _ritwebApi.PostAsync<object, CriticidadDto>("api/Criticidades", new
                {
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden = orden,
                    activo = activo
                });
                TempData["MensajeAjustes"] = "Criticidad creada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al crear Criticidad");
                TempData["MensajeAjustesError"] = "No se pudo crear: " + ex.Message;
            }
            return RedirectToPage(new { tab = "criticidades" });
        }

        public async Task<IActionResult> OnPostEditCriticidadAsync(int id, string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "criticidades" });
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "criticidades", editCriticidadId = id }); }
            try
            {
                await _ritwebApi.PutAsync<object, CriticidadDto>($"api/Criticidades/{id}", new
                {
                    id = id,
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden = orden,
                    activo = activo
                });
                TempData["MensajeAjustes"] = "Criticidad actualizada.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al editar Criticidad id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "criticidades" });
        }

        public async Task<IActionResult> OnPostToggleActivoCriticidadAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "criticidades" });
            try
            {
                await _ritwebApi.PatchAsync($"api/Criticidades/{id}", new { activo });
                TempData["MensajeAjustes"] = "Estado actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error toggle activo Criticidad id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "criticidades" });
        }

        public async Task<IActionResult> OnPostDeleteCriticidadAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "criticidades" });
            try
            {
                await _ritwebApi.DeleteAsync($"api/Criticidades/{id}");
                TempData["MensajeAjustes"] = "Criticidad eliminada.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error delete Criticidad id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "criticidades" });
        }

        // ---------- CRUD Turnos en RIT_API (catálogo 5x2, 7x7, 14x14; el turno del usuario se asigna en la ficha) ----------
        public async Task<IActionResult> OnPostCreateTurnoAsync(string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "turnos" }); }
            try
            {
                await _ritApi.PostAsync<object, TurnoApi>("api/Turnos", new
                {
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden,
                    activo
                });
                TempData["MensajeAjustes"] = "Turno creado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al crear Turno");
                TempData["MensajeAjustesError"] = "No se pudo crear: " + ex.Message;
            }
            return RedirectToPage(new { tab = "turnos" });
        }

        public async Task<IActionResult> OnPostEditTurnoAsync(int id, string nombre, string? codigo, int orden, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "turnos" });
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "turnos", editTurnoId = id }); }
            try
            {
                await _ritApi.PutAsync<object, TurnoApi>($"api/Turnos/{id}", new
                {
                    nombre = nombre.Trim(),
                    codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    orden,
                    activo
                });
                TempData["MensajeAjustes"] = "Turno actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al editar Turno id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "turnos" });
        }

        public async Task<IActionResult> OnPostToggleActivoTurnoAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "turnos" });
            try
            {
                var turno = await _ritApi.GetAsync<TurnoApi>($"api/Turnos/{id}");
                if (turno != null)
                    await _ritApi.PutAsync<object, TurnoApi>($"api/Turnos/{id}", new { nombre = turno.Nombre, codigo = turno.Codigo, orden = turno.Orden, activo });
                TempData["MensajeAjustes"] = activo ? "Turno activado." : "Turno desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error toggle activo Turno id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "turnos" });
        }

        public async Task<IActionResult> OnPostDeleteTurnoAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (id <= 0) return RedirectToPage(new { tab = "turnos" });
            try
            {
                await _ritApi.DeleteAsync($"api/Turnos/{id}");
                TempData["MensajeAjustes"] = "Turno eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error delete Turno id={Id}", id);
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "turnos" });
        }

        // ---------- CRUD Tipo de evento ----------
        public async Task<IActionResult> OnPostCreateTipoEventoAsync(string nombre, List<int>? disciplinaIds, bool requiereCierre, bool requiereDocumentoRespaldo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposevento" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposevento" }); }
            var ids = disciplinaIds ?? new List<int>();
            try
            {
                await _ritwebApi.PostAsync<object, TipoEventoDto>("api/TiposEvento", new
                {
                    Nombre = nombre.Trim(),
                    DisciplinaIds = ids.Count > 0 ? ids : null,
                    RequiereCierre = requiereCierre,
                    RequiereDocumentoRespaldo = requiereDocumentoRespaldo,
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de evento creado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al crear tipo de evento");
                TempData["MensajeAjustesError"] = "No se pudo crear: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposevento" });
        }

        public async Task<IActionResult> OnPostEditTipoEventoAsync(int id, string nombre, List<int>? disciplinaIds, bool requiereCierre, bool requiereDocumentoRespaldo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposevento" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposevento" }); }
            var ids = disciplinaIds ?? new List<int>();
            try
            {
                await _ritwebApi.PutAsync<object, TipoEventoDto>($"api/TiposEvento/{id}", new
                {
                    Nombre = nombre.Trim(),
                    DisciplinaIds = ids.Count > 0 ? ids : null,
                    RequiereCierre = requiereCierre,
                    RequiereDocumentoRespaldo = requiereDocumentoRespaldo,
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de evento actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al actualizar tipo de evento");
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposevento" });
        }

        public async Task<IActionResult> OnPostDeleteTipoEventoAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.DeleteAsync($"api/TiposEvento/{id}");
                TempData["MensajeAjustes"] = "Tipo de evento eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar tipo de evento");
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposevento" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoEventoAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.PatchAsync($"api/TiposEvento/{id}", new { activo });
                TempData["MensajeAjustes"] = activo ? "Tipo de evento activado." : "Tipo de evento desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cambiar estado tipo de evento");
                TempData["MensajeAjustesError"] = "No se pudo cambiar estado: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposevento" });
        }

        // ---------- CRUD Sectores ----------
        public async Task<IActionResult> OnPostCreateSectorAsync(string nombre, int proyectoId, string? codigo, string? descripcion, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "sectores" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "sectores" }); }
            if (proyectoId <= 0) { TempData["MensajeAjustesError"] = "Seleccione un proyecto (configure proyectos en Ajustes RIT si no hay ninguno)."; return RedirectToPage(new { tab = "sectores" }); }
            try
            {
                await _ritwebApi.PostAsync<object, SectorDto>("api/Sectores", new
                {
                    Nombre = nombre.Trim(),
                    ProyectoId = proyectoId,
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Sector creado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al crear sector");
                TempData["MensajeAjustesError"] = "No se pudo crear: " + ex.Message;
            }
            return RedirectToPage(new { tab = "sectores" });
        }

        public async Task<IActionResult> OnPostEditSectorAsync(int id, string nombre, int proyectoId, string? codigo, string? descripcion, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "sectores" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "sectores" }); }
            if (proyectoId <= 0) { TempData["MensajeAjustesError"] = "Seleccione un proyecto."; return RedirectToPage(new { tab = "sectores" }); }
            try
            {
                await _ritwebApi.PutAsync<object, SectorDto>($"api/Sectores/{id}", new
                {
                    Nombre = nombre.Trim(),
                    ProyectoId = proyectoId,
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Sector actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al actualizar sector");
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "sectores" });
        }

        public async Task<IActionResult> OnPostDeleteSectorAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.DeleteAsync($"api/Sectores/{id}");
                TempData["MensajeAjustes"] = "Sector eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar sector");
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "sectores" });
        }

        public async Task<IActionResult> OnPostToggleActivoSectorAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.PatchAsync($"api/Sectores/{id}", new { activo });
                TempData["MensajeAjustes"] = activo ? "Sector activado." : "Sector desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cambiar estado sector");
                TempData["MensajeAjustesError"] = "No se pudo cambiar estado: " + ex.Message;
            }
            return RedirectToPage(new { tab = "sectores" });
        }

        // ---------- CRUD Tipos documento respaldo ----------
        public async Task<IActionResult> OnPostCreateTipoDocumentoAsync(string nombre, string? codigo, List<int>? disciplinaIds, string? descripcion, string? numeroProtocolo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            var disciplinas = disciplinaIds is { Count: > 0 }
                ? disciplinaIds
                : (await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas"))?.Select(d => d.Id).ToList() ?? new List<int>();
            if (disciplinas.Count == 0) disciplinas.Add(0);
            var creados = 0;
            var errores = new List<string>();
            foreach (var disciplinaId in disciplinas.Distinct())
            {
                try
                {
                    await _ritwebApi.PostAsync<object, TipoDocumentoRespaldoDto>("api/TiposDocumentoRespaldo", new
                    {
                        Nombre = nombre.Trim(),
                        Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                        DisciplinaId = disciplinaId,
                        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                        NumeroProtocolo = string.IsNullOrWhiteSpace(numeroProtocolo) ? null : numeroProtocolo.Trim(),
                        Activo = activo
                    });
                    creados++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al crear tipo documento DisciplinaId={DisciplinaId}", disciplinaId);
                    errores.Add($"Disciplina {disciplinaId}: {ex.Message}");
                }
            }
            if (creados > 0)
                TempData["MensajeAjustes"] = creados == 1 ? "Tipo de documento creado correctamente." : $"{creados} tipos de documento creados correctamente.";
            if (errores.Count > 0)
                TempData["MensajeAjustesError"] = errores.Count == 1 ? "No se pudo crear: " + errores[0] : $"Algunos no se crearon ({errores.Count}): " + string.Join("; ", errores.Take(2)) + (errores.Count > 2 ? "…" : "");
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        public async Task<IActionResult> OnPostEditTipoDocumentoAsync(int id, string nombre, string? codigo, int disciplinaId, string? descripcion, string? numeroProtocolo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            try
            {
                await _ritwebApi.PutAsync<object, TipoDocumentoRespaldoDto>($"api/TiposDocumentoRespaldo/{id}", new
                {
                    Nombre = nombre.Trim(),
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    DisciplinaId = disciplinaId,
                    Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                    NumeroProtocolo = string.IsNullOrWhiteSpace(numeroProtocolo) ? null : numeroProtocolo.Trim(),
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de documento actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al actualizar tipo de documento");
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        public async Task<IActionResult> OnPostDeleteTipoDocumentoAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.DeleteAsync($"api/TiposDocumentoRespaldo/{id}");
                TempData["MensajeAjustes"] = "Tipo de documento eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar tipo de documento");
                TempData["MensajeAjustesError"] = "No se pudo eliminar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoDocumentoAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.PatchAsync($"api/TiposDocumentoRespaldo/{id}", new { activo });
                TempData["MensajeAjustes"] = activo ? "Tipo de documento activado." : "Tipo de documento desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cambiar estado tipo de documento");
                TempData["MensajeAjustesError"] = "No se pudo cambiar estado: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoDocumentoMultipleAsync(List<int> ids, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (ids == null || ids.Count == 0) { TempData["MensajeAjustesError"] = "Ningún ítem seleccionado."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            var ok = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    await _ritwebApi.PatchAsync($"api/TiposDocumentoRespaldo/{id}", new { activo });
                    ok++;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error al cambiar estado tipo documento {Id}", id); }
            }
            TempData["MensajeAjustes"] = activo ? $"{ok} tipo(s) de documento activados." : $"{ok} tipo(s) de documento desactivados.";
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        public async Task<IActionResult> OnPostDeleteTipoDocumentoMultipleAsync(List<int> ids)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (ids == null || ids.Count == 0) { TempData["MensajeAjustesError"] = "Ningún ítem seleccionado."; return RedirectToPage(new { tab = "tiposdocumento" }); }
            var ok = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    await _ritwebApi.DeleteAsync($"api/TiposDocumentoRespaldo/{id}");
                    ok++;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar tipo documento {Id}", id); }
            }
            TempData["MensajeAjustes"] = $"{ok} tipo(s) de documento eliminados.";
            return RedirectToPage(new { tab = "tiposdocumento" });
        }

        // ---------- CRUD Tipos evento detalle ----------
        public async Task<IActionResult> OnPostCreateTipoEventoDetalleAsync(string nombre, List<int>? tipoEventoIds, List<int>? disciplinaIds, int? tipoEventoDetallePadreId, string? codigo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }

            var tipos = tipoEventoIds is { Count: > 0 } ? tipoEventoIds : (await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento")).Select(t => t.Id).ToList();
            var disciplinas = disciplinaIds is { Count: > 0 } ? disciplinaIds : (await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas")).Select(d => d.Id).ToList();

            // Si el usuario eligió un padre, los hijos heredan tipos de evento y disciplinas del padre (no se usan los checkboxes).
            var usarPadre = tipoEventoDetallePadreId.HasValue && tipoEventoDetallePadreId.Value > 0;
            Dictionary<(int TipoEventoId, int DisciplinaId), int>? padrePorCombinacion = null;
            if (usarPadre)
            {
                var todosDetalles = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
                var padreRepresentativo = todosDetalles.FirstOrDefault(x => x.Id == tipoEventoDetallePadreId!.Value);
                if (padreRepresentativo != null)
                {
                    var mismoGrupo = todosDetalles.Where(x =>
                        x.Nombre == padreRepresentativo.Nombre
                        && (x.Codigo ?? "") == (padreRepresentativo.Codigo ?? "")
                        && x.TipoEventoDetallePadreId == padreRepresentativo.TipoEventoDetallePadreId
                        && x.Nivel == padreRepresentativo.Nivel).ToList();
                    padrePorCombinacion = mismoGrupo.ToDictionary(x => (x.TipoEventoId, x.DisciplinaId), x => x.Id);
                }
                else
                    usarPadre = false;
            }

            if (!usarPadre)
            {
                if (tipos.Count == 0) { TempData["MensajeAjustesError"] = "No hay tipos de evento. Cree al menos uno."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
                if (disciplinas.Count == 0) { TempData["MensajeAjustesError"] = "No hay disciplinas en RIT. Configure disciplinas primero."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            }
            else if (padrePorCombinacion == null || padrePorCombinacion.Count == 0)
            {
                TempData["MensajeAjustesError"] = "No se pudo obtener el grupo del padre seleccionado."; return RedirectToPage(new { tab = "tiposeventodetalle" });
            }

            var creados = 0;
            var errores = new List<string>();

            if (usarPadre && padrePorCombinacion != null)
            {
                // Heredar: crear solo las combinaciones del padre (mismo tipo evento y disciplina que cada registro del grupo).
                foreach (var kv in padrePorCombinacion)
                {
                    var (tipoEventoId, disciplinaId) = kv.Key;
                    var padreId = kv.Value;
                    try
                    {
                        await _ritwebApi.PostAsync<object, TipoEventoDetalleDto>("api/TiposEventoDetalle", new
                        {
                            nombre = nombre.Trim(),
                            tipoEventoId = tipoEventoId,
                            disciplinaId = disciplinaId,
                            tipoEventoDetallePadreId = padreId,
                            codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                            activo = activo
                        });
                        creados++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al crear detalle TipoEventoId={TipoEventoId}, DisciplinaId={DisciplinaId}", tipoEventoId, disciplinaId);
                        errores.Add($"Tipo {tipoEventoId} × Disciplina {disciplinaId}: {ex.Message}");
                    }
                }
            }
            else
            {
                foreach (var tipoEventoId in tipos)
                {
                    foreach (var disciplinaId in disciplinas)
                    {
                        try
                        {
                            await _ritwebApi.PostAsync<object, TipoEventoDetalleDto>("api/TiposEventoDetalle", new
                            {
                                nombre = nombre.Trim(),
                                tipoEventoId = tipoEventoId,
                                disciplinaId = disciplinaId,
                                tipoEventoDetallePadreId = (int?)null,
                                codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                                activo = activo
                            });
                            creados++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error al crear detalle TipoEventoId={TipoEventoId}, DisciplinaId={DisciplinaId}", tipoEventoId, disciplinaId);
                            errores.Add($"Tipo {tipoEventoId} × Disciplina {disciplinaId}: {ex.Message}");
                        }
                    }
                }
            }

            if (creados > 0)
                TempData["MensajeAjustes"] = creados == 1 ? "Tipo de evento detalle creado correctamente." : $"{creados} tipos de evento detalle creados correctamente.";
            if (errores.Count > 0)
                TempData["MensajeAjustesError"] = errores.Count == 1 ? "No se pudo crear: " + errores[0] : $"Algunos no se crearon ({errores.Count}): " + string.Join("; ", errores.Take(2)) + (errores.Count > 2 ? "…" : "");
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        public async Task<IActionResult> OnPostEditTipoEventoDetalleAsync(int id, string nombre, string? codigo, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (string.IsNullOrWhiteSpace(nombre)) { TempData["MensajeAjustesError"] = "El nombre es obligatorio."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            if (nombre.Trim().Length > 200) { TempData["MensajeAjustesError"] = "El nombre no puede superar 200 caracteres."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            try
            {
                await _ritwebApi.PutAsync<object, TipoEventoDetalleDto>($"api/TiposEventoDetalle/{id}", new
                {
                    Nombre = nombre.Trim(),
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim(),
                    Activo = activo
                });
                TempData["MensajeAjustes"] = "Tipo de evento detalle actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al actualizar tipo de evento detalle");
                TempData["MensajeAjustesError"] = "No se pudo actualizar: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        public async Task<IActionResult> OnPostDeleteTipoEventoDetalleAsync(int id)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.DeleteAsync($"api/TiposEventoDetalle/{id}");
                TempData["MensajeAjustes"] = "Tipo de evento detalle eliminado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar tipo de evento detalle");
                var msg = ex.Message.Contains("hijo", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("child", StringComparison.OrdinalIgnoreCase)
                    ? "No se puede eliminar: tiene hijos. Elimine primero los subniveles."
                    : "No se pudo eliminar: " + ex.Message;
                TempData["MensajeAjustesError"] = msg;
            }
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoEventoDetalleAsync(int id, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            try
            {
                await _ritwebApi.PatchAsync($"api/TiposEventoDetalle/{id}", new { activo });
                TempData["MensajeAjustes"] = activo ? "Detalle activado." : "Detalle desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cambiar estado tipo de evento detalle");
                TempData["MensajeAjustesError"] = "No se pudo cambiar estado: " + ex.Message;
            }
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        public async Task<IActionResult> OnPostToggleActivoTipoEventoDetalleMultipleAsync(List<int> ids, bool activo)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (ids == null || ids.Count == 0) { TempData["MensajeAjustesError"] = "Ningún ítem seleccionado."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            var ok = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    await _ritwebApi.PatchAsync($"api/TiposEventoDetalle/{id}", new { activo });
                    ok++;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error al cambiar estado detalle {Id}", id); }
            }
            TempData["MensajeAjustes"] = activo ? $"{ok} detalle(s) activados." : $"{ok} detalle(s) desactivados.";
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        public async Task<IActionResult> OnPostDeleteTipoEventoDetalleMultipleAsync(List<int> ids)
        {
            if (RedirSiNoSuperAdminAjustes() is { } redir) return redir;
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;
            if (ids == null || ids.Count == 0) { TempData["MensajeAjustesError"] = "Ningún ítem seleccionado."; return RedirectToPage(new { tab = "tiposeventodetalle" }); }
            var ok = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    await _ritwebApi.DeleteAsync($"api/TiposEventoDetalle/{id}");
                    ok++;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar detalle {Id}", id); }
            }
            TempData["MensajeAjustes"] = $"{ok} detalle(s) eliminados.";
            return RedirectToPage(new { tab = "tiposeventodetalle" });
        }

        /// <summary>Exporta la tabla Tipos de evento detalle a CSV (Excel).</summary>
        public async Task<IActionResult> OnGetExportTiposEventoDetalleAsync()
        {
            var redirect = await VerificarAccesoRitWebAsync();
            if (redirect != null) return redirect;

            List<TipoEventoDetalleDto>? detalles;
            List<TipoEventoDto>? tiposEvento;
            List<DisciplinaApi>? disciplinas;
            try
            {
                detalles = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle") ?? new List<TipoEventoDetalleDto>();
                tiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento") ?? new List<TipoEventoDto>();
                disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas") ?? new List<DisciplinaApi>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar datos para exportar TiposEventoDetalle");
                TempData["MensajeAjustesError"] = "No se pudo cargar la data para exportar.";
                return RedirectToPage(new { tab = "tiposeventodetalle" });
            }

            static string CsvCell(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                var t = s.Replace("\"", "\"\"");
                return t.Contains(';') || t.Contains('"') || t.Contains('\n') || t.Contains('\r') ? $"\"{t}\"" : t;
            }

            var grupos = detalles
                .GroupBy(x => new { x.Nombre, Codigo = x.Codigo ?? "", x.Activo, x.Nivel })
                .ToList();

            string GetGroupKey(IEnumerable<TipoEventoDetalleDto> grp)
            {
                var f = grp.First();
                return $"{f.Nombre}\t{f.Codigo ?? ""}\t{f.Activo}\t{f.Nivel}";
            }
            string? GetParentGroupKey(IEnumerable<TipoEventoDetalleDto> grp)
            {
                var f = grp.First();
                if (!f.TipoEventoDetallePadreId.HasValue) return null;
                var r = detalles!.FirstOrDefault(x => x.Id == f.TipoEventoDetallePadreId.Value);
                return r == null ? null : $"{r.Nombre}\t{r.Codigo ?? ""}\t{r.Activo}\t{r.Nivel}";
            }
            var keyToGroup = grupos.ToDictionary(g => GetGroupKey(g));
            var orderedKeys = new List<string>();
            void AddKeys(string? parentKey)
            {
                var children = grupos.Where(g => GetParentGroupKey(g) == parentKey).OrderBy(g => g.First().Nombre).ToList();
                foreach (var g in children)
                {
                    var k = GetGroupKey(g);
                    orderedKeys.Add(k);
                    AddKeys(k);
                }
            }
            AddKeys(null);
            var added = new HashSet<string>(orderedKeys);
            foreach (var g in grupos.OrderBy(g => g.First().Nombre))
            {
                var k = GetGroupKey(g);
                if (added.Contains(k)) continue;
                orderedKeys.Add(k);
                AddKeys(k);
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", "Id", "Nombre", "Tipo evento", "Disciplina", "Nivel", "Código", "Padre", "Estado", "Registros"));
            foreach (var key in orderedKeys)
            {
                var grupo = keyToGroup[key];
                var items = grupo.ToList();
                var primero = items[0];
                var padre = primero.TipoEventoDetallePadreId.HasValue ? detalles.FirstOrDefault(x => x.Id == primero.TipoEventoDetallePadreId.Value) : null;
                var tipoIds = items.Select(x => x.TipoEventoId).Distinct().ToList();
                var discIds = items.Select(x => x.DisciplinaId).Distinct().ToList();
                var tipoNombres = primero.Nivel == 1 ? string.Join(", ", tipoIds.Select(tid => tiposEvento.FirstOrDefault(t => t.Id == tid)?.Nombre ?? tid.ToString())) : "";
                var discNombres = primero.Nivel == 1 ? string.Join(", ", discIds.Select(did => disciplinas.FirstOrDefault(d => d.Id == did)?.Nombre ?? did.ToString())) : "";
                var ids = items.Select(x => x.Id).ToList();
                var idRango = items.Count == 1 ? "#" + primero.Id : "#" + ids.Min() + "–#" + ids.Max();
                sb.AppendLine(string.Join(";",
                    CsvCell(idRango),
                    CsvCell(primero.Nombre),
                    CsvCell(tipoNombres),
                    CsvCell(discNombres),
                    CsvCell("L" + primero.Nivel),
                    CsvCell(primero.Codigo),
                    CsvCell(padre?.Nombre),
                    CsvCell(primero.Activo ? "Activo" : "Inactivo"),
                    items.Count.ToString()));
            }

            var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", "TiposEventoDetalle.csv");
        }
    }
}
