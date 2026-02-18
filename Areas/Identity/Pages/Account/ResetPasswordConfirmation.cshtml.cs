#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorIdentity.Areas.Identity.Pages.Account
{
    public class ResetPasswordConfirmationModel : PageModel
    {
        public bool Success { get; set; }

        public IActionResult OnGet(bool? success = true)
        {
            Success = success == true;
            return Page();
        }
    }
}
