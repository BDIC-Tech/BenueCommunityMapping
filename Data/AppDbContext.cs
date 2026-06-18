using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Models.Survey;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── Geographic hierarchy ──────────────────────────────────────
        public DbSet<LocalGovernmentArea> LGAs        { get; set; }
        public DbSet<Ward>                Wards       { get; set; }
        public DbSet<Kindred>             Kindreds    { get; set; }
        public DbSet<Community>           Communities { get; set; }

        // ── Submissions ───────────────────────────────────────────────
        public DbSet<QuestionnaireSubmission> Submissions { get; set; }

        // ── Pre-computed analytics (raw data stays separate) ──────────
        public DbSet<AnalyticsSnapshot> AnalyticsSnapshots { get; set; }

        // ── Section detail tables ─────────────────────────────────────
        public DbSet<Market>                 Markets                  { get; set; }
        public DbSet<HealthFacility>         HealthFacilities         { get; set; }
        public DbSet<OtherHealthFacility>    OtherHealthFacilities    { get; set; }
        public DbSet<EducationalInstitution> EducationalInstitutions  { get; set; }
        public DbSet<AccessRoad>             AccessRoads              { get; set; }
        public DbSet<FinancialService>       FinancialServices        { get; set; }
        public DbSet<NaturalFeature>         NaturalFeatures          { get; set; }
        public DbSet<IndustrialActivity>     IndustrialActivities     { get; set; }
        public DbSet<MiningActivity>         MiningActivities         { get; set; }
        public DbSet<EnvironmentalChallenge> EnvironmentalChallenges  { get; set; }
        public DbSet<ReligiousGroup>         ReligiousGroups          { get; set; }
        public DbSet<GSMNetwork>             GSMNetworks              { get; set; }
        public DbSet<SecurityService>        SecurityServices         { get; set; }
        public DbSet<VulnerableGroup>        VulnerableGroups         { get; set; }
        public DbSet<SocialProtection>       SocialProtections        { get; set; }
        public DbSet<SecurityProgramme>      SecurityProgrammes       { get; set; }
        public DbSet<SecurityIncident>       SecurityIncidents        { get; set; }
        public DbSet<IDPCamp>                IDPCamps                 { get; set; }
        public DbSet<MigrantSettlerActivity> MigrantSettlerActivities { get; set; }
        public DbSet<NGO>                    NGOs                     { get; set; }
        public DbSet<PriorityNeed>           PriorityNeeds            { get; set; }
        public DbSet<ConsentSignatory>       ConsentSignatories       { get; set; }
        public DbSet<QuestionnaireWorkflowHistory> WorkflowHistories  { get; set; }

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // CachedRole is [NotMapped] on the entity – EF ignores it automatically.

            // ── ApplicationUser self-reference (Coordinator → Agents) ─────
            b.Entity<ApplicationUser>(e =>
            {
                e.HasMany(u => u.Agents)
                 .WithOne(u => u.Coordinator)
                 .HasForeignKey(u => u.CoordinatorId)
                 .OnDelete(DeleteBehavior.Restrict);   // prevent cascade loop

                // Agent FK: NoAction prevents the multiple-cascade-path SQL Server error.
                // Submissions are deleted in application code before deleting the user.
                e.HasMany(u => u.Submissions)
                 .WithOne(s => s.Agent)
                 .HasForeignKey(s => s.AgentId)
                 .OnDelete(DeleteBehavior.NoAction);
            });

            // ── Geographic hierarchy ──────────────────────────────────────
            b.Entity<LocalGovernmentArea>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.HasMany(x => x.Wards)
                 .WithOne(x => x.LocalGovernmentArea)
                 .HasForeignKey(x => x.LocalGovernmentAreaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Ward>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.HasMany(x => x.Kindreds)
                 .WithOne(x => x.Ward)
                 .HasForeignKey(x => x.WardId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Kindred>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.HasMany(x => x.Communities)
                 .WithOne(x => x.Kindred)
                 .HasForeignKey(x => x.KindredId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Community>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                // Ignore [NotMapped] computed nav properties
                e.Ignore(c => c.Ward);
                e.Ignore(c => c.LGA);
                e.HasMany(x => x.Submissions)
                 .WithOne(s => s.Community)
                 .HasForeignKey(s => s.CommunityId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── QuestionnaireSubmission ───────────────────────────────────
            b.Entity<QuestionnaireSubmission>(e =>
            {
                e.HasKey(s => s.Id);

                // Coordinator FK: NoAction for same reason – avoids cascade cycles.
                e.HasOne(s => s.Coordinator)
                 .WithMany()
                 .HasForeignKey(s => s.CoordinatorId)
                 .OnDelete(DeleteBehavior.NoAction);

                // Analytical indexes on FK and filter columns
                e.HasIndex(s => s.CommunityId);
                e.HasIndex(s => s.Status);
                e.HasIndex(s => s.AgentId);
                e.HasIndex(s => s.CoordinatorId);
                e.HasIndex(s => s.SubmittedAt);
            });

            b.Entity<QuestionnaireWorkflowHistory>(e =>
            {
                e.HasKey(h => h.Id);

                e.HasOne(h => h.Submission)
                 .WithMany(s => s.WorkflowHistory)
                 .HasForeignKey(h => h.SubmissionId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(h => h.Actor)
                 .WithMany()
                 .HasForeignKey(h => h.ActorId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(h => h.SubmissionId);
                e.HasIndex(h => h.ActorId);
            });
            
            b.Entity<AnalyticsSnapshot>(entity =>
            {
                entity.Property(e => e.KeywordsMarketChallenges).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsHealthDiseases).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsEducationChallenges).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsTransportChallenges).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsFinancialChallenges).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsNaturalFeatures).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsTelecomChallenges).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsSecurityIssues).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsDisputeResolution).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsDisplacementCauses).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsPriorityNeeds).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsSecurityIncidents).HasColumnType("TEXT");
                entity.Property(e => e.KeywordsAllChallenges).HasColumnType("TEXT");
            });

            // ── AnalyticsSnapshot indexes ─────────────────────────────────
            b.Entity<AnalyticsSnapshot>(e =>
            {
                // Composite unique: one snapshot per scope+type combination (all-time)
                e.HasIndex(s => new { s.ScopeType, s.ScopeId, s.FromDate, s.ToDate })
                 .IsUnique(false); // not unique – allows multiple time windows
                e.HasIndex(s => s.ScopeType);
                e.HasIndex(s => s.ComputedAt);
            });

            // ── Detail tables: FK to Submission + cascade + index ─────────
            // Each detail table has SubmissionId FK pointing to QuestionnaireSubmission.
            // Inverse navigation properties are explicitly specified so EF Core
            // correctly populates them even with AsNoTrackingWithIdentityResolution + AsSplitQuery.
            ConfigDetail<Market>(b, nameof(QuestionnaireSubmission.Markets));
            ConfigDetail<HealthFacility>(b, nameof(QuestionnaireSubmission.HealthFacilities));
            ConfigDetail<OtherHealthFacility>(b, nameof(QuestionnaireSubmission.OtherHealthFacilities));
            ConfigDetail<EducationalInstitution>(b, nameof(QuestionnaireSubmission.EducationalInstitutions));
            ConfigDetail<AccessRoad>(b, nameof(QuestionnaireSubmission.AccessRoads));
            ConfigDetail<FinancialService>(b, nameof(QuestionnaireSubmission.FinancialServices));
            ConfigDetail<NaturalFeature>(b, nameof(QuestionnaireSubmission.NaturalFeatures));
            ConfigDetail<IndustrialActivity>(b, nameof(QuestionnaireSubmission.IndustrialActivities));
            ConfigDetail<MiningActivity>(b, nameof(QuestionnaireSubmission.MiningActivities));
            ConfigDetail<EnvironmentalChallenge>(b, nameof(QuestionnaireSubmission.EnvironmentalChallenges));
            ConfigDetail<ReligiousGroup>(b, nameof(QuestionnaireSubmission.ReligiousGroups));
            ConfigDetail<GSMNetwork>(b, nameof(QuestionnaireSubmission.GSMNetworks));
            ConfigDetail<SecurityService>(b, nameof(QuestionnaireSubmission.SecurityServices));
            ConfigDetail<VulnerableGroup>(b, nameof(QuestionnaireSubmission.VulnerableGroups));
            ConfigDetail<SocialProtection>(b, nameof(QuestionnaireSubmission.SocialProtections));
            ConfigDetail<SecurityProgramme>(b, nameof(QuestionnaireSubmission.SecurityProgrammes));
            ConfigDetail<SecurityIncident>(b, nameof(QuestionnaireSubmission.SecurityIncidents));
            ConfigDetail<IDPCamp>(b, nameof(QuestionnaireSubmission.IDPCamps));
            ConfigDetail<MigrantSettlerActivity>(b, nameof(QuestionnaireSubmission.MigrantSettlerActivities));
            ConfigDetail<NGO>(b, nameof(QuestionnaireSubmission.NGOs));
            ConfigDetail<PriorityNeed>(b, nameof(QuestionnaireSubmission.PriorityNeeds));
            ConfigDetail<ConsentSignatory>(b, nameof(QuestionnaireSubmission.ConsentSignatories));
        }

        /// <summary>
        /// Configures the standard FK (SubmissionId → QuestionnaireSubmission),
        /// cascade-delete, and an index for every section detail table.
        /// The inverse navigation property name on QuestionnaireSubmission is specified explicitly.
        /// </summary>
        private static void ConfigDetail<T>(ModelBuilder b, string inverseNav)
            where T : class
        {
            b.Entity<T>(e =>
            {
                e.HasOne(typeof(QuestionnaireSubmission), "Submission")
                 .WithMany(inverseNav)
                 .HasForeignKey("SubmissionId")
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex("SubmissionId");
            });
        }
    }
}
