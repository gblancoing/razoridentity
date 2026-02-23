using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;

namespace RazorIdentity.Pages
{
    [Authorize(Roles = "Super_admin,Super_Admin")]
    public class ProtegidoModel : PageModel
    {
        private readonly IRitApiClient _ritApi;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ProtegidoModel> _logger;

        public ProtegidoModel(IRitApiClient ritApi, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IEmailSender emailSender, ApplicationDbContext dbContext, ILogger<ProtegidoModel> logger)
        {
            _ritApi = ritApi;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _dbContext = dbContext;
            _logger = logger;
        }

        public string? ErrorApi { get; set; }
        public string Tab { get; set; } = "usuarios";

        /// <summary>Email o nombre del usuario actual (para el sidebar).</summary>
        public string? CurrentUserEmail { get; set; }
        /// <summary>Iniciales del usuario actual (para el avatar en el sidebar).</summary>
        public string? CurrentUserInitials { get; set; }

        /// <summary>Filtro de búsqueda para la pestaña Usuarios.</summary>
        public string? UsuarioBusqueda { get; set; }

        /// <summary>Mensaje de éxito o error en la pestaña Usuarios (crear/cambiar rol).</summary>
        public string? MensajeUsuarios { get; set; }
        public bool MensajeUsuariosEsError { get; set; }

        /// <summary>Mensaje de éxito o error en la pestaña Usuarios por disciplina.</summary>
        public string? MensajeDisciplina { get; set; }
        public bool MensajeDisciplinaEsError { get; set; }

        public List<PaisApi> Paises { get; set; } = new();
        public List<RegionApi> Regiones { get; set; } = new();
        public List<ProyectoApi> Proyectos { get; set; } = new();
        public List<CentroCostoApi> CentrosCosto { get; set; } = new();
        public List<DisciplinaApi> Disciplinas { get; set; } = new();
        public List<UsuarioCentroApi> UsuariosCentro { get; set; } = new();
        public List<UsuarioDisciplinaApi> UsuariosDisciplina { get; set; } = new();
        public List<EmpresaColaboradoraApi> EmpresasColaboradoras { get; set; } = new();
        public List<UsuarioEmpresaColaboradoraApi> UsuariosEmpresaColaboradora { get; set; } = new();
        public List<AppApi> Apps { get; set; } = new();
        public List<UsuarioAppApi> UsuariosApp { get; set; } = new();
        public List<IdentityUser> Usuarios { get; set; } = new();

        /// <summary>Roles del sistema para dropdowns.</summary>
        public List<string> Roles { get; set; } = new();

        /// <summary>Usuarios con sus roles actuales (para la pestaña Gestión de usuarios).</summary>
        public List<(IdentityUser User, IList<string> Roles)> UsuariosConRoles { get; set; } = new();

        /// <summary>Texto a mostrar en tablas por UserId: correo y nombre (nunca el Id).</summary>
        public Dictionary<string, string> UserDisplayInfo { get; set; } = new();

        public int? EditPaisId { get; set; }
        public int? EditRegionId { get; set; }
        public int? EditProyectoId { get; set; }
        public int? EditCentroId { get; set; }
        public int? EditDisciplinaId { get; set; }
        public int? EditUsuarioCentroId { get; set; }
        public int? EditEmpresaId { get; set; }
        public int? EditAppId { get; set; }

        /// <summary>Mensaje de éxito o error en la pestaña Empresas colaboradoras / Usuarios por empresa.</summary>
        public string? MensajeEmpresa { get; set; }
        public bool MensajeEmpresaEsError { get; set; }

        /// <summary>Mensaje de éxito o error en la pestaña Apps / Usuarios por app.</summary>
        public string? MensajeApp { get; set; }
        public bool MensajeAppEsError { get; set; }

        private async Task CargarDatosAsync(string? usuarioBusqueda = null)
        {
            // Roles para gestión de usuarios
            Roles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();

            // Usuarios: filtrar por búsqueda si aplica (para pestaña Usuarios)
            var usuariosQuery = _userManager.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(usuarioBusqueda))
            {
                var busqueda = usuarioBusqueda.Trim();
                usuariosQuery = usuariosQuery.Where(u =>
                    (u.Email != null && u.Email.Contains(busqueda)) ||
                    (u.UserName != null && u.UserName.Contains(busqueda)));
            }
            Usuarios = usuariosQuery.OrderBy(u => u.Email ?? u.UserName ?? "").ToList();

