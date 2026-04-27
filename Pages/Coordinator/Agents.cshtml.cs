using BenueCommunityMapping.Helpers;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Coordinator
{
    public class AgentsModel : PageModel
    {
        private readonly IUserService                 _users;
        private readonly UserManager<ApplicationUser> _userMgr;
        private const int PageSize = 20;

        public AgentsModel(IUserService users, UserManager<ApplicationUser> userMgr)
        {
            _users   = users;
            _userMgr = userMgr;
        }

        public PagedResult<UserListItem> List { get; private set; } = null!;

        private async Task<ApplicationUser> GetCallerAsync()
        {
            var u = (await _userMgr.GetUserAsync(User))!;
            u.CachedRole = AppRoles.Coordinator;
            return u;
        }

        public async Task OnGetAsync(int p = 1)
        {
            var all = await _users.GetUsersAsync(await GetCallerAsync(), AppRoles.Agent);
            List   = new PagedResult<UserListItem>(all, p, PageSize);
        }

        public async Task<IActionResult> OnPostToggleAsync(string id)
        {
            var target = await _users.GetByIdAsync(id);
            if (target is not null)
            {
                var caller = await GetCallerAsync();
                // Only allow toggling agents assigned to this coordinator
                if (target.CoordinatorId == caller.Id)
                    await _users.SetActiveAsync(id, !target.IsActive);
            }

            TempData["Success"] = "Agent status updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id, string newPassword)
        {
            var target = await _users.GetByIdAsync(id);
            if (target is not null)
            {
                var caller = await GetCallerAsync();
                // Only allow resetting password for agents assigned to this coordinator
                if (target.CoordinatorId == caller.Id)
                {
                    var (ok, errors) = await _users.ResetPasswordAsync(id, newPassword);
                    if (!ok)
                    {
                        TempData["Error"] = string.Join("; ", errors);
                        return RedirectToPage();
                    }
                }
            }

            TempData["Success"] = "Password reset successfully.";
            return RedirectToPage();
        }
    }
}
