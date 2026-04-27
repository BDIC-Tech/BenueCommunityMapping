using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userMgr;

        public ResetPasswordModel(UserManager<ApplicationUser> userMgr)
            => _userMgr = userMgr;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        /// <summary>True when the userId/token from the link are invalid or missing.</summary>
        public bool InvalidLink     { get; private set; }

        /// <summary>True once the password has been successfully reset.</summary>
        public bool ResetSucceeded  { get; private set; }

        public class InputModel
        {
            [Required]
            public string UserId { get; set; } = string.Empty;

            [Required]
            public string Token  { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please enter a new password.")]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your new password.")]
            [DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
            [Display(Name = "Confirm Password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        // ── GET — arrive from the email link ────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string? userId, string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                InvalidLink = true;
                return Page();
            }

            var user = await _userMgr.FindByIdAsync(userId);
            if (user is null)
            {
                InvalidLink = true;
                return Page();
            }

            // Pre-populate hidden fields so they survive the POST
            Input.UserId = userId;
            Input.Token  = token;
            return Page();
        }

        // ── POST — submit the new password ──────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userMgr.FindByIdAsync(Input.UserId);
            if (user is null)
            {
                InvalidLink = true;
                return Page();
            }

            // Identity tokens are URL-decoded by the model binder,
            // but '+' chars in Base64 tokens may have been decoded as spaces —
            // restore them before passing to Identity.
            var token  = Input.Token.Replace(" ", "+");
            var result = await _userMgr.ResetPasswordAsync(user, token, Input.NewPassword);

            if (result.Succeeded)
            {
                ResetSucceeded = true;
                return Page();
            }

            // Surface Identity errors (expired token, complexity failure, etc.)
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}