            // Usuarios con sus roles (para la tabla de la pestaña Usuarios)
            UsuariosConRoles = new List<(IdentityUser User, IList<string> Roles)>();
            foreach (var u in Usuarios)
                UsuariosConRoles.Add((u, await _userManager.GetRolesAsync(u)));


            try
            {
                Paises = await _ritApi.GetListAsync<PaisApi>("api/Paises");
                Regiones = await _ritApi.GetListAsync<RegionApi>("api/Regiones");
                Proyectos = await _ritApi.GetListAsync<ProyectoApi>("api/Proyectos");
                CentrosCosto = await _ritApi.GetListAsync<CentroCostoApi>("api/CentrosCosto");
                UsuariosCentro = await _ritApi.GetListAsync<UsuarioCentroApi>("api/UsuariosCentro");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar datos de la API");
                ErrorApi = "No se pudo conectar con Rit_Api. Compruebe que esté en ejecución (ApiSettings:BaseUrl).";
            }

            // Disciplinas y UsuariosDisciplina se cargan por separado para que si uno falla (ej. UsuariosDisciplina no existe aún) la pestaña Disciplinas siga funcionando
            try { Disciplinas = await _ritApi.GetListAsync<DisciplinaApi>("api/Disciplinas"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar Disciplinas"); }
            try { UsuariosDisciplina = await _ritApi.GetListAsync<UsuarioDisciplinaApi>("api/UsuariosDisciplina"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar UsuariosDisciplina"); }
            try { EmpresasColaboradoras = await _ritApi.GetListAsync<EmpresaColaboradoraApi>("api/EmpresasColaboradoras"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar EmpresasColaboradoras"); }
            try { UsuariosEmpresaColaboradora = await _ritApi.GetListAsync<UsuarioEmpresaColaboradoraApi>("api/UsuariosEmpresaColaboradora"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar UsuariosEmpresaColaboradora"); }
            try { Apps = await _ritApi.GetListAsync<AppApi>("api/Apps"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar Apps"); }
            try { UsuariosApp = await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cargar UsuariosApp"); }

            // UserDisplayInfo para todos los UserIds que aparecen en tablas (usuarios + asignaciones)
            var idsEnAsignaciones = UsuariosCentro.Select(uc => uc.UserId)
                .Union(UsuariosDisciplina.Select(ud => ud.UserId))
                .Union(UsuariosEmpresaColaboradora.Select(ue => ue.UserId))
                .Union(UsuariosApp.Select(ua => ua.UserId))
                .Distinct()
                .ToList();
            var todosLosIds = Usuarios.Select(u => u.Id).Union(idsEnAsignaciones).Distinct().ToList();
            var perfiles = await _dbContext.UserProfiles.Where(p => todosLosIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.FullName);
            var usuariosParaDisplay = await _userManager.Users.Where(u => todosLosIds.Contains(u.Id)).ToListAsync();
            UserDisplayInfo = new Dictionary<string, string>();
            foreach (var u in usuariosParaDisplay)
            {
                var email = u.Email ?? u.UserName ?? "";
                var fullName = perfiles.GetValueOrDefault(u.Id)?.Trim();
                UserDisplayInfo[u.Id] = !string.IsNullOrEmpty(fullName) ? $"{fullName} ({email})" : email;
            }
        }

        public async Task OnGetAsync(string? tab, string? usuarioBusqueda, int? editPaisId, int? editRegionId, int? editProyectoId, int? editCentroId, int? editDisciplinaId, int? editUsuarioCentroId, int? editEmpresaId, int? editAppId, string? mensajeUsuarios, bool mensajeUsuariosEsError = false, string? mensajeDisciplina = null, bool mensajeDisciplinaEsError = false, string? mensajeEmpresa = null, bool mensajeEmpresaEsError = false, string? mensajeApp = null, bool mensajeAppEsError = false)
        {
            Tab = tab ?? "usuarios";
            UsuarioBusqueda = usuarioBusqueda;
            MensajeUsuarios = mensajeUsuarios;
            MensajeUsuariosEsError = mensajeUsuariosEsError;
            MensajeDisciplina = mensajeDisciplina;
            MensajeDisciplinaEsError = mensajeDisciplinaEsError;
            MensajeEmpresa = mensajeEmpresa;
            MensajeEmpresaEsError = mensajeEmpresaEsError;
            MensajeApp = mensajeApp;
            MensajeAppEsError = mensajeAppEsError;
            EditPaisId = editPaisId;
            EditRegionId = editRegionId;
            EditProyectoId = editProyectoId;
            EditCentroId = editCentroId;
            EditDisciplinaId = editDisciplinaId;
            EditUsuarioCentroId = editUsuarioCentroId;
            EditEmpresaId = editEmpresaId;
            EditAppId = editAppId;
            await CargarDatosAsync(usuarioBusqueda);
            var user = await _userManager.GetUserAsync(User);
            var email = user?.Email ?? user?.UserName;
            CurrentUserEmail = email ?? "Admin";
            CurrentUserInitials = (CurrentUserEmail.Length >= 2 ? CurrentUserEmail.Substring(0, 2) : "SA").ToUpperInvariant();
        }

        public async Task<IActionResult> OnPostCreatePaisAsync(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "paises" });
            try
            {
                await _ritApi.PostAsync<object, PaisApi>("api/Paises", new { Nombre = nombre.Trim() });
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear país"); }
            return RedirectToPage(new { tab = "paises" });
        }

        public async Task<IActionResult> OnPostUpdatePaisAsync(int id, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "paises" });
            try { await _ritApi.PutAsync<object, PaisApi>($"api/Paises/{id}", new { Nombre = nombre.Trim() }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar país"); }
            return RedirectToPage(new { tab = "paises" });
        }

        public async Task<IActionResult> OnPostDeletePaisAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/Paises/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar país"); }
            return RedirectToPage(new { tab = "paises" });
        }

        public async Task<IActionResult> OnPostCreateRegionAsync(string nombre, int paisId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "regiones" });
            try { await _ritApi.PostAsync<object, RegionApi>("api/Regiones", new { Nombre = nombre.Trim(), PaisId = paisId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear región"); }
            return RedirectToPage(new { tab = "regiones" });
        }

        public async Task<IActionResult> OnPostUpdateRegionAsync(int id, string nombre, int paisId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "regiones" });
            try { await _ritApi.PutAsync<object, RegionApi>($"api/Regiones/{id}", new { Nombre = nombre.Trim(), PaisId = paisId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar región"); }
            return RedirectToPage(new { tab = "regiones" });
        }

        public async Task<IActionResult> OnPostDeleteRegionAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/Regiones/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar región"); }
            return RedirectToPage(new { tab = "regiones" });
        }

        public async Task<IActionResult> OnPostCreateProyectoAsync(string nombre, int regionId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "proyectos" });
            try { await _ritApi.PostAsync<object, ProyectoApi>("api/Proyectos", new { Nombre = nombre.Trim(), RegionId = regionId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear proyecto"); }
            return RedirectToPage(new { tab = "proyectos" });
        }

        public async Task<IActionResult> OnPostUpdateProyectoAsync(int id, string nombre, int regionId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "proyectos" });
            try { await _ritApi.PutAsync<object, ProyectoApi>($"api/Proyectos/{id}", new { Nombre = nombre.Trim(), RegionId = regionId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar proyecto"); }
            return RedirectToPage(new { tab = "proyectos" });
        }

        public async Task<IActionResult> OnPostDeleteProyectoAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/Proyectos/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar proyecto"); }
            return RedirectToPage(new { tab = "proyectos" });
        }

        public async Task<IActionResult> OnPostCreateCentroAsync(string nombre, int proyectoId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "centros" });
            try { await _ritApi.PostAsync<object, CentroCostoApi>("api/CentrosCosto", new { Nombre = nombre.Trim(), ProyectoId = proyectoId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear centro"); }
            return RedirectToPage(new { tab = "centros" });
        }

        public async Task<IActionResult> OnPostUpdateCentroAsync(int id, string nombre, int proyectoId)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "centros" });
            try { await _ritApi.PutAsync<object, CentroCostoApi>($"api/CentrosCosto/{id}", new { Nombre = nombre.Trim(), ProyectoId = proyectoId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar centro"); }
            return RedirectToPage(new { tab = "centros" });
        }

        public async Task<IActionResult> OnPostDeleteCentroAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/CentrosCosto/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar centro"); }
            return RedirectToPage(new { tab = "centros" });
        }

        public async Task<IActionResult> OnPostCreateUsuarioCentroAsync(string userId, int centroCostoId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return RedirectToPage(new { tab = "usuarioscentro" });
            try { await _ritApi.PostAsync<object, UsuarioCentroApi>("api/UsuariosCentro", new { UserId = userId.Trim(), CentroCostoId = centroCostoId }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error asignar usuario a centro"); }
            return RedirectToPage(new { tab = "usuarioscentro" });
        }

        public async Task<IActionResult> OnPostDeleteUsuarioCentroAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/UsuariosCentro/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error quitar usuario de centro"); }
            return RedirectToPage(new { tab = "usuarioscentro" });
        }

        public async Task<IActionResult> OnPostCreateEmpresaAsync(string nombre, int centroCostoId, string? rut, string? direccion, string? ubicacion, DateTime? fechaInicioContrato, DateTime? fechaTerminoEsperadaContrato, string? descripcionServicios, string? email, string? telefono)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "empresas" });
            var body = new { Nombre = nombre.Trim(), CentroCostoId = centroCostoId, Rut = rut?.Trim(), Direccion = direccion?.Trim(), Ubicacion = ubicacion?.Trim(), FechaInicioContrato = fechaInicioContrato, FechaTerminoEsperadaContrato = fechaTerminoEsperadaContrato, DescripcionServicios = descripcionServicios?.Trim(), Email = email?.Trim(), Telefono = telefono?.Trim() };
            try { await _ritApi.PostAsync<object, EmpresaColaboradoraApi>("api/EmpresasColaboradoras", body); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear empresa colaboradora"); }
            return RedirectToPage(new { tab = "empresas" });
        }

        public async Task<IActionResult> OnPostUpdateEmpresaAsync(int id, string nombre, int centroCostoId, string? rut, string? direccion, string? ubicacion, DateTime? fechaInicioContrato, DateTime? fechaTerminoEsperadaContrato, string? descripcionServicios, string? email, string? telefono)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "empresas" });
            var body = new { Nombre = nombre.Trim(), CentroCostoId = centroCostoId, Rut = rut?.Trim(), Direccion = direccion?.Trim(), Ubicacion = ubicacion?.Trim(), FechaInicioContrato = fechaInicioContrato, FechaTerminoEsperadaContrato = fechaTerminoEsperadaContrato, DescripcionServicios = descripcionServicios?.Trim(), Email = email?.Trim(), Telefono = telefono?.Trim() };
            try { await _ritApi.PutAsync<object, EmpresaColaboradoraApi>($"api/EmpresasColaboradoras/{id}", body); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar empresa colaboradora"); }
            return RedirectToPage(new { tab = "empresas" });
        }

        public async Task<IActionResult> OnPostDeleteEmpresaAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/EmpresasColaboradoras/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar empresa colaboradora"); }
            return RedirectToPage(new { tab = "empresas" });
        }

        public async Task<IActionResult> OnPostCreateUsuarioEmpresaAsync(string userId, int empresaColaboradoraId, int? rolId, int? disciplinaId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToPage(new { tab = "usuariosempresa", mensajeEmpresa = "Seleccione un usuario.", mensajeEmpresaEsError = true });
            try
            {
                await _ritApi.PostAsync<object, UsuarioEmpresaColaboradoraApi>("api/UsuariosEmpresaColaboradora", new { UserId = userId.Trim(), EmpresaColaboradoraId = empresaColaboradoraId, RolId = rolId, DisciplinaId = disciplinaId });
                return RedirectToPage(new { tab = "usuariosempresa", mensajeEmpresa = "Asignación guardada correctamente.", mensajeEmpresaEsError = false });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error asignar usuario a empresa colaboradora");
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " " + ex.InnerException.Message;
                return RedirectToPage(new { tab = "usuariosempresa", mensajeEmpresa = "No se pudo asignar: " + msg, mensajeEmpresaEsError = true });
            }
        }

        public async Task<IActionResult> OnPostDeleteUsuarioEmpresaAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/UsuariosEmpresaColaboradora/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error quitar usuario de empresa colaboradora"); }
            return RedirectToPage(new { tab = "usuariosempresa" });
        }

        public async Task<IActionResult> OnPostCreateAppAsync(string nombre, string? descripcionApp, string? imagenApp)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "apps" });
            try { await _ritApi.PostAsync<object, AppApi>("api/Apps", new { Nombre = nombre.Trim(), DescripcionApp = descripcionApp?.Trim(), ImagenApp = imagenApp?.Trim() }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear app"); }
            return RedirectToPage(new { tab = "apps" });
        }

        public async Task<IActionResult> OnPostUpdateAppAsync(int id, string nombre, string? descripcionApp, string? imagenApp)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "apps" });
            try { await _ritApi.PutAsync<object, AppApi>($"api/Apps/{id}", new { Nombre = nombre.Trim(), DescripcionApp = descripcionApp?.Trim(), ImagenApp = imagenApp?.Trim() }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar app"); }
            return RedirectToPage(new { tab = "apps" });
        }

        public async Task<IActionResult> OnPostDeleteAppAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/Apps/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar app"); }
            return RedirectToPage(new { tab = "apps" });
        }

        public async Task<IActionResult> OnPostCreateUsuarioAppAsync(string userId, int appId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "Seleccione un usuario.", mensajeAppEsError = true });
            try
            {
                await _ritApi.PostAsync<object, UsuarioAppApi>("api/UsuariosApp", new { UserId = userId.Trim(), AppId = appId });
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "App asignada correctamente.", mensajeAppEsError = false });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error asignar app a usuario");
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " " + ex.InnerException.Message;
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "No se pudo asignar: " + msg, mensajeAppEsError = true });
            }
        }

        /// <summary>Asigna todas las apps del catálogo al usuario seleccionado (omite las ya asignadas).</summary>
        public async Task<IActionResult> OnPostAsignarTodasAppsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "Seleccione un usuario.", mensajeAppEsError = true });
            var apps = await _ritApi.GetListAsync<AppApi>("api/Apps");
            if (apps.Count == 0)
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "No hay aplicaciones en el catálogo.", mensajeAppEsError = true });
            var yaAsignadas = (await _ritApi.GetListAsync<UsuarioAppApi>("api/UsuariosApp"))
                .Where(ua => ua.UserId == userId.Trim())
                .Select(ua => ua.AppId)
                .ToHashSet();
            var asignadas = 0;
            var omitidas = 0;
            foreach (var app in apps)
            {
                if (yaAsignadas.Contains(app.Id)) { omitidas++; continue; }
                try
                {
                    await _ritApi.PostAsync<object, UsuarioAppApi>("api/UsuariosApp", new { UserId = userId.Trim(), AppId = app.Id });
                    asignadas++;
                }
                catch { omitidas++; }
            }
            var msg = asignadas > 0
                ? $"Se asignaron {asignadas} app(s)." + (omitidas > 0 ? $" {omitidas} ya estaban asignadas u omitidas." : "")
                : (omitidas > 0 ? "El usuario ya tenía todas las apps asignadas." : "No se pudo asignar ninguna app.");
            return RedirectToPage(new { tab = "usuariosapp", mensajeApp = msg, mensajeAppEsError = asignadas == 0 && omitidas < apps.Count });
        }

        /// <summary>Asigna varias apps a un usuario en una sola acción.</summary>
        public async Task<IActionResult> OnPostCreateUsuarioAppVariasAsync(string userId, List<int> appIds)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "Seleccione un usuario.", mensajeAppEsError = true });
            if (appIds == null || !appIds.Any())
                return RedirectToPage(new { tab = "usuariosapp", mensajeApp = "Seleccione al menos una app.", mensajeAppEsError = true });
            var asignadas = 0;
            foreach (var appId in appIds.Distinct())
            {
                try
                {
                    await _ritApi.PostAsync<object, UsuarioAppApi>("api/UsuariosApp", new { UserId = userId.Trim(), AppId = appId });
                    asignadas++;
                }
                catch { /* duplicado u otro error, omitir */ }
            }
            var msg = asignadas > 0 ? $"Se asignaron {asignadas} app(s) correctamente." : "No se pudo asignar ninguna (posiblemente ya estaban asignadas).";
            return RedirectToPage(new { tab = "usuariosapp", mensajeApp = msg, mensajeAppEsError = asignadas == 0 });
        }

        public async Task<IActionResult> OnPostDeleteUsuarioAppAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/UsuariosApp/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error quitar app de usuario"); }
            return RedirectToPage(new { tab = "usuariosapp" });
        }

        public async Task<IActionResult> OnPostCreateDisciplinaAsync(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "disciplinas" });
            try { await _ritApi.PostAsync<object, DisciplinaApi>("api/Disciplinas", new { Nombre = nombre.Trim() }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error crear disciplina"); }
            return RedirectToPage(new { tab = "disciplinas" });
        }

        public async Task<IActionResult> OnPostUpdateDisciplinaAsync(int id, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return RedirectToPage(new { tab = "disciplinas" });
            try { await _ritApi.PutAsync<object, DisciplinaApi>($"api/Disciplinas/{id}", new { Nombre = nombre.Trim() }); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error actualizar disciplina"); }
            return RedirectToPage(new { tab = "disciplinas" });
        }

        public async Task<IActionResult> OnPostDeleteDisciplinaAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/Disciplinas/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error eliminar disciplina"); }
            return RedirectToPage(new { tab = "disciplinas" });
        }

        public async Task<IActionResult> OnPostCreateUsuarioDisciplinaAsync(string userId, int disciplinaId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToPage(new { tab = "usuariosdisciplina", mensajeDisciplina = "Seleccione un usuario.", mensajeDisciplinaEsError = true });
            try
            {
                await _ritApi.PostAsync<object, UsuarioDisciplinaApi>("api/UsuariosDisciplina", new { UserId = userId.Trim(), DisciplinaId = disciplinaId });
                return RedirectToPage(new { tab = "usuariosdisciplina", mensajeDisciplina = "Asignación guardada correctamente.", mensajeDisciplinaEsError = false });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error asignar usuario a disciplina");
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " " + ex.InnerException.Message;
                return RedirectToPage(new { tab = "usuariosdisciplina", mensajeDisciplina = "No se pudo asignar: " + msg + ". Compruebe que Rit_Api tenga el endpoint POST api/UsuariosDisciplina.", mensajeDisciplinaEsError = true });
            }
        }

        public async Task<IActionResult> OnPostDeleteUsuarioDisciplinaAsync(int id)
        {
            try { await _ritApi.DeleteAsync($"api/UsuariosDisciplina/{id}"); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error quitar usuario de disciplina"); }
            return RedirectToPage(new { tab = "usuariosdisciplina" });
        }

        /// <summary>Crear nuevo usuario (desde Super Admin) con email, contraseña y rol inicial.</summary>
        public async Task<IActionResult> OnPostCreateUsuarioAsync(string email, string password, string confirmPassword, string rolInicial)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "El email es obligatorio.", mensajeUsuariosEsError = true });
            email = email.Trim();
            if (password != confirmPassword)
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "La contraseña y la confirmación no coinciden.", mensajeUsuariosEsError = true });
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "La contraseña debe tener al menos 6 caracteres.", mensajeUsuariosEsError = true });
            if (string.IsNullOrWhiteSpace(rolInicial))
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "Debe seleccionar un rol.", mensajeUsuariosEsError = true });

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "Ya existe un usuario con ese email.", mensajeUsuariosEsError = true });

            var user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = errors, mensajeUsuariosEsError = true });
            }

            await _userManager.AddToRoleAsync(user, rolInicial);
            _logger.LogInformation("Usuario {Email} creado por admin con rol {Rol}", email, rolInicial);

            // Enviar correo de confirmación (mismo flujo que registro público)
            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { area = "Identity", userId, code }, protocol: Request.Scheme);
            var htmlConfirmacion = EmailTemplateHelper.BuildConfirmacionCorreoHtml(callbackUrl);
            await _emailSender.SendEmailAsync(email, "Confirmar su correo - RazorIdentity", htmlConfirmacion);

            return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "Usuario creado. Se ha enviado el correo de confirmación.", mensajeUsuariosEsError = false });
        }

        /// <summary>Cambiar el rol de un usuario (reemplaza todos sus roles por el nuevo).</summary>
        public async Task<IActionResult> OnPostChangeRolAsync(string userId, string nuevoRol)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(nuevoRol))
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "Faltan datos.", mensajeUsuariosEsError = true });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = "Usuario no encontrado.", mensajeUsuariosEsError = true });

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            var addResult = await _userManager.AddToRoleAsync(user, nuevoRol);
            if (!addResult.Succeeded)
            {
                await _userManager.AddToRolesAsync(user, currentRoles);
                var errors = string.Join(" ", addResult.Errors.Select(e => e.Description));
                return RedirectToPage(new { tab = "usuarios", mensajeUsuarios = errors, mensajeUsuariosEsError = true });
            }

            _logger.LogInformation("Rol de usuario {UserId} cambiado a {Rol}", userId, nuevoRol);
            var busqueda = Request.Form["usuarioBusqueda"].FirstOrDefault();
            return RedirectToPage(new { tab = "usuarios", usuarioBusqueda = busqueda, mensajeUsuarios = "Rol actualizado correctamente.", mensajeUsuariosEsError = false });
        }
    }
}
