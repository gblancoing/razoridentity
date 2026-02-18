#nullable disable

using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace RazorIdentity.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(UserManager<IdentityUser> userManager, ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public string StatusMessage { get; set; }
        public string ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code, string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (userId == null || code == null)
            {
                StatusMessage = "Error: enlace de confirmación no válido.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "Error: usuario no encontrado.";
                return Page();
            }

            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("Correo confirmado para el usuario {UserId}", userId);
                StatusMessage = "Gracias por confirmar su correo. Ya puede iniciar sesión.";
                return Page();
            }

            StatusMessage = "Error al confirmar el correo. El enlace puede haber expirado.";
            return Page();
        }
    }
}
