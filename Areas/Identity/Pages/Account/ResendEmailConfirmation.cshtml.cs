#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace RazorIdentity.Areas.Identity.Pages.Account
{
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ResendEmailConfirmationModel> _logger;

        public ResendEmailConfirmationModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ILogger<ResendEmailConfirmationModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
            [Display(Name = "Correo electrónico")]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // No revelar si existe o no
                return RedirectToPage("./ResendEmailConfirmation");
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("./ResendEmailConfirmation");
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl = Url.Content("~/") },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Confirmar su correo - RazorIdentity",
                $"Confirme su cuenta <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>haciendo clic aquí</a>.");

            _logger.LogInformation("Reenvío de confirmación enviado a {Email}", Input.Email);
            return RedirectToPage("./RegisterConfirmation", new { email = Input.Email });
        }
    }
}
