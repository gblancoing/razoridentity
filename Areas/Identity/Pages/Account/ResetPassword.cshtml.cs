// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace RazorIdentity.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            UserManager<IdentityUser> userManager,
            ILogger<ResetPasswordModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [StringLength(100, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Nueva contraseña")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare("Password", ErrorMessage = "La contraseña y la confirmación no coinciden.")]
            public string ConfirmPassword { get; set; }
        }

        public string Code { get; set; }
        public string UserId { get; set; }

        public IActionResult OnGet(string code = null, string userId = null)
        {
            if (code == null || userId == null)
            {
                _logger.LogWarning("ResetPassword llamado sin code o userId");
                return RedirectToPage("./Login");
            }

            Code = code;
            UserId = userId;
            Input = new InputModel();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string code, string userId)
        {
            if (code == null || userId == null)
            {
                _logger.LogWarning("ResetPassword POST sin code o userId");
                return RedirectToPage("./Login");
            }

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("ResetPassword: usuario no encontrado {UserId}", userId);
                return RedirectToPage("./ResetPasswordConfirmation", new { success = false });
            }

            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ResetPasswordAsync(user, decodedCode, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Contraseña restablecida correctamente para usuario {UserId}", userId);
                return RedirectToPage("./ResetPasswordConfirmation", new { success = true });
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            Code = code;
            UserId = userId;
            return Page();
        }
    }
}
