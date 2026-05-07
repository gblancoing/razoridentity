using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;

namespace RazorIdentity.Pages
{
    [Authorize]
    public class AppModel : PageModel
    {
        private readonly IRitApiClient _ritApi;
        private readonly ILogger<AppModel> _logger;

        public AppModel(IRitApiClient ritApi, ILogger<AppModel> logger)
        {
            _ritApi = ritApi;
            _logger = logger;
        }

        public List<AppApi> Apps { get; set; } = new();
        public string? ErrorApi { get; set; }
        /// <summary>True si el usuario tiene rol Super_admin: puede ver RitWeb separado por proyectos.</summary>
        public bool IsSuperAdmin { get; set; }

        public async Task OnGetAsync()
        {
            IsSuperAdmin = User.IsInRole("Super_admin") || User.IsInRole("Super_Admin");
            try
            {
                Apps = await _ritApi.GetListAsync<AppApi>("api/Apps");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar apps desde Rit_Api");
                ErrorApi = "No se pudo cargar el listado de aplicaciones. Compruebe que Rit_Api esté en ejecución.";
            }
        }
    }
}
