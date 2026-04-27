using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly UserManager<ApplicationUser>  _userMgr;
        private readonly BenueCommunityMapping.Services.IUserService _userService;

        public LoginModel(
            SignInManager<ApplicationUser> signIn, 
            UserManager<ApplicationUser> userMgr,
            BenueCommunityMapping.Services.IUserService userService)
        {
            _signIn  = signIn;
            _userMgr = userMgr;
            _userService = userService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public IActionResult OnGet()
        {
            if (_signIn.IsSignedIn(User)) return RedirectToPage("/Index");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userMgr.FindByEmailAsync(Input.Email);
            if (user is null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials or account is inactive.");
                return Page();
            }

            var result = await _signIn.PasswordSignInAsync(
                user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userMgr.UpdateAsync(user);
                return RedirectToPage("/Index");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked due to multiple failed attempts. Try again in 15 minutes.");
                return Page();
            }

            // Email not yet verified — Identity returns IsNotAllowed when
            // options.SignIn.RequireConfirmedEmail = true and the user hasn't
            // clicked the verification link yet.
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty,
                    "Your email address has not been verified. " +
                    "Please check your inbox for the verification link sent when your account was created.");
                
                // Add unverified flag so UI can show the Resend button
                ViewData["UnverifiedEmail"] = Input.Email;
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        public async Task<IActionResult> OnPostResendVerificationAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Email is required to resend verification.");
                return Page();
            }

            var (ok, error) = await _userService.ResendVerificationEmailAsync(email);
            
            if (ok)
            {
                TempData["Success"] = "Verification email has been resent. Please check your inbox (and spam folder).";
                // Redirecting to GET so we clear the unverified state and just show success
                return RedirectToPage();
            }
            else
            {
                ModelState.AddModelError(string.Empty, error);
                // Keep showing the resend option
                ViewData["UnverifiedEmail"] = email;
                Input.Email = email;
                return Page();
            }
        }
    }
}
