using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Admin.Users
{
    public class CreateModel : PageModel
    {
        private readonly IUserService                 _users;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly AppDbContext                 _db;

        public CreateModel(IUserService users, UserManager<ApplicationUser> userMgr, AppDbContext db)
        {
            _users   = users;
            _userMgr = userMgr;
            _db      = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<ApplicationUser>      Coordinators { get; private set; } = [];
        public List<LocalGovernmentArea>    LGAs         { get; private set; } = [];

        public class InputModel
        {
            [Required] public string FirstName { get; set; } = string.Empty;
            [Required] public string LastName  { get; set; } = string.Empty;
            [Required, EmailAddress] public string Email { get; set; } = string.Empty;
            [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
            [Required] public string Role { get; set; } = AppRoles.Agent;
            public string? LGA   { get; set; }
            public string? Ward  { get; set; }
            public string? CoordinatorId { get; set; }
        }

        public async Task OnGetAsync(string? role)
        {
            Input.Role   = role ?? AppRoles.Agent;
            Coordinators = (await _users.GetCoordinatorsAsync()).ToList();
            LGAs         = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Coordinators = (await _users.GetCoordinatorsAsync()).ToList();
            LGAs         = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();

            // Coordinators are LGA-level only — clear ward & coordinator assignment
            if (Input.Role == AppRoles.Coordinator)
            {
                Input.Ward          = null;
                Input.CoordinatorId = null;
            }

            if (!ModelState.IsValid) return Page();

            var (ok, errors) = await _users.CreateAsync(new CreateUserRequest
            {
                FirstName     = Input.FirstName,
                LastName      = Input.LastName,
                Email         = Input.Email,
                Password      = Input.Password,
                Role          = Input.Role,
                LGA           = Input.LGA,
                Ward          = Input.Ward,
                CoordinatorId = Input.CoordinatorId
            });

            if (!ok)
            {
                foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
                return Page();
            }

            TempData["Success"] = $"{Input.Role} '{Input.FirstName} {Input.LastName}' created successfully.";
            return RedirectToPage(Input.Role == AppRoles.Coordinator ? "Coordinators" : "Agents");
        }
    }
}
