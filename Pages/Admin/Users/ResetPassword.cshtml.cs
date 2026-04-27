using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Admin.Users
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IUserService _users;
        public ResetPasswordModel(IUserService users) => _users = users;

        [BindProperty] public InputModel Input { get; set; } = new();
        public string TargetName { get; private set; } = string.Empty;

        public class InputModel
        {
            public string UserId { get; set; } = string.Empty;
            [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
            [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
            public string Confirm { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user is null) return NotFound();
            TargetName   = user.FullName;
            Input.UserId = id;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var (ok, errors) = await _users.ResetPasswordAsync(Input.UserId, Input.NewPassword);
            if (!ok)
            {
                foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
                return Page();
            }

            TempData["Success"] = "Password reset successfully.";
            return RedirectToPage("Agents");
        }
    }
}
