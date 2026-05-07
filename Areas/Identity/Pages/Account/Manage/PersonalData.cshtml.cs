// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models;
using RazorIdentity.Models.Api;
using RazorIdentity.Services;

namespace RazorIdentity.Areas.Identity.Pages.Account.Manage
{
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IRitApiClient _ritApi;

        public PersonalDataModel(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext dbContext,
            IRitApiClient ritApi)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _ritApi = ritApi;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>Catálogo de turnos desde RIT_API (GET api/Turnos) para el desplegable.</summary>
        public List<TurnoApi> TurnosDisponibles { get; set; } = new();

        /// <summary>Todos los campos son opcionales; se puede guardar el formulario sin completar Turno, Dirección, Código postal, etc.</summary>
        public class InputModel
        {
            [MaxLength(200)]
            [Display(Name = "Nombre completo")]
            public string FullName { get; set; } = "";

            [MaxLength(200)]
            [Display(Name = "Cargo")]
            public string Cargo { get; set; } = "";

            [Display(Name = "Turno")]
            public string Turno { get; set; } = "";

            [MaxLength(500)]
            [Display(Name = "Dirección")]
            public string Address { get; set; } = "";

            [MaxLength(150)]
            [Display(Name = "Ciudad")]
            public string City { get; set; } = "";

            [MaxLength(150)]
            [Display(Name = "Región o estado")]
            public string RegionOrState { get; set; } = "";

            [MaxLength(100)]
            [Display(Name = "Código postal")]
            public string PostalCode { get; set; } = "";

            [MaxLength(150)]
            [Display(Name = "País")]
            public string Country { get; set; } = "";

            [MaxLength(500)]
            [Url(ErrorMessage = "Debe ser una URL válida.")]
            [Display(Name = "Perfil de LinkedIn")]
            public string? LinkedInUrl { get; set; }  // null o vacío = opcional; [Url] solo valida cuando hay valor

            [MaxLength(500)]
            [Url(ErrorMessage = "Debe ser una URL válida.")]
            [Display(Name = "Sitio web personal")]
            public string? WebsiteUrl { get; set; }   // null o vacío = opcional

            [MaxLength(500)]
            [Display(Name = "Otros enlaces o redes")]
            public string? OtherLinks { get; set; }

            [MaxLength(1000)]
            [Display(Name = "Notas o biografía breve")]
            public string? BioOrNotes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"No se pudo cargar el usuario con ID '{_userManager.GetUserId(User)}'.");

            await LoadProfileAsync(user);
            return Page();
        }

        private async Task LoadProfileAsync(IdentityUser user)
        {
            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            // Prellenar desde RIT_API (Ficha) cuando exista; si no, usar UserProfiles
            string? fullName = profile?.FullName;
            string? cargo = profile?.Cargo;
            string? turno = null;
            try
            {
                var ficha = await _ritApi.GetAsync<UsuarioFichaApi>($"api/Usuarios/{user.Id}/Ficha");
                if (ficha != null)
                {
                    if (!string.IsNullOrWhiteSpace(ficha.NombreCompleto)) fullName = ficha.NombreCompleto.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.NCargo)) cargo = ficha.NCargo.Trim();
                    if (!string.IsNullOrWhiteSpace(ficha.Turno)) turno = ficha.Turno.Trim();
                }
            }
            catch
            {
                // Si RIT_API no está disponible, se usan solo los datos locales
            }

            // Catálogo de turnos (5x2, 7x7, 14x14) para el desplegable
            try
            {
                TurnosDisponibles = await _ritApi.GetListAsync<TurnoApi>("api/Turnos");
            }
            catch
            {
                TurnosDisponibles = new List<TurnoApi>();
            }

            Input = new InputModel
            {
                FullName = fullName ?? "",
                Cargo = cargo ?? "",
                Turno = turno ?? "",
                Address = profile?.Address ?? "",
                City = profile?.City ?? "",
                RegionOrState = profile?.RegionOrState ?? "",
                PostalCode = profile?.PostalCode ?? "",
                Country = profile?.Country ?? "",
                LinkedInUrl = profile?.LinkedInUrl ?? "",
                WebsiteUrl = profile?.WebsiteUrl ?? "",
                OtherLinks = profile?.OtherLinks,
                BioOrNotes = profile?.BioOrNotes
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"No se pudo cargar el usuario con ID '{_userManager.GetUserId(User)}'.");

            // URLs opcionales: vacío → null para que [Url] no falle
            Input.LinkedInUrl = string.IsNullOrWhiteSpace(Input.LinkedInUrl) ? null : Input.LinkedInUrl.Trim();
            Input.WebsiteUrl = string.IsNullOrWhiteSpace(Input.WebsiteUrl) ? null : Input.WebsiteUrl.Trim();
            if (Input.LinkedInUrl == null) ModelState.Remove("Input.LinkedInUrl");
            if (Input.WebsiteUrl == null) ModelState.Remove("Input.WebsiteUrl");

            if (!ModelState.IsValid)
            {
                await LoadProfileAsync(user);
                return Page();
            }

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile { UserId = user.Id };
                _dbContext.UserProfiles.Add(profile);
            }

            profile.FullName = Input.FullName;
            profile.Cargo = Input.Cargo;
            profile.Address = Input.Address;
            profile.City = Input.City;
            profile.RegionOrState = Input.RegionOrState;
            profile.PostalCode = Input.PostalCode;
            profile.Country = Input.Country;
            profile.LinkedInUrl = Input.LinkedInUrl;
            profile.WebsiteUrl = Input.WebsiteUrl;
            profile.OtherLinks = string.IsNullOrWhiteSpace(Input.OtherLinks) ? null : Input.OtherLinks.Trim();
            profile.BioOrNotes = string.IsNullOrWhiteSpace(Input.BioOrNotes) ? null : Input.BioOrNotes.Trim();

            await _dbContext.SaveChangesAsync();

            // Sincronizar con RIT_API (PUT Ficha) para mantener la API como fuente maestra
            try
            {
                await _ritApi.PutAsync<UsuarioFichaApiUpdateDto, UsuarioFichaApi>(
                    $"api/Usuarios/{user.Id}/Ficha",
                    new UsuarioFichaApiUpdateDto
                    {
                        Turno = string.IsNullOrWhiteSpace(Input.Turno) ? null : Input.Turno.Trim(),
                        NCargo = string.IsNullOrWhiteSpace(Input.Cargo) ? null : Input.Cargo.Trim(),
                        NombreCompleto = string.IsNullOrWhiteSpace(Input.FullName) ? null : Input.FullName.Trim(),
                        Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim()
                    });
            }
            catch
            {
                // Si RIT_API falla, los datos ya quedaron guardados en UserProfiles
            }

            StatusMessage = "Sus datos personales se han guardado correctamente.";
            return RedirectToPage();
        }
    }
}
