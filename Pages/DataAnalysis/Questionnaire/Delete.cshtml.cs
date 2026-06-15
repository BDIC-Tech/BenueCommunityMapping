using BenueCommunityMapping.Authorization;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.DataAnalysis.Questionnaire
{
    public class DeleteModel : PageModel
    {
        private readonly ISubmissionService           _submissions;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly IAuthorizationService        _authz;

        public DeleteModel(
            ISubmissionService           submissions,
            UserManager<ApplicationUser> userMgr,
            IAuthorizationService        authz)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
            _authz       = authz;
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var submission = await _submissions.GetByIdAsync(id);
            if (submission is null) return NotFound();

            // Only drafts may be deleted by agents; admins can delete anything
            var user  = await _userMgr.GetUserAsync(User);
            var roles = await _userMgr.GetRolesAsync(user!);
            bool isAdmin = roles.Contains(AppRoles.Admin);

            if (!isAdmin && submission.Status != SubmissionStatus.Draft)
            {
                TempData["Error"] = "Only Draft submissions can be deleted.";
                return RedirectToPage("/Agent/MySubmissions");
            }

            var auth = await _authz.AuthorizeAsync(
                User, submission, new SubmissionOwnerRequirement());
            if (!auth.Succeeded) return Forbid();

            await _submissions.DeleteAsync(id);
            TempData["Success"] = "Submission deleted.";

            return isAdmin
                ? RedirectToPage("/Admin/Submissions")
                : RedirectToPage("/Agent/MySubmissions");
        }
    }
}
