// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models;

namespace RazorIdentity.Areas.Identity.Pages.Account.Manage
{
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public PersonalDataModel(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [MaxLength(200)]
            [Display(Name = "Nombre completo")]
            public string FullName { get; set; }

            [MaxLength(500)]
            [Display(Name = "Dirección")]
            public string Address { get; set; }

            [MaxLength(150)]
            [Display(Name = "Ciudad")]
            public string City { get; set; }

            [MaxLength(150)]
            [Display(Name = "Región o estado")]
            public string RegionOrState { get; set; }

            [MaxLength(100)]
            [Display(Name = "Código postal")]
            public string PostalCode { get; set; }

            [MaxLength(150)]
            [Display(Name = "País")]
            public string Country { get; set; }

            [MaxLength(500)]
            [Url(ErrorMessage = "Debe ser una URL válida.")]
            [Display(Name = "Perfil de LinkedIn")]
            public string LinkedInUrl { get; set; }

            [MaxLength(500)]
            [Url(ErrorMessage = "Debe ser una URL válida.")]
            [Display(Name = "Sitio web personal")]
            public string WebsiteUrl { get; set; }

            [MaxLength(500)]
            [Display(Name = "Otros enlaces o redes")]
            public string OtherLinks { get; set; }

            [MaxLength(1000)]
            [Display(Name = "Notas o biografía breve")]
            public string BioOrNotes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"No se pudo cargar el usuario con ID '{_userManager.GetUserId(User)}'.");

            await LoadProfileAsync(user.Id);
            return Page();
        }

        private async Task LoadProfileAsync(string userId)
        {
            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            Input = new InputModel
            {
                FullName = profile?.FullName,
                Address = profile?.Address,
                City = profile?.City,
                RegionOrState = profile?.RegionOrState,
                PostalCode = profile?.PostalCode,
                Country = profile?.Country,
                LinkedInUrl = profile?.LinkedInUrl,
                WebsiteUrl = profile?.WebsiteUrl,
                OtherLinks = profile?.OtherLinks,
                BioOrNotes = profile?.BioOrNotes
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"No se pudo cargar el usuario con ID '{_userManager.GetUserId(User)}'.");

            if (!ModelState.IsValid)
            {
                await LoadProfileAsync(user.Id);
                return Page();
            }

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile { UserId = user.Id };
                _dbContext.UserProfiles.Add(profile);
            }

            profile.FullName = Input.FullName;
            profile.Address = Input.Address;
            profile.City = Input.City;
            profile.RegionOrState = Input.RegionOrState;
            profile.PostalCode = Input.PostalCode;
            profile.Country = Input.Country;
            profile.LinkedInUrl = Input.LinkedInUrl;
            profile.WebsiteUrl = Input.WebsiteUrl;
            profile.OtherLinks = Input.OtherLinks;
            profile.BioOrNotes = Input.BioOrNotes;

            await _dbContext.SaveChangesAsync();
            StatusMessage = "Sus datos personales se han guardado correctamente.";
            return RedirectToPage();
        }
    }
}
