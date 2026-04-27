using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Coordinator
{
    public class CreateAgentModel : PageModel
    {
        private readonly IUserService                 _users;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly AppDbContext                 _db;

        public CreateAgentModel(IUserService users, UserManager<ApplicationUser> userMgr, AppDbContext db)
        {
            _users   = users;
            _userMgr = userMgr;
            _db      = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<LocalGovernmentArea> LGAs { get; private set; } = [];

        public class InputModel
        {
            [Required] public string FirstName { get; set; } = string.Empty;
            [Required] public string LastName  { get; set; } = string.Empty;
            [Required, EmailAddress] public string Email { get; set; } = string.Empty;
            [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
            public string? LGA  { get; set; }
            public string? Ward { get; set; }
        }

        public async Task OnGetAsync()
        {
            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();

            if (!ModelState.IsValid) return Page();

            var coordinator = (await _userMgr.GetUserAsync(User))!;

            var (ok, errors) = await _users.CreateAsync(new CreateUserRequest
            {
                FirstName     = Input.FirstName,
                LastName      = Input.LastName,
                Email         = Input.Email,
                Password      = Input.Password,
                Role          = AppRoles.Agent,
                LGA           = Input.LGA,
                Ward          = Input.Ward,
                CoordinatorId = coordinator.Id
            });

            if (!ok)
            {
                foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
                return Page();
            }

            TempData["Success"] = $"Agent '{Input.FirstName} {Input.LastName}' created successfully.";
            return RedirectToPage("Agents");
        }
    }
}
