using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SaPosts.Web.Pages.Admin;

public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Posts/Index");
}
