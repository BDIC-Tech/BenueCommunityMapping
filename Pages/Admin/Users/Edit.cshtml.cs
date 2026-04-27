using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Pages.Admin.Users
{
    public class EditModel : PageModel
    {
        private readonly IUserService                 _users;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly AppDbContext                 _db;

        public EditModel(IUserService users, UserManager<ApplicationUser> userMgr, AppDbContext db)
        {
            _users   = users;
            _userMgr = userMgr;
            _db      = db;
        }

        [BindProperty] public InputModel Input { get; set; } = new();
        public string Email { get; private set; } = string.Empty;
        public List<ApplicationUser>   Coordinators { get; private set; } = [];
        public List<LocalGovernmentArea> LGAs       { get; private set; } = [];
        public List<Ward>                Wards      { get; private set; } = [];

        public class InputModel
        {
            public string Id { get; set; } = string.Empty;
            [Required] public string FirstName { get; set; } = string.Empty;
            [Required] public string LastName  { get; set; } = string.Empty;
            public string? LGA  { get; set; }
            public string? Ward { get; set; }
            public string? CoordinatorId { get; set; }
            public string Role { get; set; } = AppRoles.Agent;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user is null) return NotFound();

            // Detect actual role
            var roles = await _userMgr.GetRolesAsync(user);
            var role  = roles.Contains(AppRoles.Coordinator) ? AppRoles.Coordinator
                      : roles.Contains(AppRoles.Admin) ? AppRoles.Admin
                      : AppRoles.Agent;

            Email = user.Email ?? string.Empty;
            await LoadDropdownsAsync(user.LocalGovernmentArea);
            Coordinators = (await _users.GetCoordinatorsAsync()).ToList();

            Input = new InputModel
            {
                Id            = user.Id,
                FirstName     = user.FirstName,
                LastName      = user.LastName,
                LGA           = user.LocalGovernmentArea,
                Ward          = user.AssignedWard,
                CoordinatorId = user.CoordinatorId,
                Role          = role
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Coordinators = (await _users.GetCoordinatorsAsync()).ToList();
            await LoadDropdownsAsync(Input.LGA);

            // If Coordinator, clear ward (coordinators are LGA-level only)
            if (Input.Role == AppRoles.Coordinator)
                Input.Ward = null;

            if (!ModelState.IsValid) return Page();

            var (ok, errors) = await _users.UpdateAsync(Input.Id, new CreateUserRequest
            {
                FirstName     = Input.FirstName,
                LastName      = Input.LastName,
                Role          = Input.Role,
                LGA           = Input.LGA,
                Ward          = Input.Ward,
                CoordinatorId = Input.CoordinatorId,
                Email         = string.Empty,  // not updated on edit
                Password      = string.Empty
            });

            if (!ok)
            {
                foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
                return Page();
            }

            TempData["Success"] = "User updated successfully.";
            return RedirectToPage(Input.Role == AppRoles.Coordinator ? "Coordinators" : "Agents");
        }

        /// <summary>Load LGAs and, if an LGA name is selected, load its Wards.</summary>
        private async Task LoadDropdownsAsync(string? lgaName)
        {
            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            if (!string.IsNullOrEmpty(lgaName))
            {
                var lga = LGAs.FirstOrDefault(l => l.Name == lgaName);
                if (lga is not null)
                    Wards = await _db.Wards
                        .Where(w => w.LocalGovernmentAreaId == lga.Id && w.IsActive)
                        .OrderBy(w => w.Name).ToListAsync();
            }
        }
    }
}
