using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorIdentity.Pages;

/// <summary>Compatibilidad: enlaces antiguos a /PyC redirigen a PMO.</summary>
[Authorize]
public class PyCRedirectModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/PMO");
}
