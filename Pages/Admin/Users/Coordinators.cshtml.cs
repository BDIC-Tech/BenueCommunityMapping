using BenueCommunityMapping.Helpers;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Admin.Users
{
    public class CoordinatorsModel : PageModel
    {
        private readonly IUserService                 _users;
        private readonly UserManager<ApplicationUser> _userMgr;
        private const int PageSize = 20;

        public CoordinatorsModel(IUserService users, UserManager<ApplicationUser> userMgr)
        {
            _users   = users;
            _userMgr = userMgr;
        }

        public PagedResult<UserListItem> List { get; private set; } = null!;
        public string? Search { get; private set; }

        private async Task<ApplicationUser> GetCallerAsync()
        {
            var u = (await _userMgr.GetUserAsync(User))!;
            u.CachedRole = AppRoles.Admin;
            return u;
        }

        public async Task OnGetAsync(string? search, int p = 1)
        {
            Search = search;
            var all  = await _users.GetUsersAsync(await GetCallerAsync(), AppRoles.Coordinator, search);
            List     = new PagedResult<UserListItem>(all, p, PageSize);
        }

        public async Task<IActionResult> OnPostToggleAsync(string id)
        {
            var target = await _users.GetByIdAsync(id);
            if (target is not null)
                await _users.SetActiveAsync(id, !target.IsActive);

            TempData["Success"] = "User status updated.";
            return RedirectToPage();
        }
    }
}
