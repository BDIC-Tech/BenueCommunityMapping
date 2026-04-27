using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ISubmissionService            _submissions;
        private readonly UserManager<ApplicationUser>  _userMgr;

        public IndexModel(ISubmissionService submissions, UserManager<ApplicationUser> userMgr)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
        }

        public string UserFullName { get; private set; } = string.Empty;
        public string UserRole     { get; private set; } = string.Empty;
        public DashboardStats Stats { get; private set; } = default!;
        public IReadOnlyList<SubmissionListItem> RecentSubmissions { get; private set; } = [];

        public async Task OnGetAsync()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return;

            var roles = await _userMgr.GetRolesAsync(user);
            user.CachedRole = roles.FirstOrDefault() ?? AppRoles.Agent;

            UserFullName = user.FullName;
            UserRole     = user.CachedRole;

            Stats = await _submissions.GetStatsAsync(user);

            var all = await _submissions.GetSubmissionsAsync(user);
            RecentSubmissions = all.Take(10).ToList();
        }
    }
}
