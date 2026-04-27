using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        public ProfileModel(UserManager<ApplicationUser> userMgr) => _userMgr = userMgr;

        [BindProperty] public InputModel Input { get; set; } = new();
        public string Email { get; private set; } = string.Empty;

        public class InputModel
        {
            [Required] public string FirstName   { get; set; } = string.Empty;
            [Required] public string LastName    { get; set; } = string.Empty;
            [Phone]    public string? PhoneNumber { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return Challenge();

            Email = user.Email ?? string.Empty;
            Input = new InputModel
            {
                FirstName   = user.FirstName,
                LastName    = user.LastName,
                PhoneNumber = user.PhoneNumber
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return Challenge();

            user.FirstName   = Input.FirstName;
            user.LastName    = Input.LastName;
            user.PhoneNumber = Input.PhoneNumber;

            await _userMgr.UpdateAsync(user);
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToPage();
        }
    }
}
