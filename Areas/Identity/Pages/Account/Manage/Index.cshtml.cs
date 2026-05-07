// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;

namespace RazorIdentity.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IRitApiClient _ritApi;

        public IndexModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext dbContext,
            IRitApiClient ritApi)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
            _ritApi = ritApi;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        /// <summary>Email del usuario (para mostrar en la ficha).</summary>
        public string Email { get; set; }

        /// <summary>Nombre completo del perfil (Datos personales).</summary>
        public string FullName { get; set; }

        /// <summary>Cargo del perfil (Datos personales o RIT_API Ficha).</summary>
        public string Cargo { get; set; }

        /// <summary>Turno desde RIT_API Ficha (si está disponible).</summary>
        public string? Turno { get; set; }

        /// <summary>Indica si el correo está confirmado (para la ficha).</summary>
        public bool EmailConfirmed { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
        }

        private async Task LoadAsync(IdentityUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;
            Email = user?.Email ?? userName;
            EmailConfirmed = user?.EmailConfirmed ?? false;

            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            FullName = profile?.FullName?.Trim();
            Cargo = profile?.Cargo?.Trim();
            Turno = null;

            // Preferir datos de RIT_API Ficha cuando estén disponibles
            try
            {
                var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{user.Id}/Ficha");
                if (ficha != null)
                {
                    if (!string.IsNullOrWhiteSpace(ficha.NombreCompleto)) FullName = ficha.NombreCompleto.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.NCargo)) Cargo = ficha.NCargo.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.Turno)) Turno = ficha.Turno.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.Email)) Email = ficha.Email.Trim();
                }
            }
            catch
            {
                // Si RIT_API no está disponible, se mantienen FullName, Cargo, Email de perfil/Identity
            }

            Input = new InputModel
            {
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Error inesperado al guardar el teléfono.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Su perfil ha sido actualizado.";
            return RedirectToPage();
        }
    }
}
