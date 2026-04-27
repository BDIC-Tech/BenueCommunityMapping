using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Email_Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Services
{
    public record UserListItem(
        string  Id,
        string  FullName,
        string  Email,
        string? Phone,
        string  Role,
        string? LGA,
        string? AssignedWard,
        string? CoordinatorName,
        bool    IsActive,
        DateTime CreatedAt,
        int     SubmissionCount);

    public class CreateUserRequest
    {
        public string  FirstName     { get; set; } = string.Empty;
        public string  LastName      { get; set; } = string.Empty;
        public string  Email         { get; set; } = string.Empty;
        public string  Password      { get; set; } = string.Empty;
        public string  Role          { get; set; } = AppRoles.Agent;
        public string? LGA           { get; set; }
        public string? Ward          { get; set; }
        public string? CoordinatorId { get; set; }
    }

    public interface IUserService
    {
        Task<IReadOnlyList<UserListItem>> GetUsersAsync(ApplicationUser caller, string role, string? search = null);
        Task<ApplicationUser?>            GetByIdAsync(string id);
        Task<(bool ok, string[] errors)>  CreateAsync(CreateUserRequest req);
        Task<(bool ok, string[] errors)>  UpdateAsync(string id, CreateUserRequest req);
        Task                              SetActiveAsync(string id, bool active);
        Task<(bool ok, string[] errors)>  ResetPasswordAsync(string id, string newPassword);
        Task<IReadOnlyList<ApplicationUser>> GetCoordinatorsAsync();
        Task<(bool ok, string error)>     ResendVerificationEmailAsync(string email);
    }

    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser>  _userMgr;
        private readonly RoleManager<IdentityRole>     _roleMgr;
        private readonly Data.AppDbContext             _db;
        private readonly IEmailTemplateService         _emailTpl;
        private readonly IHttpContextAccessor          _httpCtx;

        public UserService(
            UserManager<ApplicationUser> userMgr,
            RoleManager<IdentityRole>    roleMgr,
            Data.AppDbContext            db,
            IEmailTemplateService        emailTpl,
            IHttpContextAccessor         httpCtx)
        {
            _userMgr  = userMgr;
            _roleMgr  = roleMgr;
            _db       = db;
            _emailTpl = emailTpl;
            _httpCtx  = httpCtx;
        }

        public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(
            ApplicationUser caller, string role, string? search = null)
        {
            var usersInRole = await _userMgr.GetUsersInRoleAsync(role);

            IEnumerable<ApplicationUser> users = usersInRole;

            // Coordinators only see their own agents
            if (caller.CachedRole == AppRoles.Coordinator && role == AppRoles.Agent)
                users = users.Where(u => u.CoordinatorId == caller.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                users = users.Where(u =>
                    u.FullName.ToLower().Contains(term) ||
                    (u.Email ?? "").ToLower().Contains(term));
            }

            var result = new List<UserListItem>();
            foreach (var u in users.OrderBy(u => u.LastName))
            {
                var coord = u.CoordinatorId is not null
                    ? await _userMgr.FindByIdAsync(u.CoordinatorId)
                    : null;
                var subCount = await _db.Submissions.CountAsync(s => s.AgentId == u.Id);
                result.Add(new UserListItem(
                    u.Id, u.FullName, u.Email ?? "", u.PhoneNumber,
                    role, u.LocalGovernmentArea, u.AssignedWard,
                    coord?.FullName, u.IsActive, u.CreatedAt, subCount));
            }
            return result;
        }

        public async Task<ApplicationUser?> GetByIdAsync(string id)
            => await _userMgr.FindByIdAsync(id);

        public async Task<(bool ok, string[] errors)> CreateAsync(CreateUserRequest req)
        {
            // Admin accounts (created via seeder) already have EmailConfirmed = true.
            // Coordinator and Agent accounts require email verification.
            bool requiresVerification = req.Role == AppRoles.Coordinator ||
                                        req.Role == AppRoles.Agent;

            var user = new ApplicationUser
            {
                UserName            = req.Email,
                Email               = req.Email,
                FirstName           = req.FirstName,
                LastName            = req.LastName,
                LocalGovernmentArea = req.LGA,
                AssignedWard        = req.Ward,
                CoordinatorId       = req.Role == AppRoles.Agent ? req.CoordinatorId : null,
                IsActive            = true,
                EmailConfirmed      = !requiresVerification   // false for Coord/Agent
            };

            var result = await _userMgr.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return (false, result.Errors.Select(e => e.Description).ToArray());

            await _userMgr.AddToRoleAsync(user, req.Role);

            // ── Send HTML verification email ─────────────────────────────
            if (requiresVerification)
            {
                try
                {
                    var token = await _userMgr.GenerateEmailConfirmationTokenAsync(user);
                    var link  = BuildConfirmationLink(user.Id, token);

                    if (req.Role == AppRoles.Coordinator)
                    {
                        await _emailTpl.SendCoordinatorVerificationAsync(
                            toEmail:          user.Email!,
                            fullName:         user.FullName,
                            lga:              user.LocalGovernmentArea ?? "N/A",
                            confirmationLink: link);
                    }
                    else // Agent
                    {
                        // Resolve coordinator name for the email body
                        string coordName = "Your Coordinator";
                        if (!string.IsNullOrEmpty(user.CoordinatorId))
                        {
                            var coord = await _userMgr.FindByIdAsync(user.CoordinatorId);
                            if (coord is not null) coordName = coord.FullName;
                        }

                        await _emailTpl.SendAgentVerificationAsync(
                            toEmail:          user.Email!,
                            fullName:         user.FullName,
                            lga:              user.LocalGovernmentArea ?? "N/A",
                            ward:             user.AssignedWard        ?? "N/A",
                            coordinatorName:  coordName,
                            confirmationLink: link);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail — the account was created successfully.
                    // The admin can resend or the user can request via support.
                    var logger = _httpCtx.HttpContext?
                        .RequestServices.GetService<ILogger<UserService>>();
                    logger?.LogError(ex,
                        "Verification email failed for {Email}", user.Email);
                }
            }

            return (true, Array.Empty<string>());
        }

        public async Task<(bool ok, string[] errors)> UpdateAsync(string id, CreateUserRequest req)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user is null) return (false, new[] { "User not found." });

            user.FirstName           = req.FirstName;
            user.LastName            = req.LastName;
            user.LocalGovernmentArea = req.LGA;
            user.AssignedWard        = req.Ward;
            if (req.Role == AppRoles.Agent)
                user.CoordinatorId = req.CoordinatorId;

            var result = await _userMgr.UpdateAsync(user);
            if (!result.Succeeded)
                return (false, result.Errors.Select(e => e.Description).ToArray());

            // Sync role
            var currentRoles = await _userMgr.GetRolesAsync(user);
            if (!string.IsNullOrEmpty(req.Role) && !currentRoles.Contains(req.Role))
            {
                await _userMgr.RemoveFromRolesAsync(user, currentRoles);
                await _userMgr.AddToRoleAsync(user, req.Role);
            }

            return (true, Array.Empty<string>());
        }

        public async Task SetActiveAsync(string id, bool active)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user is null) return;
            user.IsActive = active;
            await _userMgr.UpdateAsync(user);
        }

        public async Task<(bool ok, string[] errors)> ResetPasswordAsync(string id, string newPassword)
        {
            var user = await _userMgr.FindByIdAsync(id);
            if (user is null) return (false, new[] { "User not found." });

            var token  = await _userMgr.GeneratePasswordResetTokenAsync(user);
            var result = await _userMgr.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded
                ? (true, Array.Empty<string>())
                : (false, result.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<IReadOnlyList<ApplicationUser>> GetCoordinatorsAsync()
            => (await _userMgr.GetUsersInRoleAsync(AppRoles.Coordinator))
               .Where(u => u.IsActive)
               .OrderBy(u => u.FullName)
               .ToList();

        public async Task<(bool ok, string error)> ResendVerificationEmailAsync(string email)
        {
            var user = await _userMgr.FindByEmailAsync(email);
            if (user is null) return (false, "User not found.");
            if (user.EmailConfirmed) return (false, "Email is already verified.");

            var roles = await _userMgr.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? AppRoles.Agent;

            try
            {
                var token = await _userMgr.GenerateEmailConfirmationTokenAsync(user);
                var link  = BuildConfirmationLink(user.Id, token);

                if (role == AppRoles.Coordinator)
                {
                    await _emailTpl.SendCoordinatorVerificationAsync(
                        toEmail:          user.Email!,
                        fullName:         user.FullName,
                        lga:              user.LocalGovernmentArea ?? "N/A",
                        confirmationLink: link);
                }
                else // Agent
                {
                    string coordName = "Your Coordinator";
                    if (!string.IsNullOrEmpty(user.CoordinatorId))
                    {
                        var coord = await _userMgr.FindByIdAsync(user.CoordinatorId);
                        if (coord is not null) coordName = coord.FullName;
                    }

                    await _emailTpl.SendAgentVerificationAsync(
                        toEmail:          user.Email!,
                        fullName:         user.FullName,
                        lga:              user.LocalGovernmentArea ?? "N/A",
                        ward:             user.AssignedWard        ?? "N/A",
                        coordinatorName:  coordName,
                        confirmationLink: link);
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                var logger = _httpCtx.HttpContext?
                    .RequestServices.GetService<ILogger<UserService>>();
                logger?.LogError(ex, "Verification email resend failed for {Email}", user.Email);
                return (false, "Failed to send the verification email. Please try again later.");
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Builds an absolute confirm-email URL using the current request's
        /// scheme + host.  Falls back to a relative URL when no HTTP context
        /// is available (e.g. during unit tests).
        /// </summary>
        private string BuildConfirmationLink(string userId, string token)
        {
            var ctx = _httpCtx.HttpContext;
            if (ctx is null)
                return $"/Account/ConfirmEmail?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";

            var request  = ctx.Request;
            var baseUrl  = $"{request.Scheme}://{request.Host}";
            return $"{baseUrl}/Account/ConfirmEmail" +
                   $"?userId={Uri.EscapeDataString(userId)}" +
                   $"&token={Uri.EscapeDataString(token)}";
        }
    }
}
