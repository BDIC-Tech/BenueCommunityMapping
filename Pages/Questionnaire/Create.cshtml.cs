using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Questionnaire
{
    public class CreateModel : PageModel
    {
        public IActionResult OnGet() => RedirectToPage("Edit");
    }
}
