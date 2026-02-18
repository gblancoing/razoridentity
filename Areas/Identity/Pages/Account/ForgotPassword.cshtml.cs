// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
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
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ILogger<ForgotPasswordModel> logger)
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
            if (ModelState.IsValid)
            {
                // En este proyecto el UserName es el email (ver Register.cshtml.cs)
                var user = await _userManager.FindByNameAsync(Input.Email);
                if (user == null)
                {
                    // No revelar si el usuario existe o no (evitar enumeración de cuentas)
                    _logger.LogWarning("Solicitud de recuperación de contraseña para un correo no registrado: {Email}", Input.Email);
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code, userId = user.Id },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Recuperar contraseña - RazorIdentity",
                    $"Para restablecer su contraseña <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>haga clic aquí</a>. " +
                    "El enlace es válido por tiempo limitado.");

                _logger.LogInformation("Enlace de recuperación de contraseña enviado a {Email}", Input.Email);
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
