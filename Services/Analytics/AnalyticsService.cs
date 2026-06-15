using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Survey;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BenueCommunityMapping.Services.Analytics
{
    /// <inheritdoc cref="IAnalyticsService"/>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _db;

        public AnalyticsService(AppDbContext db) => _db = db;

        // ═══════════════════════════════════════════════════════════════
        // BASE QUERY HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Returns a query scoped by the filter's geographic level and date range.</summary>
        private IQueryable<QuestionnaireSubmission> BaseQuery(AnalyticsFilter f, bool approvedOnly = true)
        {
            IQueryable<QuestionnaireSubmission> q = _db.Submissions
                .Include(s => s.Community)
                    .ThenInclude(c => c.Kindred)
                        .ThenInclude(k => k.Ward)
                            .ThenInclude(w => w.LocalGovernmentArea);

            if (approvedOnly)
                q = q.Where(s => s.Status == SubmissionStatus.ApprovedByAdmin);

            // Geographic scope — most specific wins
            if      (f.CommunityId.HasValue) q = q.Where(s => s.CommunityId == f.CommunityId);
            else if (f.KindredId.HasValue)   q = q.Where(s => s.Community.KindredId == f.KindredId);
            else if (f.WardId.HasValue)      q = q.Where(s => s.Community.Kindred.WardId == f.WardId);
            else if (f.LGAId.HasValue)       q = q.Where(s => s.Community.Kindred.Ward.LocalGovernmentAreaId == f.LGAId);

            // Date range
            if (f.FromDate.HasValue) q = q.Where(s => s.SubmittedAt >= f.FromDate);
            if (f.ToDate.HasValue)   q = q.Where(s => s.SubmittedAt <= f.ToDate);

            return q;
        }

        // Shorthand helpers
        private IQueryable<QuestionnaireSubmission> Approved(AnalyticsFilter f) => BaseQuery(f, true);
        private IQueryable<QuestionnaireSubmission> All(AnalyticsFilter f)      => BaseQuery(f, false);

        // ═══════════════════════════════════════════════════════════════
        // 1. NUMERICAL DASHBOARD
        //    Every metric: Count + Percentage (count/N×100) + Ratio (count/HH×1000)
        // ═══════════════════════════════════════════════════════════════

        public async Task<NumericalDashboard> GetNumericalDashboardAsync(AnalyticsFilter filter)
        {
            var q    = Approved(filter);
            var ids  = await q.Select(s => s.Id).ToListAsync();
            int N    = ids.Count;
            int hh   = N > 0 ? await q.SumAsync(s => s.EstimatedNumberOfHouseholds ?? 0) : 0;
            string scope = await ScopeNameAsync(filter);

            if (N == 0)
                return new NumericalDashboard(scope, 0, 0, [], [], [], [], [], [], [], []);

            // Local helper: Count + Percentage + Ratio
            MetricRow M(string label, int count, int denominator = -1, string? note = null)
            {
                int d = denominator < 0 ? N : denominator;
                return new MetricRow(
                    label, count,
                    d  > 0 ? Math.Round((double)count / d  * 100,  1) : 0,
                    hh > 0 ? Math.Round((double)count / hh * 1000, 2) : 0,
                    note);
            }

            // ── Health ──────────────────────────────────────────────
            int hf   = await _db.HealthFacilities.CountAsync(x => ids.Contains(x.SubmissionId));
            int ohf  = await _db.OtherHealthFacilities.CountAsync(x => ids.Contains(x.SubmissionId));
            var healthMetrics = new List<MetricRow>
            {
                M("Health Facilities (formal)",           hf,  N, $"avg {(N>0?Math.Round((double)hf/N,2):0)}/community"),
                M("Other Health Facilities (pharmacy…)", ohf,  N),
                M("Functional Ambulance / Referral",
                    await q.CountAsync(s => s.FunctionalAmbulanceOrReferral)),
                M("Childbirth Deaths Reported (2 yrs)",
                    await q.CountAsync(s => s.WomenDiedDuringChildbirthLast2Years)),
                M("Night Emergency Transport Access",
                    await q.CountAsync(s => s.PregnantWomenCanAccessEmergencyTransportAtNight)),
            };

            // ── Education ────────────────────────────────────────────
            int sch  = await _db.EducationalInstitutions.CountAsync(x => ids.Contains(x.SubmissionId));
            int dest = await _db.EducationalInstitutions.CountAsync(x => ids.Contains(x.SubmissionId)
                            && x.DestroyedOrClosedDueToConflict == true);
            int oos  = await q.SumAsync(s => s.ChildrenNotInSchool ?? 0);
            var educationMetrics = new List<MetricRow>
            {
                M("Schools / Institutions",             sch, N, $"avg {(N>0?Math.Round((double)sch/N,2):0)}/community"),
                new("Children Not in School (total)",   oos,
                    N > 0 ? Math.Round((double)oos / N, 1) : 0,
                    hh > 0 ? Math.Round((double)oos / hh * 1000, 2) : 0,
                    $"avg {(N>0?Math.Round((double)oos/N,1):0)}/community"),
                M("Schools Destroyed by Conflict",      dest, sch, "of all schools"),
                M("Schools Hosting IDPs",
                    await q.CountAsync(s => s.SchoolsCurrentlyHostingIDPs)),
            };

            // ── Markets ──────────────────────────────────────────────
            int mkt  = await _db.Markets.CountAsync(x => ids.Contains(x.SubmissionId));
            int pmkt = await _db.Markets.CountAsync(x => ids.Contains(x.SubmissionId)
                            && x.InfrastructureCondition == InfrastructureCondition.Poor);
            var marketMetrics = new List<MetricRow>
            {
                M("Markets",                             mkt, N, $"avg {(N>0?Math.Round((double)mkt/N,2):0)}/community"),
                M("Markets: Poor Infrastructure",       pmkt, mkt, "of all markets"),
                M("Markets Affected by Insecurity",
                    await q.CountAsync(s => s.MarketActivitiesAffectedByInsecurity)),
                M("Communities Paying Illegal Levy",
                    await q.CountAsync(s => s.CommunityPaysIllegalLevy)),
            };

            // ── Roads ────────────────────────────────────────────────
            int roads   = await _db.AccessRoads.CountAsync(x => ids.Contains(x.SubmissionId));
            int impassR = await _db.AccessRoads.CountAsync(x => ids.Contains(x.SubmissionId)
                              && x.RainySeasonCondition == RoadPassability.NotPassable);
            int impassD = await _db.AccessRoads.CountAsync(x => ids.Contains(x.SubmissionId)
                              && x.DrySeasonCondition  == RoadPassability.NotPassable);
            var roadMetrics = new List<MetricRow>
            {
                M("Access Roads Recorded",               roads, N),
                M("Main Road: Tarred",                   await q.CountAsync(s => s.MainAccessRoadType == RoadSurfaceType.Tarred)),
                M("Main Road: Untarred",                 await q.CountAsync(s => s.MainAccessRoadType == RoadSurfaceType.Untarred)),
                M("Roads Impassable (Rainy Season)",    impassR, roads, "of roads surveyed"),
                M("Roads Impassable (Dry Season)",      impassD, roads, "of roads surveyed"),
            };

            // ── Finance ──────────────────────────────────────────────
            int fs    = await _db.FinancialServices.CountAsync(x => ids.Contains(x.SubmissionId));
            var fsTypes = await _db.FinancialServices
                .Where(x => ids.Contains(x.SubmissionId)).Select(x => x.Type).ToListAsync();
            int formal = fsTypes.Count(t =>
                t == FinancialServiceType.CommercialBank || t == FinancialServiceType.MicrofinanceBank);
            var financeMetrics = new List<MetricRow>
            {
                M("Financial Service Points",           fs, N, $"avg {(N>0?Math.Round((double)fs/N,2):0)}/community"),
                M("Formal Banking (Bank / MFB)",        formal, N),
                M("Rely on Informal Savings (Adashi)",  await q.CountAsync(s => s.RelyMoreOnInformalSavingsGroups)),
                M("Lost Money to POS Fraud",            await q.CountAsync(s => s.MembersLostMoneyToFailedPOSOrFraud)),
            };

            // ── Environment ──────────────────────────────────────────
            int flood  = await _db.EnvironmentalChallenges.CountAsync(x => ids.Contains(x.SubmissionId)
                             && x.ChallengeType == EnvironmentalChallengeType.Flooding
                             && x.Frequency     == OccurrenceFrequency.Often);
            int erosion = await _db.EnvironmentalChallenges.CountAsync(x => ids.Contains(x.SubmissionId)
                             && x.ChallengeType == EnvironmentalChallengeType.Erosion
                             && x.Frequency     == OccurrenceFrequency.Often);
            int drought = await _db.EnvironmentalChallenges.CountAsync(x => ids.Contains(x.SubmissionId)
                             && x.ChallengeType == EnvironmentalChallengeType.Drought
                             && x.Frequency     == OccurrenceFrequency.Often);
            double avgExt = N > 0
                ? Math.Round(await q.AverageAsync(s => (double)(s.NumberOfAgriculturalExtensionWorkers ?? 0)), 2)
                : 0;
            var environmentMetrics = new List<MetricRow>
            {
                M("Water: Borehole",                    await q.CountAsync(s => s.WaterSourceBorehole)),
                M("Water: Pipe-borne",                  await q.CountAsync(s => s.WaterSourcePipeBorne)),
                M("Water: River / Stream",              await q.CountAsync(s => s.WaterSourceRiverStream)),
                M("Irrigation Systems Present",         await q.CountAsync(s => s.IrrigationSystemsPresent)),
                M("Farmland Inaccessible (Insecurity)", await q.CountAsync(s => s.FarmlandInaccessibleDueToInsecurity)),
                M("Land Disputes with IDPs",            await q.CountAsync(s => s.LandDisputesBetweenIndigenesAndIDPs)),
                M("Flooding: Frequent",                 flood),
                M("Erosion: Frequent",                  erosion),
                M("Drought: Frequent",                  drought),
                new("Avg Extension Workers / Community",(int)Math.Round(avgExt), avgExt, 0),
            };

            // ── Security ─────────────────────────────────────────────
            int idpCamps = await _db.IDPCamps.CountAsync(x => ids.Contains(x.SubmissionId));
            int secInc   = await _db.SecurityIncidents.CountAsync(x => ids.Contains(x.SubmissionId));
            int idpsOut  = await q.SumAsync(s => s.EstimatedIDPsOutsideCamps ?? 0);
            var securityMetrics = new List<MetricRow>
            {
                M("Unsafe Communities",             await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.Unsafe)),
                M("Fairly Safe Communities",        await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.FairlySafe)),
                M("Safe Communities",               await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.Safe)),
                M("Farmer-Herder Conflict Issue",   await q.CountAsync(s => s.SecIssueFarmerHerder)),
                M("Banditry / Kidnapping Issue",    await q.CountAsync(s => s.SecIssueBanditry)),
                M("GBV Reported (Displacement)",    await q.CountAsync(s => s.WomenAndGirlsExposedToGBV)),
                M("Members Had to Flee",            await q.CountAsync(s => s.MembersHadToSleepInBushOrFlee)),
                M("Nearby Communities Destroyed",   await q.CountAsync(s => s.NearbyCommunitiesCompletelyDestroyed)),
                new("IDP Camps Recorded",           idpCamps, idpCamps, 0),
                new("Security Incidents Recorded",  secInc,   secInc,   0),
                new("Est. IDPs Outside Camps",      idpsOut,  idpsOut,  0),
                new("Security Programmes Recorded", await _db.SecurityProgrammes.CountAsync(x => ids.Contains(x.SubmissionId)), N, 0),
                new("Vulnerable Groups Recorded",   await _db.VulnerableGroups.CountAsync(x => ids.Contains(x.SubmissionId) && x.NumberOfPeople > 0), N, 0),
                new("Social Protections Recorded",  await _db.SocialProtections.CountAsync(x => ids.Contains(x.SubmissionId) && x.Available == true), N, 0),
                M("Power Supply: < 6 Hours",        await q.CountAsync(s => s.PublicPowerSupplyHours == PublicPowerSupplyHours.LessThan6)),
                M("Power Supply: 6-12 Hours",       await q.CountAsync(s => s.PublicPowerSupplyHours == PublicPowerSupplyHours.SixTo12)),
                M("Power Supply: > 12 Hours",       await q.CountAsync(s => s.PublicPowerSupplyHours == PublicPowerSupplyHours.MoreThan12)),
                M("Power Supply: None",             await q.CountAsync(s => s.PublicPowerSupplyHours == PublicPowerSupplyHours.None)),
                M("Elec: Public Power",             await q.CountAsync(s => s.ElecSourcePublicPower)),
                M("Elec: Generators",               await q.CountAsync(s => s.ElecSourceGenerators)),
                M("Elec: Solar Power",              await q.CountAsync(s => s.ElecSourceSolarPower)),
                new("NGOs Recorded",                await _db.NGOs.CountAsync(x => ids.Contains(x.SubmissionId)), N, 0),
                new("Migrant/Settler Activities",   await _db.MigrantSettlerActivities.CountAsync(x => ids.Contains(x.SubmissionId)), N, 0),
            };

            // ── Telecom ──────────────────────────────────────────────
            var telecomMetrics = new List<MetricRow>
            {
                M("Communication Black Spots",       await q.CountAsync(s => s.CommunicationBlackSpotsExist)),
                M("Internet: Mobile Data",           await q.CountAsync(s => s.InternetSourceMobileData)),
                M("Internet: Broadband / Fibre",     await q.CountAsync(s => s.InternetSourceBroadbandFibre)),
                M("MTN Coverage (Good/Fair)",        await CountNetworkCoverageAsync(ids, GSMProvider.MTN)),
                M("Airtel Coverage (Good/Fair)",     await CountNetworkCoverageAsync(ids, GSMProvider.Airtel)),
                M("Glo Coverage (Good/Fair)",        await CountNetworkCoverageAsync(ids, GSMProvider.Glo)),
                M("9Mobile Coverage (Good/Fair)",    await CountNetworkCoverageAsync(ids, GSMProvider.NineMobile)),
            };

            return new NumericalDashboard(
                scope, N, hh,
                healthMetrics, educationMetrics, marketMetrics,
                roadMetrics, financeMetrics, environmentMetrics,
                securityMetrics, telecomMetrics);
        }

        private Task<int> CountNetworkCoverageAsync(List<Guid> ids, GSMProvider provider) =>
            _db.GSMNetworks.CountAsync(n =>
                ids.Contains(n.SubmissionId) && n.Provider == provider &&
                (n.CoverageStrength == NetworkCoverage.Strong || n.CoverageStrength == NetworkCoverage.Fair));

        // ═══════════════════════════════════════════════════════════════
        // CROSS-TABULATIONS
        // ═══════════════════════════════════════════════════════════════

        public async Task<IReadOnlyList<CrossTabRow>> GetCrossTabAsync(string dimension, AnalyticsFilter filter)
        {
            return dimension switch
            {
                "health_facility_type" => await DetailCrossTabAsync(filter,
                    ids => _db.HealthFacilities
                        .Where(h => ids.Contains(h.SubmissionId) && h.Type != null)
                        .Select(h => h.Type.ToString()!)),
                "school_type" => await DetailCrossTabAsync(filter,
                    ids => _db.EducationalInstitutions
                        .Where(e => ids.Contains(e.SubmissionId) && e.Type != null)
                        .Select(e => e.Type.ToString()!)),
                "market_type" => await DetailCrossTabAsync(filter,
                    ids => _db.Markets
                        .Where(m => ids.Contains(m.SubmissionId) && m.Type != null)
                        .Select(m => m.Type.ToString()!)),
                "road_surface"           => await SubmissionCrossTabAsync(filter,
                    s => s.MainAccessRoadType != null,
                    s => s.MainAccessRoadType!.ToString()!),
                "security_situation"     => await SubmissionCrossTabAsync(filter,
                    s => s.GeneralSecuritySituation != null,
                    s => s.GeneralSecuritySituation!.ToString()!),
                "dispute_resolution_main"=> await SubmissionCrossTabAsync(filter,
                    s => s.MostCommonDisputeResolution != null,
                    s => s.MostCommonDisputeResolution!.ToString()!),
                "water_source"           => await GetWaterSourcesAsync(filter),
                "out_of_school"          => await GetOutOfSchoolCausesAsync(filter),
                "displacement"           => await GetDisplacementCausesAsync(filter),
                "security_issues"        => await GetSecurityIssuesAsync(filter),
                "dispute_methods"        => await GetDisputeResolutionAsync(filter),
                _                        => []
            };
        }

        public Task<IReadOnlyList<CrossTabRow>> GetOutOfSchoolCausesAsync(AnalyticsFilter f) =>
            BoolCrossTabListAsync(f,
            [
                ("Poverty",       s => s.OutOfSchoolDueToPoverty),
                ("Insecurity",    s => s.OutOfSchoolDueToInsecurity),
                ("Child Labour",  s => s.OutOfSchoolDueToChildLabour),
                ("Early Marriage",s => s.OutOfSchoolDueToEarlyMarriage),
                ("Distance",      s => s.OutOfSchoolDueToDistance),
                ("IDP-related",   s => s.OutOfSchoolDueToIDPRelated),
            ]);

        public Task<IReadOnlyList<CrossTabRow>> GetWaterSourcesAsync(AnalyticsFilter f) =>
            BoolCrossTabListAsync(f,
            [
                ("River / Stream", s => s.WaterSourceRiverStream),
                ("Borehole",       s => s.WaterSourceBorehole),
                ("Well",           s => s.WaterSourceWell),
                ("Rainwater",      s => s.WaterSourceRainwater),
                ("Pipe-borne",     s => s.WaterSourcePipeBorne),
            ]);

        public Task<IReadOnlyList<CrossTabRow>> GetDisplacementCausesAsync(AnalyticsFilter f) =>
            BoolCrossTabListAsync(f,
            [
                ("Farmer-Herder Crisis",  s => s.DisplacementCauseFarmerHerder),
                ("Armed Conflict",        s => s.DisplacementCauseArmedConflict),
                ("Flooding",              s => s.DisplacementCauseFlooding),
                ("Communal Violence",     s => s.DisplacementCauseCommunalViolence),
            ]);

        public Task<IReadOnlyList<CrossTabRow>> GetSecurityIssuesAsync(AnalyticsFilter f) =>
            BoolCrossTabListAsync(f,
            [
                ("Farmer-Herder Conflict",  s => s.SecIssueFarmerHerder),
                ("Communal Crisis",         s => s.SecIssueCommunalCrisis),
                ("Banditry / Kidnapping",   s => s.SecIssueBanditry),
                ("Tension w/ Sec. Ops",     s => s.SecIssueTensionWithOps),
                ("Theft",                   s => s.SecIssueTheft),
                ("Youth Restiveness",       s => s.SecIssueYouthRestiveness),
                ("Armed Robbery",           s => s.SecIssueArmedRobbery),
                ("Cultism",                 s => s.SecIssueCultism),
            ]);

        public Task<IReadOnlyList<CrossTabRow>> GetDisputeResolutionAsync(AnalyticsFilter f) =>
            BoolCrossTabListAsync(f,
            [
                ("Traditional Leaders", s => s.DispResTraditionalLeaders),
                ("Religious Leaders",   s => s.DispResReligiousLeaders),
                ("Local Government",    s => s.DispResLocalGovt),
                ("Courts",              s => s.DispResCourts),
                ("ADR",                 s => s.DispResADR),
            ]);

        public Task<IReadOnlyList<CrossTabRow>> GetCategoryBreakdownAsync(string category, AnalyticsFilter f) =>
            category switch
            {
                "health"      => BoolCrossTabListAsync(f,
                [
                    ("Functional Ambulance / Referral",  s => s.FunctionalAmbulanceOrReferral),
                    ("Childbirth Deaths (last 2 yrs)",   s => s.WomenDiedDuringChildbirthLast2Years),
                    ("Night Emergency Transport",        s => s.PregnantWomenCanAccessEmergencyTransportAtNight),
                ]),
                "education"   => GetOutOfSchoolCausesAsync(f),
                "markets"     => BoolCrossTabListAsync(f,
                [
                    ("Affected by Insecurity",           s => s.MarketActivitiesAffectedByInsecurity),
                    ("Traders from Outside Afraid",      s => s.TradersFromOutsideAfraid),
                    ("Pays Illegal Levy",                s => s.CommunityPaysIllegalLevy),
                ]),
                "roads"       => GetCrossTabAsync("road_surface", f),
                "finance"     => BoolCrossTabListAsync(f,
                [
                    ("Rely on Informal Savings",         s => s.RelyMoreOnInformalSavingsGroups),
                    ("Lost Money to POS / Fraud",        s => s.MembersLostMoneyToFailedPOSOrFraud),
                ]),
                "environment" => GetWaterSourcesAsync(f),
                "security"    => GetSecurityIssuesAsync(f),
                "telecom"     => BoolCrossTabListAsync(f,
                [
                    ("Mobile Internet",                  s => s.InternetSourceMobileData),
                    ("Broadband / Fibre",                s => s.InternetSourceBroadbandFibre),
                    ("Communication Black Spots",        s => s.CommunicationBlackSpotsExist),
                    ("Info via Radio",                   s => s.InfoChannelRadio),
                    ("Info via Social Media",            s => s.InfoChannelSocialMedia),
                    ("Info via Town Crier",              s => s.InfoChannelTownCrier),
                ]),
                "demographics"=> BoolCrossTabListAsync(f,
                [
                    ("Farmer-Herder Conflict",           s => s.AffectedByFarmerHerderConflict),
                    ("Host Community to IDPs",           s => s.IsHostCommunityToIDPs),
                    ("Farmland Inaccessible",            s => s.FarmlandInaccessibleDueToInsecurity),
                    ("Land Disputes with IDPs",          s => s.LandDisputesBetweenIndigenesAndIDPs),
                ]),
                _             => Task.FromResult<IReadOnlyList<CrossTabRow>>([])
            };

        // Cross-tab helpers
        private async Task<IReadOnlyList<CrossTabRow>> BoolCrossTabListAsync(
            AnalyticsFilter f,
            IEnumerable<(string Label, Func<QuestionnaireSubmission, bool> Pred)> items)
        {
            var all = await Approved(f).ToListAsync();
            int N = all.Count;
            if (N == 0) return [];
            return items
                .Select(i => { int c = all.Count(s => i.Pred(s)); return new CrossTabRow(i.Label, c, Pct(c, N)); })
                .OrderByDescending(r => r.Count)
                .ToList();
        }

        private async Task<IReadOnlyList<CrossTabRow>> DetailCrossTabAsync(
            AnalyticsFilter f, Func<List<Guid>, IQueryable<string>> selector)
        {
            var ids = await Approved(f).Select(s => s.Id).ToListAsync();
            if (ids.Count == 0) return [];
            var all = await selector(ids).ToListAsync();
            int T = all.Count;
            if (T == 0) return [];
            return all.GroupBy(v => v)
                .Select(g => new CrossTabRow(g.Key, g.Count(), Pct(g.Count(), T)))
                .OrderByDescending(r => r.Count).ToList();
        }

        private async Task<IReadOnlyList<CrossTabRow>> SubmissionCrossTabAsync(
            AnalyticsFilter f,
            Func<QuestionnaireSubmission, bool>   where,
            Func<QuestionnaireSubmission, string> key)
        {
            var all = await Approved(f).ToListAsync();
            int N = all.Count;
            if (N == 0) return [];
            return all.Where(where).GroupBy(key)
                .Select(g => new CrossTabRow(g.Key, g.Count(), Pct(g.Count(), N)))
                .OrderByDescending(r => r.Count).ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        // 2. TEXT ANALYSIS
        // ═══════════════════════════════════════════════════════════════

        public async Task<TextDashboard> GetTextDashboardAsync(AnalyticsFilter filter, int topN = 20)
        {
            var subs = await Approved(filter)
                .Include(s => s.PriorityNeeds)
                .Include(s => s.SecurityIncidents)
                .ToListAsync();

            int N = subs.Count;
            string scope = await ScopeNameAsync(filter);
            var analyses = new List<TextFieldAnalysis>();

            // All registered free-text fields
            foreach (var (key, label, get) in TextFieldRegistry.All)
            {
                var texts = subs.Select(s => get(s))
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
                analyses.Add(new(key, label, texts.Count, ExtractKeywords(texts, texts.Count, topN)));
            }

            // Priority needs — all ranks
            var pnAll = subs.SelectMany(s => s.PriorityNeeds)
                .Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            analyses.Add(new("priority_needs_all", "Priority Needs (all ranks)", pnAll.Count,
                ExtractKeywords(pnAll, Math.Max(pnAll.Count, 1), topN)));

            // Priority #1 only (most critical)
            var pn1 = subs.SelectMany(s => s.PriorityNeeds.Where(p => p.Rank == 1))
                .Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            analyses.Add(new("priority_needs_top", "Top Priority Need (#1 only)", pn1.Count,
                ExtractKeywords(pn1, Math.Max(pn1.Count, 1), topN)));

            // Security incidents
            var si = subs.SelectMany(s => s.SecurityIncidents)
                .SelectMany(i => new[] { i.Incident, i.Cause, i.HelpNeeded })
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
            analyses.Add(new("security_incidents", "Security Incidents", si.Count,
                ExtractKeywords(si, Math.Max(si.Count, 1), topN)));

            return new TextDashboard(scope, N, analyses);
        }

        public async Task<IReadOnlyList<KeywordFrequency>> GetKeywordsAsync(
            string fieldKey, AnalyticsFilter filter, int topN = 30)
        {
            var subs = await Approved(filter)
                .Include(s => s.PriorityNeeds)
                .Include(s => s.SecurityIncidents)
                .ToListAsync();

            if (fieldKey.StartsWith("priority_needs"))
            {
                bool topOnly = fieldKey == "priority_needs_top";
                var texts = subs
                    .SelectMany(s => topOnly
                        ? s.PriorityNeeds.Where(p => p.Rank == 1)
                        : s.PriorityNeeds)
                    .Select(p => p.Description)
                    .Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                return ExtractKeywords(texts, Math.Max(texts.Count, 1), topN);
            }
            if (fieldKey == "security_incidents")
            {
                var texts = subs.SelectMany(s => s.SecurityIncidents)
                    .SelectMany(i => new[] { i.Incident, i.Cause })
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
                return ExtractKeywords(texts, Math.Max(texts.Count, 1), topN);
            }

            var field = TextFieldRegistry.All.FirstOrDefault(x => x.Key == fieldKey);
            if (field == default) return [];
            var ft = subs.Select(s => field.Get(s))
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
            return ExtractKeywords(ft, Math.Max(ft.Count, 1), topN);
        }

        public async Task<IReadOnlyList<TextGroup>> GetTextGroupsAsync(
            string fieldKey, AnalyticsFilter filter, int minGroupSize = 2)
        {
            var subs = await Approved(filter)
                .Include(s => s.PriorityNeeds)
                .Include(s => s.SecurityIncidents)
                .ToListAsync();

            List<string> texts = fieldKey switch
            {
                "priority_needs_top" => subs.SelectMany(s => s.PriorityNeeds.Where(p => p.Rank == 1))
                    .Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList(),
                "priority_needs_all" => subs.SelectMany(s => s.PriorityNeeds)
                    .Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList(),
                "security_incidents" => subs.SelectMany(s => s.SecurityIncidents)
                    .SelectMany(i => new[] { i.Incident, i.Cause })
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList(),
                _ => (TextFieldRegistry.All.FirstOrDefault(x => x.Key == fieldKey) is var fd && fd != default)
                    ? subs.Select(s => fd.Get(s)).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList()
                    : []
            };

            if (texts.Count == 0) return [];

            var topKws  = ExtractKeywords(texts, texts.Count, 15);
            var groups  = new List<TextGroup>();
            var assigned = new HashSet<string>();

            foreach (var kw in topKws)
            {
                if (kw.Count < minGroupSize) break;
                var matching = texts
                    .Where(t => !assigned.Contains(t) && t.ToLower().Contains(kw.Word))
                    .ToList();
                if (matching.Count < minGroupSize) continue;

                foreach (var m in matching.SkipLast(1)) assigned.Add(m);

                groups.Add(new TextGroup(
                    kw.Word, matching.Count,
                    Math.Round((double)matching.Count / texts.Count * 100, 1),
                    matching.Take(5).Select(t => t.Length > 120 ? t[..120] + "…" : t).ToList()));
            }

            return groups.OrderByDescending(g => g.Count).ToList();
        }

        /// <summary>
        /// Keyword extraction algorithm:
        ///   Tokenise → lowercase → strip stop-words → count document frequency
        ///   (each word counted once per submission, not total occurrences).
        ///   Percentage = doc_freq / total_docs × 100
        /// </summary>
        private static IReadOnlyList<KeywordFrequency> ExtractKeywords(
            IReadOnlyList<string> texts, int N, int topN)
        {
            var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var text in texts)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var w in Regex.Split(text.ToLower(), @"[^a-z]+"))
                    if (w.Length >= 4 && !TextFieldRegistry.StopWords.Contains(w) && seen.Add(w))
                        freq[w] = freq.TryGetValue(w, out int c) ? c + 1 : 1;
            }
            return freq.OrderByDescending(kv => kv.Value).Take(topN)
                .Select(kv => new KeywordFrequency(kv.Key, kv.Value, Math.Round((double)kv.Value / N * 100, 1)))
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        // 3. FULL-TEXT SEARCH
        // ═══════════════════════════════════════════════════════════════

        public async Task<IReadOnlyList<TextSearchResult>> SearchTextAsync(
            string query, AnalyticsFilter filter, int maxResults = 100)
        {
            if (string.IsNullOrWhiteSpace(query)) return [];
            var term = query.Trim().ToLower();

            var subs = await Approved(filter)
                .Include(s => s.PriorityNeeds)
                .Include(s => s.SecurityIncidents)
                .ToListAsync();

            var results = new List<TextSearchResult>();

            foreach (var s in subs)
            {
                if (results.Count >= maxResults) break;

                string comm = s.Community?.Name ?? "–";
                string lga  = s.Community?.Kindred?.Ward?.LocalGovernmentArea?.Name ?? "–";
                string ward = s.Community?.Kindred?.Ward?.Name ?? "–";
                var at = s.SubmittedAt ?? s.UpdatedAt;

                void Hit(string label, string? val)
                {
                    if (!string.IsNullOrEmpty(val) && val.ToLower().Contains(term))
                        results.Add(new TextSearchResult(s.Id, comm, lga, ward, label, Snippet(val, term), at));
                }

                foreach (var (_, label, get) in TextFieldRegistry.All) Hit(label, get(s));
                foreach (var p in s.PriorityNeeds) Hit($"Priority Need #{p.Rank}", p.Description);
                foreach (var i in s.SecurityIncidents)
                {
                    Hit("Security Incident", i.Incident);
                    Hit("Incident Cause", i.Cause);
                    Hit("Help Needed (Security)", i.HelpNeeded);
                }
            }

            return results.Take(maxResults).ToList();
        }

        private static string Snippet(string text, string term)
        {
            int idx = text.ToLower().IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return text.Length > 120 ? text[..120] + "…" : text;
            int start = Math.Max(0, idx - 55);
            int end   = Math.Min(text.Length, idx + term.Length + 55);
            return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
        }

        // ═══════════════════════════════════════════════════════════════
        // 4. TIME-SERIES
        // ═══════════════════════════════════════════════════════════════

        public async Task<IReadOnlyList<TimeSeriesPoint>> GetSubmissionsTimeSeriesAsync(
            AnalyticsFilter filter, string groupBy = "month")
        {
            var dates = await Approved(filter)
                .Where(s => s.SubmittedAt != null)
                .Select(s => s.SubmittedAt!.Value)
                .ToListAsync();

            return dates.GroupBy(d => Period(d, groupBy))
                .Select(g => new TimeSeriesPoint(g.Key, g.Count()))
                .OrderBy(p => p.Period).ToList();
        }

        public async Task<IReadOnlyList<TimeSeriesPoint>> GetMetricTimeSeriesAsync(
            string metricKey, AnalyticsFilter filter, string groupBy = "month")
        {
            var items = await Approved(filter)
                .Where(s => s.SubmittedAt != null)
                .Select(s => new
                {
                    D = s.SubmittedAt!.Value,
                    V = (double)(metricKey == "households"             ? (s.EstimatedNumberOfHouseholds ?? 0)
                               : metricKey == "children_not_in_school" ? (s.ChildrenNotInSchool ?? 0)
                               : metricKey == "idps_outside_camps"     ? (s.EstimatedIDPsOutsideCamps ?? 0)
                               : 1)
                }).ToListAsync();

            return items.GroupBy(x => Period(x.D, groupBy))
                .Select(g => new TimeSeriesPoint(g.Key, g.Count(), Math.Round(g.Average(x => x.V), 1)))
                .OrderBy(p => p.Period).ToList();
        }

        private static string Period(DateTime d, string g) => g switch
        {
            "year"    => d.ToString("yyyy"),
            "quarter" => $"{d.Year}-Q{(d.Month - 1) / 3 + 1}",
            _         => d.ToString("yyyy-MM")
        };

        // ═══════════════════════════════════════════════════════════════
        // 5. GEOGRAPHIC SUMMARIES
        // ═══════════════════════════════════════════════════════════════

        public async Task<IReadOnlyList<GeoSummaryRow>> GetLGASummaryAsync(AnalyticsFilter filter)
        {
            var lgas = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            var rows = new List<GeoSummaryRow>();
            foreach (var lga in lgas)
            {
                var sf = filter with { LGAId = lga.Id, WardId = null, KindredId = null, CommunityId = null };
                int total    = await CountTotalAsync(sf);
                int approved = await CountApprovedAsync(sf);
                int comms    = await _db.Communities
                    .CountAsync(c => c.Kindred.Ward.LocalGovernmentAreaId == lga.Id && c.IsActive);
                rows.Add(new(lga.Id, lga.Name, lga.Code, total, approved,
                    comms > 0 ? Math.Round((double)approved / comms * 100, 1) : 0));
            }
            return rows;
        }

        public async Task<IReadOnlyList<GeoSummaryRow>> GetWardSummaryAsync(int lgaId, AnalyticsFilter filter)
        {
            var wards = await _db.Wards
                .Where(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
                .OrderBy(w => w.Name).ToListAsync();
            var rows = new List<GeoSummaryRow>();
            foreach (var w in wards)
            {
                var sf = filter with { WardId = w.Id, KindredId = null, CommunityId = null };
                int total    = await CountTotalAsync(sf);
                int approved = await CountApprovedAsync(sf);
                int comms    = await _db.Communities.CountAsync(c => c.Kindred.WardId == w.Id && c.IsActive);
                rows.Add(new(w.Id, w.Name, w.Code, total, approved,
                    comms > 0 ? Math.Round((double)approved / comms * 100, 1) : 0));
            }
            return rows;
        }

        public async Task<IReadOnlyList<GeoSummaryRow>> GetKindredSummaryAsync(int wardId, AnalyticsFilter filter)
        {
            var kindreds = await _db.Kindreds
                .Where(k => k.WardId == wardId && k.IsActive)
                .OrderBy(k => k.Name).ToListAsync();
            var rows = new List<GeoSummaryRow>();
            foreach (var k in kindreds)
            {
                var sf = filter with { KindredId = k.Id, CommunityId = null };
                int total    = await CountTotalAsync(sf);
                int approved = await CountApprovedAsync(sf);
                int comms    = await _db.Communities.CountAsync(c => c.KindredId == k.Id && c.IsActive);
                rows.Add(new(k.Id, k.Name, k.Code, total, approved,
                    comms > 0 ? Math.Round((double)approved / comms * 100, 1) : 0));
            }
            return rows;
        }

        public async Task<IReadOnlyList<GeoSummaryRow>> GetCommunitySummaryAsync(int kindredId)
        {
            var communities = await _db.Communities
                .Where(c => c.KindredId == kindredId && c.IsActive)
                .OrderBy(c => c.Name).ToListAsync();
            var rows = new List<GeoSummaryRow>();
            foreach (var c in communities)
            {
                int total    = await _db.Submissions.CountAsync(s => s.CommunityId == c.Id);
                int approved = await _db.Submissions.CountAsync(s => s.CommunityId == c.Id
                                   && s.Status == SubmissionStatus.ApprovedByAdmin);
                rows.Add(new(c.Id, c.Name, c.Code, total, approved,
                    total > 0 ? Math.Round((double)approved / total * 100, 1) : 0));
            }
            return rows;
        }

        // ═══════════════════════════════════════════════════════════════
        // 6. PRE-COMPUTED SNAPSHOTS
        //    Raw survey data is NEVER modified here.
        //    Only the AnalyticsSnapshot row is written/updated.
        // ═══════════════════════════════════════════════════════════════

        public async Task RefreshAllSnapshotsAsync()
        {
            await RefreshSnapshotAsync("System", 0);
            var lgaIds = await _db.LGAs.Where(l => l.IsActive).Select(l => l.Id).ToListAsync();
            foreach (var id in lgaIds)
                await RefreshSnapshotAsync("LGA", id);
        }

        public async Task RefreshSnapshotAsync(string scopeType, int scopeId)
        {
            var filter = scopeType switch
            {
                "LGA"       => new AnalyticsFilter { LGAId       = scopeId },
                "Ward"      => new AnalyticsFilter { WardId      = scopeId },
                "Kindred"   => new AnalyticsFilter { KindredId   = scopeId },
                "Community" => new AnalyticsFilter { CommunityId = scopeId },
                _           => new AnalyticsFilter()
            };

            string name = scopeType switch
            {
                "LGA"       => (await _db.LGAs.FindAsync(scopeId))?.Name       ?? "",
                "Ward"      => (await _db.Wards.FindAsync(scopeId))?.Name      ?? "",
                "Kindred"   => (await _db.Kindreds.FindAsync(scopeId))?.Name   ?? "",
                "Community" => (await _db.Communities.FindAsync(scopeId))?.Name?? "",
                _           => "System-wide"
            };

            var q    = Approved(filter);
            var ids  = await q.Select(s => s.Id).ToListAsync();
            int N    = ids.Count;
            int hh   = N > 0 ? await q.SumAsync(s => s.EstimatedNumberOfHouseholds ?? 0) : 0;
            int comms = scopeType == "System"
                ? await _db.Communities.CountAsync(c => c.IsActive)
                : scopeType == "LGA"
                  ? await _db.Communities.CountAsync(c => c.Kindred.Ward.LocalGovernmentAreaId == scopeId && c.IsActive)
                  : N;

            var subs = N > 0
                ? await q.Include(s => s.PriorityNeeds).Include(s => s.SecurityIncidents).ToListAsync()
                : [];

            // JSON helper for keyword columns
            static string KwJson(IEnumerable<KeywordFrequency> kws) =>
                JsonSerializer.Serialize(kws.Take(30).Select(k => new { word = k.Word, count = k.Count, pct = k.Percentage }));

            IReadOnlyList<KeywordFrequency> KwField(Func<QuestionnaireSubmission, string?> get)
            {
                var texts = subs.Select(s => get(s)).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
                return ExtractKeywords(texts, Math.Max(texts.Count, 1), 30);
            }

            // Fetch or create snapshot row
            var snap = await _db.AnalyticsSnapshots
                .FirstOrDefaultAsync(s => s.ScopeType == scopeType && s.ScopeId == scopeId
                                       && s.FromDate == null && s.ToDate == null)
                ?? new AnalyticsSnapshot { ScopeType = scopeType, ScopeId = scopeId };

            // Populate — raw data is untouched; only derived values go into snapshot
            snap.ScopeName            = name;
            snap.ComputedAt           = DateTime.UtcNow;
            snap.TotalSubmissions     = await All(filter).CountAsync();
            snap.ApprovedSubmissions  = N;
            snap.TotalCommunities     = comms;
            snap.CoverageRate         = comms > 0 ? Math.Round((double)N / comms * 100, 1) : 0;
            snap.TotalEstimatedHouseholds   = hh;
            snap.AvgHouseholdsPerCommunity  = N > 0 ? Math.Round((double)hh / N, 1) : 0;
            snap.CountFarmerHerderConflict  = N > 0 ? await q.CountAsync(s => s.AffectedByFarmerHerderConflict) : 0;
            snap.PctFarmerHerderConflict    = Pct(snap.CountFarmerHerderConflict, N);
            snap.CountHostCommunityToIDPs   = N > 0 ? await q.CountAsync(s => s.IsHostCommunityToIDPs) : 0;
            snap.PctHostCommunityToIDPs     = Pct(snap.CountHostCommunityToIDPs, N);
            snap.TotalIDPHouseholdsOutsideCamps = N > 0 ? await q.SumAsync(s => s.IDPHouseholdsOutsideCamps ?? 0) : 0;
            snap.TotalHealthFacilities      = N > 0 ? await _db.HealthFacilities.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.AvgHealthFacilitiesPerCommunity   = N > 0 ? Math.Round((double)snap.TotalHealthFacilities / N, 2) : 0;
            snap.HealthFacilitiesPer1000Households = hh > 0 ? Math.Round((double)snap.TotalHealthFacilities / hh * 1000, 2) : 0;
            snap.CountFunctionalAmbulance   = N > 0 ? await q.CountAsync(s => s.FunctionalAmbulanceOrReferral) : 0;
            snap.PctFunctionalAmbulance     = Pct(snap.CountFunctionalAmbulance, N);
            snap.CountChildbirthDeaths      = N > 0 ? await q.CountAsync(s => s.WomenDiedDuringChildbirthLast2Years) : 0;
            snap.PctChildbirthDeaths        = Pct(snap.CountChildbirthDeaths, N);
            snap.TotalSchools               = N > 0 ? await _db.EducationalInstitutions.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.AvgSchoolsPerCommunity     = N > 0 ? Math.Round((double)snap.TotalSchools / N, 2) : 0;
            snap.SchoolsPer1000Households   = hh > 0 ? Math.Round((double)snap.TotalSchools / hh * 1000, 2) : 0;
            snap.TotalChildrenNotInSchool   = N > 0 ? await q.SumAsync(s => s.ChildrenNotInSchool ?? 0) : 0;
            snap.AvgChildrenNotInSchoolPerCommunity = N > 0 ? Math.Round((double)snap.TotalChildrenNotInSchool / N, 1) : 0;
            snap.CountSchoolsDestroyedByConflict    = N > 0 ? await _db.EducationalInstitutions.CountAsync(x => ids.Contains(x.SubmissionId) && x.DestroyedOrClosedDueToConflict == true) : 0;
            snap.PctSchoolsDestroyedByConflict      = Pct(snap.CountSchoolsDestroyedByConflict, snap.TotalSchools);
            snap.TotalMarkets               = N > 0 ? await _db.Markets.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.AvgMarketsPerCommunity     = N > 0 ? Math.Round((double)snap.TotalMarkets / N, 2) : 0;
            snap.MarketsPer1000Households   = hh > 0 ? Math.Round((double)snap.TotalMarkets / hh * 1000, 2) : 0;
            snap.PctMarketsWithPoorInfrastructure = N > 0 ? Pct(await _db.Markets.CountAsync(x => ids.Contains(x.SubmissionId) && x.InfrastructureCondition == InfrastructureCondition.Poor), snap.TotalMarkets) : 0;
            snap.PctMarketAffectedByInsecurity    = Pct(N > 0 ? await q.CountAsync(s => s.MarketActivitiesAffectedByInsecurity) : 0, N);
            snap.PctWithBorehole            = Pct(N > 0 ? await q.CountAsync(s => s.WaterSourceBorehole) : 0, N);
            snap.PctWithPipedWater          = Pct(N > 0 ? await q.CountAsync(s => s.WaterSourcePipeBorne) : 0, N);
            snap.PctWithIrrigation          = Pct(N > 0 ? await q.CountAsync(s => s.IrrigationSystemsPresent) : 0, N);
            snap.PctFarmlandInaccessible    = Pct(N > 0 ? await q.CountAsync(s => s.FarmlandInaccessibleDueToInsecurity) : 0, N);
            snap.PctUnsafeCommunities       = Pct(N > 0 ? await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.Unsafe) : 0, N);
            snap.PctSafeCommunities         = Pct(N > 0 ? await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.Safe) : 0, N);
            snap.PctFarmerHerderSecurityIssue = Pct(N > 0 ? await q.CountAsync(s => s.SecIssueFarmerHerder) : 0, N);
            snap.PctGBVDueToDisplacement    = Pct(N > 0 ? await q.CountAsync(s => s.WomenAndGirlsExposedToGBV) : 0, N);
            snap.TotalIDPCamps              = N > 0 ? await _db.IDPCamps.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.TotalSecurityIncidents     = N > 0 ? await _db.SecurityIncidents.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.PctCommunicationBlackSpots = Pct(N > 0 ? await q.CountAsync(s => s.CommunicationBlackSpotsExist) : 0, N);
            snap.PctMobileInternet          = Pct(N > 0 ? await q.CountAsync(s => s.InternetSourceMobileData) : 0, N);
            snap.PctMTNCoverage             = N > 0 ? Pct(await CountNetworkCoverageAsync(ids, GSMProvider.MTN), N)    : 0;
            snap.PctAirtelCoverage          = N > 0 ? Pct(await CountNetworkCoverageAsync(ids, GSMProvider.Airtel), N) : 0;
            snap.PctGloCoverage             = N > 0 ? Pct(await CountNetworkCoverageAsync(ids, GSMProvider.Glo), N)    : 0;
            
            snap.PctElecPublicPower         = Pct(N > 0 ? await q.CountAsync(s => s.ElecSourcePublicPower) : 0, N);
            snap.PctElecGenerators          = Pct(N > 0 ? await q.CountAsync(s => s.ElecSourceGenerators) : 0, N);
            snap.PctElecSolarPower          = Pct(N > 0 ? await q.CountAsync(s => s.ElecSourceSolarPower) : 0, N);
            snap.PctElecOther               = Pct(N > 0 ? await q.CountAsync(s => s.ElecSourceOther) : 0, N);
            snap.TotalNGOs                  = N > 0 ? await _db.NGOs.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;
            snap.TotalMigrantSettlerActivities = N > 0 ? await _db.MigrantSettlerActivities.CountAsync(x => ids.Contains(x.SubmissionId)) : 0;

            // Text keyword JSON (stored in snapshot for fast dashboard reads)
            if (N > 0)
            {
                snap.KeywordsMarketChallenges    = KwJson(KwField(s => s.MarketChallenges));
                snap.KeywordsHealthDiseases      = KwJson(KwField(s => s.MajorDiseasesReported));
                snap.KeywordsEducationChallenges = KwJson(KwField(s => s.EducationKeyChallenges));
                snap.KeywordsTransportChallenges = KwJson(KwField(s => s.TransportChallenges));
                snap.KeywordsFinancialChallenges = KwJson(KwField(s => s.FinancialServicesChallenges));
                snap.KeywordsTelecomChallenges   = KwJson(KwField(s => s.TelecommunicationChallenges));
                snap.KeywordsSecurityIssues      = KwJson(KwField(s => s.OtherSecurityIssue));
                snap.KeywordsDisputeResolution   = KwJson(KwField(s => s.HowCommunityResolvesDisputes));

                var pnTexts = subs.SelectMany(s => s.PriorityNeeds)
                    .Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                snap.KeywordsPriorityNeeds = KwJson(ExtractKeywords(pnTexts, Math.Max(pnTexts.Count, 1), 30));

                var siTexts = subs.SelectMany(s => s.SecurityIncidents)
                    .SelectMany(i => new[] { i.Incident, i.Cause })
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
                snap.KeywordsSecurityIncidents = KwJson(ExtractKeywords(siTexts, Math.Max(siTexts.Count, 1), 30));
            }

            if (snap.Id == 0) _db.AnalyticsSnapshots.Add(snap);
            await _db.SaveChangesAsync();
        }

        public Task<AnalyticsSnapshot?> GetSnapshotAsync(string scopeType, int scopeId) =>
            _db.AnalyticsSnapshots.FirstOrDefaultAsync(s =>
                s.ScopeType == scopeType && s.ScopeId == scopeId &&
                s.FromDate == null && s.ToDate == null);

        // ═══════════════════════════════════════════════════════════════
        // 7. SCALARS
        // ═══════════════════════════════════════════════════════════════

        public Task<int> CountLGAsAsync()            => _db.LGAs.CountAsync(l => l.IsActive);
        public Task<int> CountWardsAsync(int? lgaId) => lgaId.HasValue
            ? _db.Wards.CountAsync(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
            : _db.Wards.CountAsync(w => w.IsActive);
        public Task<int> CountKindredsAsync(int? wardId) => wardId.HasValue
            ? _db.Kindreds.CountAsync(k => k.WardId == wardId && k.IsActive)
            : _db.Kindreds.CountAsync(k => k.IsActive);
        public Task<int> CountCommunitiesAsync(int? kindredId) => kindredId.HasValue
            ? _db.Communities.CountAsync(c => c.KindredId == kindredId && c.IsActive)
            : _db.Communities.CountAsync(c => c.IsActive);
        public Task<int> CountApprovedAsync(AnalyticsFilter f) => Approved(f).CountAsync();
        public Task<int> CountTotalAsync(AnalyticsFilter f)    => All(f).CountAsync();

        // ═══════════════════════════════════════════════════════════════
        // 8. DATA ANALYSIS PAGE
        // ═══════════════════════════════════════════════════════════════

        public async Task<IReadOnlyList<CommunityMetricRow>> GetCommunityMetricsAsync(AnalyticsFilter filter)
        {
            var subs = await Approved(filter)
                .Include(s => s.Community).ThenInclude(c => c.Kindred).ThenInclude(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea)
                .Include(s => s.HealthFacilities)
                .Include(s => s.EducationalInstitutions)
                .Include(s => s.Markets)
                .Include(s => s.FinancialServices)
                .Include(s => s.PriorityNeeds)
                .ToListAsync();

            return subs.Select(s => new CommunityMetricRow(
                s.CommunityId ?? 0,
                s.Community?.Name ?? "–",
                s.Community?.Kindred?.Name ?? "–",
                s.Community?.Kindred?.Ward?.Name ?? "–",
                s.Community?.Kindred?.Ward?.LocalGovernmentArea?.Name ?? "–",
                s.Community?.Code ?? "–",
                s.EstimatedNumberOfHouseholds ?? 0,
                s.HealthFacilities.Count,
                s.EducationalInstitutions.Count,
                s.ChildrenNotInSchool ?? 0,
                s.Markets.Count,
                s.FunctionalAmbulanceOrReferral,
                s.MainAccessRoadType == RoadSurfaceType.Tarred,
                s.FinancialServices.Any(fs =>
                    fs.Type == FinancialServiceType.CommercialBank ||
                    fs.Type == FinancialServiceType.MicrofinanceBank),
                s.WaterSourceBorehole,
                s.GeneralSecuritySituation?.ToString(),
                s.SecIssueFarmerHerder,
                s.PriorityNeeds.OrderBy(p => p.Rank).FirstOrDefault()?.Description
            )).ToList();
        }

        public async Task<IReadOnlyList<LGAComparisonRow>> GetLGAComparisonAsync(string metric, AnalyticsFilter filter)
        {
            var lgas = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            var rows = new List<LGAComparisonRow>();

            foreach (var lga in lgas)
            {
                var sf   = filter with { LGAId = lga.Id, WardId = null, KindredId = null, CommunityId = null };
                var q    = Approved(sf);
                var ids  = await q.Select(s => s.Id).ToListAsync();
                int N    = ids.Count;
                int comms = await _db.Communities
                    .CountAsync(c => c.Kindred.Ward.LocalGovernmentAreaId == lga.Id && c.IsActive);

                if (N == 0) { rows.Add(new(lga.Id, lga.Name, lga.Code, comms, N, 0, "0")); continue; }

                (double value, string label) = metric switch
                {
                    "health_facilities" => await CountAvg(_db.HealthFacilities.CountAsync(x => ids.Contains(x.SubmissionId)), N, "facility"),
                    "schools"           => await CountAvg(_db.EducationalInstitutions.CountAsync(x => ids.Contains(x.SubmissionId)), N, "school"),
                    "children_oos"      => await SumAvg(q.SumAsync(s => s.ChildrenNotInSchool ?? 0), N, "children OOS"),
                    "markets"           => await CountAvg(_db.Markets.CountAsync(x => ids.Contains(x.SubmissionId)), N, "market"),
                    "ambulance_pct"     => PctLabel(await q.CountAsync(s => s.FunctionalAmbulanceOrReferral), N),
                    "tarred_road_pct"   => PctLabel(await q.CountAsync(s => s.MainAccessRoadType == RoadSurfaceType.Tarred), N),
                    "formal_banking_pct"=> await FormalBankingPct(ids, N),
                    "borehole_pct"      => PctLabel(await q.CountAsync(s => s.WaterSourceBorehole), N),
                    "unsafe_pct"        => PctLabel(await q.CountAsync(s => s.GeneralSecuritySituation == SecuritySituation.Unsafe), N),
                    "farmer_herder_pct" => PctLabel(await q.CountAsync(s => s.SecIssueFarmerHerder), N),
                    "flooding_pct"      => PctLabel(await _db.EnvironmentalChallenges.CountAsync(x => ids.Contains(x.SubmissionId) && x.ChallengeType == EnvironmentalChallengeType.Flooding && x.Frequency == OccurrenceFrequency.Often), N),
                    "coverage_rate"     => (comms > 0 ? Math.Round((double)N / comms * 100, 1) : 0, $"{(comms>0?Math.Round((double)N/comms*100,1):0)}% ({N} of {comms} communities)"),
                    _                   => (0, "0")
                };

                rows.Add(new(lga.Id, lga.Name, lga.Code, comms, N, value, label));
            }

            return rows.OrderByDescending(r => r.Value).ToList();
        }

        // LGA comparison helpers
        private static async Task<(double, string)> CountAvg(Task<int> countTask, int N, string unit)
        {
            int c = await countTask;
            double avg = N > 0 ? Math.Round((double)c / N, 2) : 0;
            return (avg, $"{c} total (avg {avg}/{unit})");
        }
        private static async Task<(double, string)> SumAvg(Task<int> sumTask, int N, string unit)
        {
            int s = await sumTask;
            double avg = N > 0 ? Math.Round((double)s / N, 1) : 0;
            return (avg, $"{s} total (avg {avg}/community)");
        }
        private static (double, string) PctLabel(int count, int N)
        {
            double p = Pct(count, N);
            return (p, $"{p}%");
        }
        private async Task<(double, string)> FormalBankingPct(List<Guid> ids, int N)
        {
            var types = await _db.FinancialServices
                .Where(fs => ids.Contains(fs.SubmissionId)).Select(fs => fs.Type).ToListAsync();
            int formal = types.Count(t =>
                t == FinancialServiceType.CommercialBank || t == FinancialServiceType.MicrofinanceBank);
            return PctLabel(formal, N);
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static double Pct(int count, int N) =>
            N > 0 ? Math.Round((double)count / N * 100, 1) : 0;

        private async Task<string> ScopeNameAsync(AnalyticsFilter f)
        {
            if (f.CommunityId.HasValue) return (await _db.Communities.FindAsync(f.CommunityId))?.Name ?? "Community";
            if (f.KindredId.HasValue)   return (await _db.Kindreds.FindAsync(f.KindredId))?.Name ?? "Kindred";
            if (f.WardId.HasValue)      return (await _db.Wards.FindAsync(f.WardId))?.Name ?? "Ward";
            if (f.LGAId.HasValue)       return (await _db.LGAs.FindAsync(f.LGAId))?.Name ?? "LGA";
            string date = "";
            if (f.FromDate.HasValue || f.ToDate.HasValue)
                date = $" ({f.FromDate?.ToString("MMM yyyy") ?? "start"} – {f.ToDate?.ToString("MMM yyyy") ?? "now"})";
            return "System-wide" + date;
        }
    }
}
