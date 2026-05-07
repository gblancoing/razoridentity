using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Api;
using RazorIdentity.Models.ApiRitweb;
using RazorIdentity.Services;

namespace RazorIdentity.Pages;

[Authorize]
public class EventoPdfModel : PageModel
{
    private readonly IApiRitwebClient _ritwebApi;
    private readonly IRitApiClient _ritApi;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;

    public EventoPdfModel(
        IApiRitwebClient ritwebApi,
        IRitApiClient ritApi,
        UserManager<IdentityUser> userManager,
        ApplicationDbContext db)
    {
        _ritwebApi = ritwebApi;
        _ritApi = ritApi;
        _userManager = userManager;
        _db = db;
    }

    public EventoDto? Evento { get; set; }
    public string? Error { get; set; }

    /// <summary>Nombres resueltos para el PDF (en lugar de IDs).</summary>
    public string TipoEventoNombre { get; set; } = "—";
    public string ProyectoNombre { get; set; } = "—";
    public string SectorNombre { get; set; } = "—";
    public string EmpresaNombre { get; set; } = "—";
    public string TipoDocumentoNombre { get; set; } = "—";
    public string UsuarioRegistroNombre { get; set; } = "—";
    /// <summary>Disciplina del usuario que registró el evento (desde Usuarios por disciplina, RIT API).</summary>
    public string UsuarioRegistroDisciplinaNombre { get; set; } = "—";
    /// <summary>Turno a mostrar: del evento (API_Ritweb) o, si viene vacío, de la ficha del usuario creador (RIT_API).</summary>
    public string TurnoDisplay { get; set; } = "—";
    public string CriticidadNombre { get; set; } = "—";
    public string TipoAlertaNombre { get; set; } = "—";
    public string CausaNombre { get; set; } = "—";

    /// <summary>Si el tipo de evento requiere cierre (resuelto desde TipoEvento por si la API no lo envía en el evento).</summary>
    public bool EventoRequiereCierre { get; set; }
    /// <summary>Días desde creación hasta cierre (si está cerrado) o hasta hoy (si sigue abierto). Solo aplica cuando el tipo de evento requiere cierre.</summary>
    public int? EventoDiasAbierto { get; set; }

    /// <summary>Posts del foro de seguimiento (registro de gestiones). Participantes: misma disciplina y misma empresa que el usuario que registró el evento.</summary>
    public List<EventoForoPostDto> ForoPosts { get; set; } = new();
    /// <summary>Mensaje tras publicar en el foro (éxito o error).</summary>
    public string? MensajeForo { get; set; }
    /// <summary>Mensaje tras intentar cerrar el evento (éxito o error).</summary>
    public string? MensajeCierre { get; set; }
    /// <summary>Mensaje tras reasignar el evento (éxito o error).</summary>
    public string? MensajeReasignar { get; set; }

    /// <summary>True si el usuario actual puede reasignar: es el creador del evento o tiene rol Admin o Super_admin.</summary>
    public bool PuedeReasignar { get; set; }
    /// <summary>Disciplinas desde RIT API para el modal de reasignación.</summary>
    public List<DisciplinaApi> DisciplinasReasignar { get; set; } = new();

