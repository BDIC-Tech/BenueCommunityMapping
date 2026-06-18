using BenueCommunityMapping.Authorization;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Questionnaire
{
    public class ViewModel : PageModel
    {
        private readonly ISubmissionService           _submissions;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly IAuthorizationService        _authz;

        public ViewModel(
            ISubmissionService           submissions,
            UserManager<ApplicationUser> userMgr,
            IAuthorizationService        authz)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
            _authz       = authz;
        }

        public QuestionnaireSubmission Submission { get; private set; } = null!;

        private async Task<IActionResult?> LoadAndAuthoriseAsync(Guid id)
        {
            var submission = await _submissions.GetByIdAsync(id);
            if (submission is null) return NotFound();

            var auth = await _authz.AuthorizeAsync(
                User, submission, new SubmissionOwnerRequirement());
            if (!auth.Succeeded) return Forbid();

            Submission = submission;
            return null;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
            => (await LoadAndAuthoriseAsync(id)) ?? Page();

        // ── Coordinator: mark reviewed ─────────────────────────────
        public async Task<IActionResult> OnPostReviewAsync(Guid id)
        {
            var guard = await LoadAndAuthoriseAsync(id);
            if (guard is not null) return guard;

            var user = await _userMgr.GetUserAsync(User);
            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.ReviewedByCoordinator,
                $"Reviewed by {user!.FullName} on {DateTime.Now:dd MMM yyyy}",
                user!.Id,
                AppRoles.Coordinator);

            TempData["Success"] = "Marked as reviewed.";
            return RedirectToPage(new { id });
        }

        // ── Admin: approve ─────────────────────────────────────────
        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var guard = await LoadAndAuthoriseAsync(id);
            if (guard is not null) return guard;

            var user = await _userMgr.GetUserAsync(User);
            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.ApprovedByAdmin,
                $"Approved by {user!.FullName} on {DateTime.Now:dd MMM yyyy}",
                user!.Id,
                AppRoles.Admin);

            TempData["Success"] = "Submission approved.";
            return RedirectToPage(new { id });
        }

        // ── Admin: reject ──────────────────────────────────────────
        public async Task<IActionResult> OnPostRejectAsync(Guid id, string reason)
        {
            var guard = await LoadAndAuthoriseAsync(id);
            if (guard is not null) return guard;

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Rejection reason is mandatory.";
                return RedirectToPage(new { id });
            }

            var user = await _userMgr.GetUserAsync(User);
            var role = (await _userMgr.GetRolesAsync(user!)).FirstOrDefault() ?? AppRoles.Admin;

            await _submissions.UpdateStatusAsync(
                id, SubmissionStatus.Rejected,
                reason,
                user!.Id,
                role);

            TempData["Success"] = "Submission rejected.";
            return RedirectToPage(new { id });
        }
    }
}
