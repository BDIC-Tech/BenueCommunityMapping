using BenueCommunityMapping.Helpers;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Coordinator
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
            user.CachedRole = AppRoles.Coordinator;
            return user;
        }

        public async Task OnGetAsync(string? search, SubmissionStatus? status, int p = 1)
        {
            Search       = search;
            StatusFilter = status;
            var all      = await _submissions.GetSubmissionsAsync(await GetCallerAsync(), search, status);
            List         = new PagedResult<SubmissionListItem>(all, p, PageSize);
        }

        public async Task<IActionResult> OnPostReviewAsync(Guid id)
        {
            var user = await GetCallerAsync();
            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.ReviewedByCoordinator,
                $"Reviewed by {user.FullName} on {DateTime.Now:dd MMM yyyy}",
                user.Id,
                AppRoles.Coordinator);

            TempData["Success"] = "Submission marked as reviewed.";
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
                AppRoles.Coordinator);

            TempData["Success"] = "Submission rejected.";
            return RedirectToPage();
        }
    }
}