    /// <summary>URL de una tile de mapa estático (Esri World Imagery) centrada en el evento, para Anexo Imagen 3. Null si no hay lat/long.</summary>
    public string? MapaEstaticoUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0) { Error = "ID de evento no válido."; return Page(); }
        try
        {
            Evento = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{id}");
            if (Evento == null) { Error = "Evento no encontrado."; return Page(); }

            await ResolverNombresAsync();
            if (!string.IsNullOrWhiteSpace(Evento.Turno))
                TurnoDisplay = Evento.Turno.Trim();
            else if (!string.IsNullOrWhiteSpace(Evento.UserId))
            {
                try
                {
                    var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{Evento.UserId}/Ficha");
                    if (!string.IsNullOrWhiteSpace(ficha?.Turno))
                        TurnoDisplay = ficha.Turno.Trim();
                }
                catch { /* RIT API puede no exponer Ficha */ }
            }
            await CargarForoAsync(id);
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                PuedeReasignar = string.Equals(Evento.UserId, user.Id, StringComparison.OrdinalIgnoreCase)
                    || User.IsInRole("Admin")
                    || User.IsInRole("Super_admin")
                    || User.IsInRole("Super_Admin");
            }
            if (PuedeReasignar)
            {
                try
                {
                    var disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas");
                    DisciplinasReasignar = disciplinas?.OrderBy(d => d.Nombre).ToList() ?? new List<DisciplinaApi>();
                }
                catch { /* RIT API puede no estar disponible */ }
            }
            MensajeForo = TempData["MensajeForo"] as string;
            MensajeCierre = TempData["MensajeCierre"] as string;
            MensajeReasignar = TempData["MensajeReasignar"] as string;
            if (Evento.Latitude.HasValue && Evento.Longitude.HasValue)
                MapaEstaticoUrl = BuildMapaEstaticoUrl(Evento.Latitude.Value, Evento.Longitude.Value);
        }
        catch (Exception)
        {
            Error = "No se pudo cargar el evento. Compruebe que API_Ritweb esté disponible.";
        }
        return Page();
    }

    private static string BuildMapaEstaticoUrl(decimal latitude, decimal longitude)
    {
        const int zoom = 14;
        double lat = (double)latitude;
        double lon = (double)longitude;
        double n = Math.Pow(2, zoom);
        double latRad = lat * Math.PI / 180.0;
        int tileX = (int)Math.Floor((lon + 180.0) / 360.0 * n);
        int tileY = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{zoom}/{tileY}/{tileX}";
    }

    private async Task ResolverNombresAsync()
    {
        if (Evento == null) return;

        try
        {
            var tiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
            var tipo = tiposEvento?.FirstOrDefault(t => t.Id == Evento.TipoEventoId);
            TipoEventoNombre = tipo?.Nombre?.Trim() ?? Evento.TipoEventoId.ToString();
            EventoRequiereCierre = tipo?.RequiereCierre ?? Evento.RequiereCierre;
            if (EventoRequiereCierre)
            {
                var fin = Evento.FechaCierre ?? DateTime.UtcNow;
                EventoDiasAbierto = (int)(fin - Evento.FechaCreacion).TotalDays;
            }

            var sectores = await _ritwebApi.GetListAsync<SectorDto>("api/Sectores");
            var sector = Evento.SectorId.HasValue ? sectores?.FirstOrDefault(s => s.Id == Evento.SectorId.Value) : null;
            SectorNombre = sector?.Nombre?.Trim() ?? (Evento.SectorId?.ToString() ?? "—");

            var tiposDoc = await _ritwebApi.GetListAsync<TipoDocumentoRespaldoDto>("api/TiposDocumentoRespaldo");
            var tipoDoc = Evento.TipoDocumentoRespaldoId.HasValue ? tiposDoc?.FirstOrDefault(t => t.Id == Evento.TipoDocumentoRespaldoId.Value) : null;
            TipoDocumentoNombre = tipoDoc?.Nombre?.Trim() ?? (Evento.TipoDocumentoRespaldoId?.ToString() ?? "—");

            var criticidades = await _ritwebApi.GetListAsync<CriticidadDto>("api/Criticidades");
            var crit = Evento.CriticidadId.HasValue ? criticidades?.FirstOrDefault(c => c.Id == Evento.CriticidadId.Value) : null;
            CriticidadNombre = crit?.Nombre?.Trim() ?? (Evento.CriticidadId?.ToString() ?? "—");

            var tiposAlerta = await _ritwebApi.GetListAsync<TipoAlertaDto>("api/TiposAlerta");
            var alerta = Evento.TipoAlertaId.HasValue ? tiposAlerta?.FirstOrDefault(a => a.Id == Evento.TipoAlertaId.Value) : null;
            TipoAlertaNombre = alerta?.Nombre?.Trim() ?? (Evento.TipoAlertaId?.ToString() ?? "—");

            var detalles = await _ritwebApi.GetListAsync<TipoEventoDetalleDto>("api/TiposEventoDetalle");
            var detalle = Evento.TipoEventoDetalleId.HasValue ? detalles?.FirstOrDefault(d => d.Id == Evento.TipoEventoDetalleId.Value) : null;
            CausaNombre = detalle?.Nombre?.Trim() ?? (Evento.TipoEventoDetalleId?.ToString() ?? "—");

            var proyectos = await _ritApi.GetListAsync<ProyectoApi>("api/Proyectos");
            var proy = proyectos?.FirstOrDefault(p => p.Id == Evento.ProyectoId);
            ProyectoNombre = proy?.Nombre?.Trim() ?? Evento.ProyectoId.ToString();

            if (Evento.EmpresaColaboradoraId > 0)
            {
                if (!string.IsNullOrWhiteSpace(Evento.EmpresaColaboradoraNombre))
                    EmpresaNombre = Evento.EmpresaColaboradoraNombre.Trim();
                else
                {
                    try
                    {
                        var empresas = await _ritApi.GetListAsync<EmpresaColaboradoraApi>($"api/EmpresasColaboradoras?proyectoId={Evento.ProyectoId}");
                        var emp = empresas?.FirstOrDefault(e => e.Id == Evento.EmpresaColaboradoraId);
                        EmpresaNombre = emp?.Nombre?.Trim() ?? Evento.EmpresaColaboradoraId.ToString();
                    }
                    catch { EmpresaNombre = Evento.EmpresaColaboradoraId.ToString(); }
                }
            }
            else
                EmpresaNombre = "—";

            if (!string.IsNullOrWhiteSpace(Evento.UsuarioNombre))
                UsuarioRegistroNombre = Evento.UsuarioNombre.Trim();
            else if (!string.IsNullOrWhiteSpace(Evento.UserId))
            {
                var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == Evento.UserId);
                if (profile?.FullName != null && !string.IsNullOrWhiteSpace(profile.FullName))
                    UsuarioRegistroNombre = profile.FullName.Trim();
                else
                {
                    var identity = await _userManager.FindByIdAsync(Evento.UserId);
                    UsuarioRegistroNombre = identity?.Email ?? identity?.UserName ?? Evento.UserId;
                }
            }

            // Disciplina del usuario que registró (Usuarios por disciplina, RIT API)
            if (!string.IsNullOrWhiteSpace(Evento.UserId))
            {
                try
                {
                    var usuariosDisciplina = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina");
                    var asignacion = usuariosDisciplina?.FirstOrDefault(ud => string.Equals(ud.UserId, Evento.UserId, StringComparison.OrdinalIgnoreCase));
                    if (asignacion != null)
                    {
                        var disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas");
                        var disciplina = disciplinas?.FirstOrDefault(d => d.Id == asignacion.DisciplinaId);
                        if (!string.IsNullOrWhiteSpace(disciplina?.Nombre))
                            UsuarioRegistroDisciplinaNombre = disciplina.Nombre.Trim();
                    }
                }
                catch { /* RIT API puede no exponer UsuariosDisciplina */ }
            }
        }
        catch
        {
            TipoEventoNombre = Evento.TipoEventoId.ToString();
            ProyectoNombre = Evento.ProyectoId.ToString();
            SectorNombre = Evento.SectorId?.ToString() ?? "—";
            EmpresaNombre = Evento.EmpresaColaboradoraId > 0 ? Evento.EmpresaColaboradoraId.ToString() : "—";
            TipoDocumentoNombre = Evento.TipoDocumentoRespaldoId?.ToString() ?? "—";
            UsuarioRegistroNombre = Evento.UserId ?? "—";
        }
    }

    private async Task CargarForoAsync(int eventoId)
    {
        try
        {
            var list = await _ritwebApi.GetListAsync<EventoForoPostDto>($"api/Eventos/{eventoId}/Foro");
            ForoPosts = list ?? new List<EventoForoPostDto>();
        }
        catch
        {
            ForoPosts = new List<EventoForoPostDto>();
        }
    }

    /// <summary>Publica un mensaje en el foro de seguimiento. La API valida que el usuario pertenezca a la misma empresa y disciplina que el evento (RIT API: UsuariosEmpresaColaboradora, UsuariosDisciplina).</summary>
    public async Task<IActionResult> OnPostNuevoSeguimientoAsync(int id, string contenido, bool esPrioridad = false, int? parentId = null)
    {
        if (id <= 0) { TempData["MensajeForo"] = "ID de evento no válido."; return RedirectToPage(new { id }); }
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        contenido = (contenido ?? "").Trim();
        if (string.IsNullOrEmpty(contenido)) { TempData["MensajeForo"] = "Escriba un mensaje."; return RedirectToPage(new { id }); }

        var evento = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{id}");
        if (evento == null) { TempData["MensajeForo"] = "Evento no encontrado."; return RedirectToPage(new { id }); }

        int empresaColaboradoraId = 0;
        int disciplinaId = 0;
        try
        {
            var asignacionesEmpresa = await _ritApi.GetListAsync<UsuarioEmpresaColaboradoraApi>("api/UsuariosEmpresaColaboradora");
            var misEmpresas = (asignacionesEmpresa ?? new List<UsuarioEmpresaColaboradoraApi>()).Where(x => x.UserId == user.Id).ToList();
            if (misEmpresas.Count > 0)
            {
                var queCoincide = misEmpresas.FirstOrDefault(x => x.EmpresaColaboradoraId == evento.EmpresaColaboradoraId);
                empresaColaboradoraId = queCoincide?.EmpresaColaboradoraId ?? misEmpresas[0].EmpresaColaboradoraId;
                if (evento.DisciplinaId.HasValue && queCoincide?.DisciplinaId.HasValue == true)
                    disciplinaId = queCoincide.DisciplinaId.Value;
                else if (queCoincide?.DisciplinaId.HasValue == true)
                    disciplinaId = queCoincide.DisciplinaId.Value;
                else if (misEmpresas.Any(x => x.DisciplinaId == evento.DisciplinaId))
                    disciplinaId = evento.DisciplinaId!.Value;
                else if (misEmpresas[0].DisciplinaId.HasValue)
                    disciplinaId = misEmpresas[0].DisciplinaId.Value;
            }
            if (disciplinaId == 0 && evento.DisciplinaId.HasValue)
            {
                var asignacionesDisc = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina");
                var misDisc = (asignacionesDisc ?? new List<UsuarioDisciplinaApi>()).Where(x => x.UserId == user.Id).ToList();
                var discDelEvento = evento.DisciplinaId.Value;
                if (misDisc.Any(x => x.DisciplinaId == discDelEvento))
                    disciplinaId = discDelEvento;
                else if (misDisc.Count > 0)
                    disciplinaId = misDisc[0].DisciplinaId;
            }
        }
        catch { /* Si RIT no está disponible, enviamos 0 y la API devolverá 403 si aplica */ }

        try
        {
            await _ritwebApi.PostAsync<object, EventoForoPostDto>($"api/Eventos/{id}/Foro", new
            {
                contenido,
                esPrioridad,
                parentId,
                userId = user.Id,
                empresaColaboradoraId,
                disciplinaId
            });
            TempData["MensajeForo"] = "Mensaje publicado.";
        }
        catch (Exception ex)
        {
            var msg = ex.Message ?? "";
            if (msg.Contains("403") || msg.Contains("Forbidden")) TempData["MensajeForo"] = "No puede publicar: solo participan usuarios de la misma empresa y disciplina que quien registró el evento. Verifique su asignación en RIT.";
            else TempData["MensajeForo"] = "No se pudo publicar. " + msg;
        }
        return RedirectToPage(new { id });
    }

    /// <summary>Cierra el evento (para alertas que requieren cierre). Envía descripción de cierre y opcionalmente una foto de respaldo. API: PATCH api/Eventos/{id} + POST Adjuntos/subir con tipo FotoCierre.</summary>
    public async Task<IActionResult> OnPostCerrarEventoAsync(int id, string descripcionCierre, IFormFile? fotoCierre)
    {
        if (id <= 0) { TempData["MensajeCierre"] = "ID de evento no válido."; return RedirectToPage(new { id }); }
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var evento = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{id}");
        if (evento == null) { TempData["MensajeCierre"] = "Evento no encontrado."; return RedirectToPage(new { id }); }
        if (evento.FechaCierre.HasValue) { TempData["MensajeCierre"] = "El evento ya está cerrado."; return RedirectToPage(new { id }); }
        // Resolver si requiere cierre desde la tabla Tipo de evento (CRUD), no solo del DTO del evento
        var tiposEvento = await _ritwebApi.GetListAsync<TipoEventoDto>("api/TiposEvento");
        var tipo = tiposEvento?.FirstOrDefault(t => t.Id == evento.TipoEventoId);
        var requiereCierre = tipo?.RequiereCierre ?? evento.RequiereCierre;
        if (!requiereCierre) { TempData["MensajeCierre"] = "Este tipo de evento no requiere cierre."; return RedirectToPage(new { id }); }

        descripcionCierre = (descripcionCierre ?? "").Trim();
        if (string.IsNullOrEmpty(descripcionCierre)) { TempData["MensajeCierre"] = "Debe indicar un comentario que justifique el cierre."; return RedirectToPage(new { id }); }

        var fechaCierreUtc = DateTime.UtcNow;
        try
        {
            var cierreOk = false;
            string? errorPatch = null;
            string? errorPost = null;

            try
            {
                await _ritwebApi.PatchAsync($"api/Eventos/{id}", new { descripcionCierre, fechaCierre = fechaCierreUtc });
                cierreOk = true;
            }
            catch (Exception ex)
            {
                errorPatch = ex.Message ?? "Error desconocido";
                try
                {
                    await _ritwebApi.PostAsync<object, EventoDto>($"api/Eventos/{id}/Cierre", new { descripcionCierre });
                    cierreOk = true;
                }
                catch (Exception ex2)
                {
                    errorPost = ex2.Message ?? "Error desconocido";
                }
            }

            if (!cierreOk)
            {
                var textoPost = errorPost ?? "";
                var esNoRequiereCierre = (errorPatch != null && errorPatch.Contains("no requiere cierre", StringComparison.OrdinalIgnoreCase))
                    || textoPost.Contains("no requiere cierre", StringComparison.OrdinalIgnoreCase);
                string mensaje;
                if (esNoRequiereCierre)
                    mensaje = "La API indica que este evento no requiere cierre. Compruebe en API_Ritweb que al validar el cierre se cargue el Tipo de evento (p. ej. .Include(e => e.TipoEvento) o consulta a TiposEvento por TipoEventoId); si TipoEvento es null, la validación falla. Ver docs/API-EVENTO-CIERRE.md.";
                else
                {
                    var detalle = new List<string>();
                    if (errorPatch != null) detalle.Add("PATCH: " + errorPatch);
                    if (errorPost != null) detalle.Add("POST Cierre: " + errorPost);
                    mensaje = "No se pudo cerrar el evento. " + (detalle.Count > 0 ? string.Join(" ", detalle) : "Compruebe que API_Ritweb esté en ejecución.");
                }
                TempData["MensajeCierre"] = mensaje;
                return RedirectToPage(new { id });
            }

            if (fotoCierre != null && fotoCierre.Length > 0)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    content.Add(new StringContent("FotoCierre"), "tipoAdjunto");
                    var fileContent = new StreamContent(fotoCierre.OpenReadStream());
                    if (!string.IsNullOrWhiteSpace(fotoCierre.ContentType))
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(fotoCierre.ContentType);
                    content.Add(fileContent, "archivo", fotoCierre.FileName ?? "foto-cierre");
                    await _ritwebApi.PostMultipartAsync<EventoAdjuntoDto>($"api/Eventos/{id}/Adjuntos/subir", content);
                }
                catch (Exception ex)
                {
                    TempData["MensajeCierre"] = "Evento cerrado correctamente, pero la foto de cierre no se pudo subir: " + (ex.Message ?? "error");
                    return RedirectToPage(new { id });
                }
            }
            TempData["MensajeCierre"] = "Evento cerrado correctamente.";
        }
        catch (Exception ex)
        {
            var msg = ex.Message ?? "";
            if (ex is HttpRequestException httpEx && httpEx.Message?.Contains("404") == true)
                msg += " (¿La API expone PUT api/Eventos/{id} con descripcionCierre y fechaCierre?)";
            TempData["MensajeCierre"] = "No se pudo cerrar el evento. " + msg;
        }
        return RedirectToPage(new { id });
    }

    /// <summary>Devuelve los usuarios asignados a una disciplina (para el modal de reasignación). JSON: [{ userId, nombre }].</summary>
    public async Task<IActionResult> OnGetUsuariosPorDisciplinaAsync(int disciplinaId)
    {
        if (disciplinaId <= 0) return new JsonResult(Array.Empty<object>());
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var usuariosDisciplina = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina");
            var userIds = (usuariosDisciplina ?? new List<UsuarioDisciplinaApi>())
                .Where(ud => ud.DisciplinaId == disciplinaId)
                .Select(ud => ud.UserId)
                .Distinct()
                .ToList();
            if (userIds.Count == 0) return new JsonResult(Array.Empty<object>());

            var perfiles = await _db.UserProfiles
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.FullName })
                .ToListAsync();
            var perfilPorUserId = perfiles.ToDictionary(p => p.UserId, p => p.FullName);

            var payload = new List<object>();
            foreach (var uid in userIds.OrderBy(x => x))
            {
                var nombre = (perfilPorUserId.TryGetValue(uid, out var fullName) && !string.IsNullOrWhiteSpace(fullName))
                    ? fullName!.Trim()
                    : (await _userManager.FindByIdAsync(uid))?.Email ?? (await _userManager.FindByIdAsync(uid))?.UserName ?? uid;
                payload.Add(new { userId = uid, nombre });
            }
            return new JsonResult(payload);
        }
        catch (Exception)
        {
            return new JsonResult(Array.Empty<object>());
        }
    }

    /// <summary>Reasigna el evento a otra disciplina y otro usuario. Solo puede el creador del evento o Admin/Super_admin. El historial de creación se conserva en la API (FechaCreacion, etc.).</summary>
    public async Task<IActionResult> OnPostReasignarEventoAsync(int id, int disciplinaId, string userId)
    {
        if (id <= 0) { TempData["MensajeReasignar"] = "ID de evento no válido."; return RedirectToPage(new { id }); }
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var evento = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{id}");
        if (evento == null) { TempData["MensajeReasignar"] = "Evento no encontrado."; return RedirectToPage(new { id }); }

        var puedeReasignar = string.Equals(evento.UserId, user.Id, StringComparison.OrdinalIgnoreCase)
            || User.IsInRole("Admin")
            || User.IsInRole("Super_admin")
            || User.IsInRole("Super_Admin");
        if (!puedeReasignar)
        {
            TempData["MensajeReasignar"] = "No tiene permiso para reasignar este evento.";
            return RedirectToPage(new { id });
        }

        userId = (userId ?? "").Trim();
        if (string.IsNullOrEmpty(userId)) { TempData["MensajeReasignar"] = "Debe seleccionar un usuario."; return RedirectToPage(new { id }); }
        if (disciplinaId <= 0) { TempData["MensajeReasignar"] = "Debe seleccionar una disciplina."; return RedirectToPage(new { id }); }

        try
        {
            await _ritwebApi.PatchAsync($"api/Eventos/{id}", new { userId, disciplinaId });
            TempData["MensajeReasignar"] = "Evento reasignado correctamente.";
        }
        catch (Exception ex)
        {
            var msg = ex.Message ?? "";
            try
            {
                await _ritwebApi.PutAsync<object, EventoDto>($"api/Eventos/{id}", new
                {
                    userId,
                    disciplinaId,
                    evento.EmpresaColaboradoraId,
                    evento.TipoEventoId,
                    evento.TipoEventoDetalleId,
                    evento.ProyectoId,
                    evento.SectorId,
                    evento.FechaEvento,
                    turno = evento.Turno,
                    evento.TipoAlertaId,
                    evento.CriticidadId,
                    evento.ProfesionalNotificadoNombre,
                    evento.ProfesionalNotificadoCargo,
                    evento.TipoDocumentoRespaldoId,
                    evento.Descripcion,
                    evento.ReferenciaDocumentoRespaldo,
                    evento.NumeroDocumentoProtocolo,
                    evento.CodigoVp
                });
                TempData["MensajeReasignar"] = "Evento reasignado correctamente.";
            }
            catch (Exception ex2)
            {
                TempData["MensajeReasignar"] = "No se pudo reasignar. " + (ex2.Message ?? msg);
            }
        }
        return RedirectToPage(new { id });
    }

    /// <summary>Sirve el archivo de un adjunto para mostrarlo en la página (imágenes) o descargarlo.</summary>
    public async Task<IActionResult> OnGetAdjuntoAsync(int id, int adjuntoId)
    {
        if (id <= 0 || adjuntoId <= 0) return NotFound();
        try
        {
            var evento = await _ritwebApi.GetAsync<EventoDto>($"api/Eventos/{id}");
            var adjunto = evento?.Adjuntos?.FirstOrDefault(a => a.Id == adjuntoId);
            if (adjunto == null) return NotFound();

            byte[]? bytes = null;
            var rutas = new[]
            {
                $"api/Eventos/{id}/Adjuntos/{adjuntoId}/descargar",
                $"api/Eventos/{id}/Adjuntos/{adjuntoId}/archivo",
                $"api/Adjuntos/{adjuntoId}/descargar",
                $"api/Adjuntos/{adjuntoId}/archivo"
            };
            foreach (var ruta in rutas)
            {
                bytes = await _ritwebApi.GetByteArrayAsync(ruta);
                if (bytes != null && bytes.Length > 0) break;
            }
            if (bytes == null && !string.IsNullOrWhiteSpace(adjunto.RutaArchivo))
            {
                var rutaArchivo = adjunto.RutaArchivo.Trim().TrimStart('/');
                if (!string.IsNullOrEmpty(rutaArchivo))
                    bytes = await _ritwebApi.GetByteArrayAsync(rutaArchivo);
            }
            if (bytes == null || bytes.Length == 0) return NotFound();

            var contentType = !string.IsNullOrWhiteSpace(adjunto.ContentType)
                ? adjunto.ContentType
                : "application/octet-stream";
            return File(bytes, contentType);
        }
        catch
        {
            return NotFound();
        }
    }
}
