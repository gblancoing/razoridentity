using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;

namespace RazorIdentity.Pages
{
    [Authorize]
    public class RitWebProyectosModel : PageModel
    {
        private readonly IRitApiClient _ritApi;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RitWebProyectosModel> _logger;

        public RitWebProyectosModel(
            IRitApiClient ritApi,
            UserManager<IdentityUser> userManager,
            ILogger<RitWebProyectosModel> logger)
        {
            _ritApi = ritApi;
            _userManager = userManager;
            _logger = logger;
        }

        public List<ProyectoApi> Proyectos { get; set; } = new();
        public string? ErrorApi { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.IsInRole("Super_admin") && !User.IsInRole("Super_Admin"))
                return RedirectToPage("/App");

            try
            {
                Proyectos = await _ritApi.GetListAsync<ProyectoApi>("api/Proyectos") ?? new List<ProyectoApi>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar proyectos para RitWeb");
                ErrorApi = "No se pudo cargar el listado de proyectos. Compruebe que RIT API esté en ejecución.";
            }

            return Page();
        }
    }
}
