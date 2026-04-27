using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Account
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser>  _userMgr;
        private readonly SignInManager<ApplicationUser> _signIn;

        public ChangePasswordModel(
            UserManager<ApplicationUser>  userMgr,
            SignInManager<ApplicationUser> signIn)
        {
            _userMgr = userMgr;
            _signIn  = signIn;
        }

        [BindProperty] public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, DataType(DataType.Password)]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required, MinLength(8), DataType(DataType.Password)]
            public string NewPassword { get; set; } = string.Empty;

            [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return Challenge();

            var result = await _userMgr.ChangePasswordAsync(
                user, Input.CurrentPassword, Input.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            await _signIn.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed successfully.";
            return RedirectToPage("/Index");
        }
    }
}
