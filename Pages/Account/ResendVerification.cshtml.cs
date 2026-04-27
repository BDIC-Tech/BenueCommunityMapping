using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;

namespace BenueCommunityMapping.Pages.Account
{
    [AllowAnonymous]
    public class ResendVerificationModel : PageModel
    {
        private readonly IUserService _userService;

        public ResendVerificationModel(IUserService userService)
        {
            _userService = userService;
        }

        [BindProperty]
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var (ok, error) = await _userService.ResendVerificationEmailAsync(Email);

            if (ok)
            {
                TempData["Success"] = "Verification email has been resent. Please check your inbox (and spam folder).";
                return RedirectToPage();
            }
            else
            {
                ModelState.AddModelError(string.Empty, error);
                return Page();
            }
        }
    }
}
