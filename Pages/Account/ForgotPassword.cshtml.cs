using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Email_Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly IEmailTemplateService        _emailTpl;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userMgr,
            IEmailTemplateService        emailTpl,
            ILogger<ForgotPasswordModel> logger)
        {
            _userMgr  = userMgr;
            _emailTpl = emailTpl;
            _logger   = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        /// <summary>True once the email has been dispatched — shows success card.</summary>
        public bool   EmailSent   { get; private set; }
        public string SentToEmail { get; private set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "Email address is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            public string Email { get; set; } = string.Empty;
        }

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userMgr.FindByEmailAsync(Input.Email);

            // Always show success to prevent user-enumeration attacks.
            // We only send the email if the account actually exists and is confirmed.
            if (user is not null && user.IsActive)
            {
                try
                {
                    var token     = await _userMgr.GeneratePasswordResetTokenAsync(user);
                    var resetLink = BuildResetLink(user.Id, token);

                    await _emailTpl.SendPasswordResetAsync(
                        toEmail:   user.Email!,
                        fullName:  user.FullName,
                        resetLink: resetLink);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send password-reset email to {Email}", Input.Email);
                    // Still show success page — do not leak internal errors
                }
            }

            EmailSent   = true;
            SentToEmail = Input.Email;
            return Page();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private string BuildResetLink(string userId, string token)
        {
            var req     = HttpContext.Request;
            var baseUrl = $"{req.Scheme}://{req.Host}";
            return $"{baseUrl}/Account/ResetPassword" +
                   $"?userId={Uri.EscapeDataString(userId)}" +
                   $"&token={Uri.EscapeDataString(token)}";
        }
    }
}
