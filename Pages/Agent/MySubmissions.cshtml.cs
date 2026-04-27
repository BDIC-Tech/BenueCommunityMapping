using BenueCommunityMapping.Helpers;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Agent
{
    public class MySubmissionsModel : PageModel
    {
        private readonly ISubmissionService           _submissions;
        private readonly UserManager<ApplicationUser> _userMgr;
        private const int PageSize = 20;

        public MySubmissionsModel(ISubmissionService submissions, UserManager<ApplicationUser> userMgr)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
        }

        public PagedResult<SubmissionListItem> List { get; private set; } = null!;
        public string? Search       { get; private set; }
        public SubmissionStatus? StatusFilter { get; private set; }

        public async Task OnGetAsync(string? search, SubmissionStatus? status, int p = 1)
        {
            Search       = search;
            StatusFilter = status;

            var user = await _userMgr.GetUserAsync(User);
            if (user is null) { List = new PagedResult<SubmissionListItem>([], 1, PageSize); return; }

            user.CachedRole = AppRoles.Agent;
            var all = await _submissions.GetSubmissionsAsync(user, search, status);
            List    = new PagedResult<SubmissionListItem>(all, p, PageSize);
        }
    }
}
