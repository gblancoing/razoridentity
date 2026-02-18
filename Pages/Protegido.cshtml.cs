using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorIdentity.Pages
{
    public class ProtegidoModel : PageModel
    {
        [Authorize(Roles = "Admin")]
        public void OnGet()
        {
        }
    }
}
