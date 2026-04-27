using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Account
{
    /// <summary>
    /// Public page that handles the email-verification link sent to
    /// newly created Coordinator and Agent accounts.
    /// </summary>
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userMgr;

        public ConfirmEmailModel(UserManager<ApplicationUser> userMgr)
            => _userMgr = userMgr;

        public bool   IsSuccess   { get; private set; }
        public string ErrorDetail { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string? userId, string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                IsSuccess   = false;
                ErrorDetail = "Missing verification parameters.";
                return Page();
            }

            var user = await _userMgr.FindByIdAsync(userId);
            if (user is null)
            {
                IsSuccess   = false;
                ErrorDetail = "Account not found.";
                return Page();
            }

            if (user.EmailConfirmed)
            {
                // Already confirmed — treat as success so clicking twice is harmless
                IsSuccess = true;
                return Page();
            }

            // Identity tokens come URL-encoded from the email link;
            // ASP.NET routing automatically decodes the query string —
            // but if the token contains '+' signs these can be misread as spaces.
            // Ensure we pass the raw value that was placed in the email.
            var result = await _userMgr.ConfirmEmailAsync(user, token);

            IsSuccess   = result.Succeeded;
            ErrorDetail = result.Succeeded
                ? string.Empty
                : string.Join(" ", result.Errors.Select(e => e.Description));

            return Page();
        }
    }
}
