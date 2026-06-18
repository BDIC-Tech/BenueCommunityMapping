using BenueCommunityMapping.Helpers;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Admin
{
    public class SubmissionsModel : PageModel
    {
        private readonly ISubmissionService           _submissions;
        private readonly UserManager<ApplicationUser> _userMgr;
        private const int PageSize = 20;

        public SubmissionsModel(ISubmissionService submissions, UserManager<ApplicationUser> userMgr)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
        }

        public PagedResult<SubmissionListItem> List { get; private set; } = null!;
        public string? Search { get; private set; }
        public SubmissionStatus? StatusFilter { get; private set; }

        private async Task<ApplicationUser> GetCallerAsync()
        {
            var user = (await _userMgr.GetUserAsync(User))!;
            user.CachedRole = AppRoles.Admin;
            return user;
        }

        public async Task OnGetAsync(string? search, SubmissionStatus? status, int p = 1)
        {
            Search       = search;
            StatusFilter = status;
            var all      = await _submissions.GetSubmissionsAsync(await GetCallerAsync(), search, status);
            List         = new PagedResult<SubmissionListItem>(all, p, PageSize);
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var user = await GetCallerAsync();
            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.ApprovedByAdmin,
                $"Approved by {user.FullName} on {DateTime.Now:dd MMM yyyy}",
                user.Id,
                AppRoles.Admin);

            TempData["Success"] = "Submission approved successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Rejection reason is mandatory.";
                return RedirectToPage();
            }
            var user = await GetCallerAsync();
            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.Rejected,
                reason,
                user.Id,
                AppRoles.Admin);

            TempData["Success"] = "Submission rejected.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            await _submissions.DeleteAsync(id);
            TempData["Success"] = "Submission deleted.";
            return RedirectToPage();
        }
    }
}
