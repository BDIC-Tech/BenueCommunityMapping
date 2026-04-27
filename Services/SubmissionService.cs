using BenueCommunityMapping.Services.Analytics;
using Microsoft.Extensions.DependencyInjection;
using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Services
{
    public record SubmissionListItem(
        Guid     Id,
        string   CommunityName,
        string   LGA,
        string   Ward,
        string   AgentName,
        string?  CoordinatorName,
        SubmissionStatus Status,
        DateTime CreatedAt,
        DateTime? SubmittedAt);

    public record DashboardStats(
        int TotalSubmissions,
        int DraftCount,
        int SubmittedCount,
        int ReviewedCount,
        int ApprovedCount,
        int RejectedCount,
        int TotalAgents,
        int TotalCoordinators);

    public interface ISubmissionService
    {
        Task<IReadOnlyList<SubmissionListItem>> GetSubmissionsAsync(
            ApplicationUser caller, string? search = null, SubmissionStatus? status = null);
        Task<QuestionnaireSubmission?> GetByIdAsync(Guid id);
        Task<QuestionnaireSubmission>  CreateDraftAsync(string agentId, string? coordinatorId, int communityId);
        Task                           SaveAsync(QuestionnaireSubmission submission);
        Task                           SubmitAsync(Guid id);
        Task                           UpdateStatusAsync(Guid id, SubmissionStatus newStatus, string? notes, string actorRole);
        Task                           DeleteAsync(Guid id);
        Task<DashboardStats>           GetStatsAsync(ApplicationUser caller);
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly AppDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;
        public SubmissionService(AppDbContext db, IServiceScopeFactory scopeFactory)
        { _db = db; _scopeFactory = scopeFactory; }

        // Base include chain for all list queries
        private IQueryable<QuestionnaireSubmission> WithIncludes() =>
            _db.Submissions
               .Include(s => s.Agent)
               .Include(s => s.Coordinator)
               .Include(s => s.Community)
                   .ThenInclude(c => c.Kindred)
                       .ThenInclude(k => k.Ward)
                           .ThenInclude(w => w.LocalGovernmentArea);

        public async Task<IReadOnlyList<SubmissionListItem>> GetSubmissionsAsync(
            ApplicationUser caller, string? search = null, SubmissionStatus? status = null)
        {
            var q = WithIncludes().AsNoTracking();

            // Role-scope
            if (caller.CachedRole == AppRoles.Coordinator)
                q = q.Where(s => s.CoordinatorId == caller.Id);
            else if (caller.CachedRole == AppRoles.Agent)
                q = q.Where(s => s.AgentId == caller.Id);

            if (status.HasValue)
                q = q.Where(s => s.Status == status.Value);

            var list = await q.OrderByDescending(s => s.UpdatedAt).ToListAsync();

            // In-memory text search (uses navigation props loaded above)
            IEnumerable<QuestionnaireSubmission> filtered = list;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim().ToLower();
                filtered = list.Where(s =>
                    s.Community.Name.ToLower().Contains(t) ||
                    (s.Community.Kindred?.Ward?.LocalGovernmentArea?.Name ?? "").ToLower().Contains(t) ||
                    s.Agent.FullName.ToLower().Contains(t));
            }

            return filtered.Select(s => new SubmissionListItem(
                s.Id,
                s.Community.Name,
                s.Community.Kindred?.Ward?.LocalGovernmentArea?.Name ?? "–",
                s.Community.Kindred?.Ward?.Name ?? "–",
                s.Agent.FullName,
                s.Coordinator?.FullName,
                s.Status,
                s.CreatedAt,
                s.SubmittedAt)).ToList();
        }

        public async Task<QuestionnaireSubmission?> GetByIdAsync(Guid id) =>
            await WithIncludes()
                .AsSplitQuery()
                .AsNoTracking()
                .Include(s => s.Markets)
                .Include(s => s.HealthFacilities)
                .Include(s => s.OtherHealthFacilities)
                .Include(s => s.EducationalInstitutions)
                .Include(s => s.AccessRoads)
                .Include(s => s.FinancialServices)
                .Include(s => s.NaturalFeatures)
                .Include(s => s.IndustrialActivities)
                .Include(s => s.MiningActivities)
                .Include(s => s.EnvironmentalChallenges)
                .Include(s => s.ReligiousGroups)
                .Include(s => s.GSMNetworks)
                .Include(s => s.SecurityServices)
                .Include(s => s.VulnerableGroups)
                .Include(s => s.SocialProtections)
                .Include(s => s.SecurityProgrammes)
                .Include(s => s.SecurityIncidents)
                .Include(s => s.IDPCamps)
                .Include(s => s.MigrantSettlerActivities)
                .Include(s => s.NGOs)
                .Include(s => s.PriorityNeeds)
                .Include(s => s.ConsentSignatories)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<QuestionnaireSubmission> CreateDraftAsync(
            string agentId, string? coordinatorId, int communityId)
        {
            var submission = new QuestionnaireSubmission
            {
                AgentId       = agentId,
                CoordinatorId = coordinatorId,
                CommunityId   = communityId,
                Status        = SubmissionStatus.Draft
            };
            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();
            return submission;
        }

        public async Task SaveAsync(QuestionnaireSubmission submission)
        {
            submission.UpdatedAt = DateTime.UtcNow;

            if (submission.Id == Guid.Empty || !await _db.Submissions.AnyAsync(s => s.Id == submission.Id))
            {
                // New submission
                _db.Submissions.Add(submission);
            }
            else
            {
                // Existing: remove all old detail rows first, then EF will insert the new ones
                var id = submission.Id;
                _db.Markets.RemoveRange(_db.Markets.Where(x => x.SubmissionId == id));
                _db.HealthFacilities.RemoveRange(_db.HealthFacilities.Where(x => x.SubmissionId == id));
                _db.OtherHealthFacilities.RemoveRange(_db.OtherHealthFacilities.Where(x => x.SubmissionId == id));
                _db.EducationalInstitutions.RemoveRange(_db.EducationalInstitutions.Where(x => x.SubmissionId == id));
                _db.AccessRoads.RemoveRange(_db.AccessRoads.Where(x => x.SubmissionId == id));
                _db.FinancialServices.RemoveRange(_db.FinancialServices.Where(x => x.SubmissionId == id));
                _db.NaturalFeatures.RemoveRange(_db.NaturalFeatures.Where(x => x.SubmissionId == id));
                _db.IndustrialActivities.RemoveRange(_db.IndustrialActivities.Where(x => x.SubmissionId == id));
                _db.MiningActivities.RemoveRange(_db.MiningActivities.Where(x => x.SubmissionId == id));
                _db.EnvironmentalChallenges.RemoveRange(_db.EnvironmentalChallenges.Where(x => x.SubmissionId == id));
                _db.ReligiousGroups.RemoveRange(_db.ReligiousGroups.Where(x => x.SubmissionId == id));
                _db.GSMNetworks.RemoveRange(_db.GSMNetworks.Where(x => x.SubmissionId == id));
                _db.SecurityServices.RemoveRange(_db.SecurityServices.Where(x => x.SubmissionId == id));
                _db.VulnerableGroups.RemoveRange(_db.VulnerableGroups.Where(x => x.SubmissionId == id));
                _db.SocialProtections.RemoveRange(_db.SocialProtections.Where(x => x.SubmissionId == id));
                _db.SecurityProgrammes.RemoveRange(_db.SecurityProgrammes.Where(x => x.SubmissionId == id));
                _db.SecurityIncidents.RemoveRange(_db.SecurityIncidents.Where(x => x.SubmissionId == id));
                _db.IDPCamps.RemoveRange(_db.IDPCamps.Where(x => x.SubmissionId == id));
                _db.MigrantSettlerActivities.RemoveRange(_db.MigrantSettlerActivities.Where(x => x.SubmissionId == id));
                _db.NGOs.RemoveRange(_db.NGOs.Where(x => x.SubmissionId == id));
                _db.PriorityNeeds.RemoveRange(_db.PriorityNeeds.Where(x => x.SubmissionId == id));
                _db.ConsentSignatories.RemoveRange(_db.ConsentSignatories.Where(x => x.SubmissionId == id));

                // Save deletes first
                await _db.SaveChangesAsync();

                // Detach navigation properties to avoid tracking conflicts
                // (the ApplicationUser may already be tracked by Identity's UserManager).
                // Foreign-key columns (AgentId, CoordinatorId, CommunityId) are preserved.
                submission.Agent       = null!;
                submission.Coordinator = null;
                submission.Community   = null!;

                // Now update the submission root (scalar columns) and re-add all detail rows
                _db.Submissions.Update(submission);

                // Reset identity Ids so SQL Server auto-generates them
                foreach (var e in submission.Markets) e.Id = 0;
                foreach (var e in submission.HealthFacilities) e.Id = 0;
                foreach (var e in submission.OtherHealthFacilities) e.Id = 0;
                foreach (var e in submission.EducationalInstitutions) e.Id = 0;
                foreach (var e in submission.AccessRoads) e.Id = 0;
                foreach (var e in submission.FinancialServices) e.Id = 0;
                foreach (var e in submission.NaturalFeatures) e.Id = 0;
                foreach (var e in submission.IndustrialActivities) e.Id = 0;
                foreach (var e in submission.MiningActivities) e.Id = 0;
                foreach (var e in submission.EnvironmentalChallenges) e.Id = 0;
                foreach (var e in submission.ReligiousGroups) e.Id = 0;
                foreach (var e in submission.GSMNetworks) e.Id = 0;
                foreach (var e in submission.SecurityServices) e.Id = 0;
                foreach (var e in submission.VulnerableGroups) e.Id = 0;
                foreach (var e in submission.SocialProtections) e.Id = 0;
                foreach (var e in submission.SecurityProgrammes) e.Id = 0;
                foreach (var e in submission.SecurityIncidents) e.Id = 0;
                foreach (var e in submission.IDPCamps) e.Id = 0;
                foreach (var e in submission.MigrantSettlerActivities) e.Id = 0;
                foreach (var e in submission.NGOs) e.Id = 0;
                foreach (var e in submission.PriorityNeeds) e.Id = 0;
                foreach (var e in submission.ConsentSignatories) e.Id = 0;

                // Re-add all detail collections
                if (submission.Markets.Any()) _db.Markets.AddRange(submission.Markets);
                if (submission.HealthFacilities.Any()) _db.HealthFacilities.AddRange(submission.HealthFacilities);
                if (submission.OtherHealthFacilities.Any()) _db.OtherHealthFacilities.AddRange(submission.OtherHealthFacilities);
                if (submission.EducationalInstitutions.Any()) _db.EducationalInstitutions.AddRange(submission.EducationalInstitutions);
                if (submission.AccessRoads.Any()) _db.AccessRoads.AddRange(submission.AccessRoads);
                if (submission.FinancialServices.Any()) _db.FinancialServices.AddRange(submission.FinancialServices);
                if (submission.NaturalFeatures.Any()) _db.NaturalFeatures.AddRange(submission.NaturalFeatures);
                if (submission.IndustrialActivities.Any()) _db.IndustrialActivities.AddRange(submission.IndustrialActivities);
                if (submission.MiningActivities.Any()) _db.MiningActivities.AddRange(submission.MiningActivities);
                if (submission.EnvironmentalChallenges.Any()) _db.EnvironmentalChallenges.AddRange(submission.EnvironmentalChallenges);
                if (submission.ReligiousGroups.Any()) _db.ReligiousGroups.AddRange(submission.ReligiousGroups);
                if (submission.GSMNetworks.Any()) _db.GSMNetworks.AddRange(submission.GSMNetworks);
                if (submission.SecurityServices.Any()) _db.SecurityServices.AddRange(submission.SecurityServices);
                if (submission.VulnerableGroups.Any()) _db.VulnerableGroups.AddRange(submission.VulnerableGroups);
                if (submission.SocialProtections.Any()) _db.SocialProtections.AddRange(submission.SocialProtections);
                if (submission.SecurityProgrammes.Any()) _db.SecurityProgrammes.AddRange(submission.SecurityProgrammes);
                if (submission.SecurityIncidents.Any()) _db.SecurityIncidents.AddRange(submission.SecurityIncidents);
                if (submission.IDPCamps.Any()) _db.IDPCamps.AddRange(submission.IDPCamps);
                if (submission.MigrantSettlerActivities.Any()) _db.MigrantSettlerActivities.AddRange(submission.MigrantSettlerActivities);
                if (submission.NGOs.Any()) _db.NGOs.AddRange(submission.NGOs);
                if (submission.PriorityNeeds.Any()) _db.PriorityNeeds.AddRange(submission.PriorityNeeds);
                if (submission.ConsentSignatories.Any()) _db.ConsentSignatories.AddRange(submission.ConsentSignatories);
            }

            await _db.SaveChangesAsync();
        }

        public async Task SubmitAsync(Guid id)
        {
            var s = await _db.Submissions.FindAsync(id)
                ?? throw new KeyNotFoundException($"Submission {id} not found.");
            s.Status      = SubmissionStatus.Submitted;
            s.SubmittedAt = DateTime.UtcNow;
            s.UpdatedAt   = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(
            Guid id, SubmissionStatus newStatus, string? notes, string actorRole)
        {
            var s = await _db.Submissions.FindAsync(id)
                ?? throw new KeyNotFoundException($"Submission {id} not found.");
            s.Status    = newStatus;
            s.UpdatedAt = DateTime.UtcNow;
            if (actorRole == AppRoles.Coordinator) s.CoordinatorNotes = notes;
            else if (actorRole == AppRoles.Admin)  s.AdminNotes       = notes;
            await _db.SaveChangesAsync();

            // Refresh snapshots in background scope (avoids circular DI)
            if (newStatus == SubmissionStatus.ApprovedByAdmin)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope     = _scopeFactory.CreateScope();
                        var analytics       = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                        var sub = await _db.Submissions
                            .Include(x => x.Community).ThenInclude(c => c.Kindred)
                                .ThenInclude(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea)
                            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                        await analytics.RefreshSnapshotAsync("System", 0);
                        if (sub?.Community?.Kindred?.Ward is not null)
                            await analytics.RefreshSnapshotAsync("LGA",
                                sub.Community.Kindred.Ward.LocalGovernmentAreaId);
                    }
                    catch { /* background – swallow */ }
                });
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var s = await _db.Submissions.FindAsync(id);
            if (s is not null)
            {
                _db.Submissions.Remove(s);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<DashboardStats> GetStatsAsync(ApplicationUser caller)
        {
            var q = _db.Submissions.AsNoTracking();
            if (caller.CachedRole == AppRoles.Coordinator)
                q = q.Where(s => s.CoordinatorId == caller.Id);
            else if (caller.CachedRole == AppRoles.Agent)
                q = q.Where(s => s.AgentId == caller.Id);

            var counts = await q
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int Total(SubmissionStatus st) =>
                counts.FirstOrDefault(c => c.Status == st)?.Count ?? 0;

            int agents = caller.CachedRole == AppRoles.Admin
                ? await _db.Users.CountAsync(u => u.CoordinatorId != null)
                : await _db.Users.CountAsync(u => u.CoordinatorId == caller.Id);

            int coords = 0;
            if (caller.CachedRole == AppRoles.Admin)
            {
                var coordRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == AppRoles.Coordinator);
                if (coordRole != null)
                    coords = await _db.UserRoles.CountAsync(ur => ur.RoleId == coordRole.Id);
            }

            return new DashboardStats(
                counts.Sum(c => c.Count),
                Total(SubmissionStatus.Draft),
                Total(SubmissionStatus.Submitted),
                Total(SubmissionStatus.ReviewedByCoordinator),
                Total(SubmissionStatus.ApprovedByAdmin),
                Total(SubmissionStatus.Rejected),
                agents, coords);
        }
    }
}
