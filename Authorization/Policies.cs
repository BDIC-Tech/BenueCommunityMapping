using BenueCommunityMapping.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace BenueCommunityMapping.Authorization
{
    // ─────────────────────────────────────────────────────────────
    // POLICY NAMES
    // ─────────────────────────────────────────────────────────────
    public static class Policies
    {
        public const string AdminOnly       = "AdminOnly";
        public const string AdminOrCoord    = "AdminOrCoordinator";
        public const string AnyAuthUser     = "AnyAuthenticatedUser";
        public const string CanManageAgents = "CanManageAgents";
    }

    // ─────────────────────────────────────────────────────────────
    // REQUIREMENT: submission ownership check
    // ─────────────────────────────────────────────────────────────
    public class SubmissionOwnerRequirement : IAuthorizationRequirement { }

    /// <summary>
    /// Passes when:
    ///   - User is Admin   → always
    ///   - User is Coordinator whose CoordinatorId matches submission.CoordinatorId → always
    ///   - User is Agent   → only when AgentId == submission.AgentId
    /// </summary>
    public class SubmissionOwnerHandler
        : AuthorizationHandler<SubmissionOwnerRequirement, QuestionnaireSubmission>
    {
        private readonly UserManager<ApplicationUser> _userMgr;

        public SubmissionOwnerHandler(UserManager<ApplicationUser> userMgr)
            => _userMgr = userMgr;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SubmissionOwnerRequirement requirement,
            QuestionnaireSubmission resource)
        {
            var user = await _userMgr.GetUserAsync(context.User);
            if (user is null) return;

            if (await _userMgr.IsInRoleAsync(user, AppRoles.Admin))
            {
                context.Succeed(requirement); return;
            }

            if (await _userMgr.IsInRoleAsync(user, AppRoles.Coordinator) &&
                resource.CoordinatorId == user.Id)
            {
                context.Succeed(requirement); return;
            }

            if (await _userMgr.IsInRoleAsync(user, AppRoles.Agent) &&
                resource.AgentId == user.Id)
            {
                context.Succeed(requirement);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // REQUIREMENT: user management – coordinator may only manage
    //             agents assigned to them
    // ─────────────────────────────────────────────────────────────
    public class ManageUserRequirement : IAuthorizationRequirement { }

    public class ManageUserHandler
        : AuthorizationHandler<ManageUserRequirement, ApplicationUser>
    {
        private readonly UserManager<ApplicationUser> _userMgr;

        public ManageUserHandler(UserManager<ApplicationUser> userMgr)
            => _userMgr = userMgr;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ManageUserRequirement requirement,
            ApplicationUser targetUser)
        {
            var actor = await _userMgr.GetUserAsync(context.User);
            if (actor is null) return;

            if (await _userMgr.IsInRoleAsync(actor, AppRoles.Admin))
            {
                context.Succeed(requirement); return;
            }

            // Coordinator can only manage their own agents
            if (await _userMgr.IsInRoleAsync(actor, AppRoles.Coordinator) &&
                targetUser.CoordinatorId == actor.Id)
            {
                context.Succeed(requirement);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // EXTENSION: register all policies
    // ─────────────────────────────────────────────────────────────
    public static class AuthorizationExtensions
    {
        public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.AdminOnly,    p => p.RequireRole(AppRoles.Admin))
                .AddPolicy(Policies.AdminOrCoord, p => p.RequireRole(AppRoles.Admin, AppRoles.Coordinator))
                .AddPolicy(Policies.AnyAuthUser,  p => p.RequireAuthenticatedUser())
                .AddPolicy(Policies.CanManageAgents, p => p.RequireRole(AppRoles.Admin, AppRoles.Coordinator));

            services.AddScoped<IAuthorizationHandler, SubmissionOwnerHandler>();
            services.AddScoped<IAuthorizationHandler, ManageUserHandler>();

            return services;
        }
    }
}
