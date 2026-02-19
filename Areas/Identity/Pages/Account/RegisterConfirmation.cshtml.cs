#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorIdentity.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        public string Email { get; set; }
        public string ReturnUrl { get; set; }

        public IActionResult OnGet(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email))
                return RedirectToPage("/Index");

            Email = email;
            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }
    }
}
