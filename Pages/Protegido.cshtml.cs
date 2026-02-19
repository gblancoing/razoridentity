using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger<ProtegidoModel> _logger;

        public ProtegidoModel(IRitApiClient ritApi, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IEmailSender emailSender, ILogger<ProtegidoModel> logger)
        {
            _ritApi = ritApi;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
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
        public List<IdentityUser> Usuarios { get; set; } = new();

        /// <summary>Roles del sistema para dropdowns.</summary>
        public List<string> Roles { get; set; } = new();

        /// <summary>Usuarios con sus roles actuales (para la pestaña Gestión de usuarios).</summary>
        public List<(IdentityUser User, IList<string> Roles)> UsuariosConRoles { get; set; } = new();

        public int? EditPaisId { get; set; }
        public int? EditRegionId { get; set; }
        public int? EditProyectoId { get; set; }
        public int? EditCentroId { get; set; }
        public int? EditDisciplinaId { get; set; }
        public int? EditUsuarioCentroId { get; set; }

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
        }

        public async Task OnGetAsync(string? tab, string? usuarioBusqueda, int? editPaisId, int? editRegionId, int? editProyectoId, int? editCentroId, int? editDisciplinaId, int? editUsuarioCentroId, string? mensajeUsuarios, bool mensajeUsuariosEsError = false, string? mensajeDisciplina = null, bool mensajeDisciplinaEsError = false)
        {
            Tab = tab ?? "usuarios";
            UsuarioBusqueda = usuarioBusqueda;
            MensajeUsuarios = mensajeUsuarios;
            MensajeUsuariosEsError = mensajeUsuariosEsError;
            MensajeDisciplina = mensajeDisciplina;
            MensajeDisciplinaEsError = mensajeDisciplinaEsError;
            EditPaisId = editPaisId;
            EditRegionId = editRegionId;
            EditProyectoId = editProyectoId;
            EditCentroId = editCentroId;
            EditDisciplinaId = editDisciplinaId;
            EditUsuarioCentroId = editUsuarioCentroId;
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
