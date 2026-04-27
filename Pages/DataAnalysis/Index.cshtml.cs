using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Pages.DataAnalysis
{
    /// <summary>
    /// Dedicated data analysis page accessible to Admin and Coordinator roles.
    /// Implements all four analysis pillars:
    ///   1. Numerical  – counts, percentages, per-1000-HH ratios
    ///   2. Text       – keyword extraction, frequency, grouping of similar answers
    ///   3. Dashboards – filterable by location, date, category
    ///   4. Data handling – raw vs computed separation, fast queries, full-text search
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IAnalyticsService _analytics;
        private readonly AppDbContext      _db;

        public IndexModel(IAnalyticsService analytics, AppDbContext db)
        { _analytics = analytics; _db = db; }

        // ── Filter state ──────────────────────────────────────────────
        public int?      SelectedLGAId     { get; private set; }
        public int?      SelectedWardId    { get; private set; }
        public int?      SelectedKindredId { get; private set; }
        public int?      SelectedCommunityId { get; private set; }
        public string    ActiveTab         { get; private set; } = "overview";
        public string    ActiveCategory    { get; private set; } = "all";
        public string?   SearchQuery       { get; private set; }
        public string?   TextFieldKey      { get; private set; }
        public string    CompareMetric     { get; private set; } = "health_facilities";
        public string    GroupBy           { get; private set; } = "month";
        public DateTime? FromDate          { get; private set; }
        public DateTime? ToDate            { get; private set; }

        // ── Dropdown data ─────────────────────────────────────────────
        public List<LocalGovernmentArea> LGAs       { get; private set; } = [];
        public List<Ward>                Wards      { get; private set; } = [];
        public List<Kindred>             Kindreds   { get; private set; } = [];
        public List<Community>           Communities{ get; private set; } = [];

        // ── Scalar summary ────────────────────────────────────────────
        public int LGACount       { get; private set; }
        public int WardCount      { get; private set; }
        public int KindredCount   { get; private set; }
        public int CommunityCount { get; private set; }
        public int TotalCount     { get; private set; }
        public int ApprovedCount  { get; private set; }
        public string ScopeName   { get; private set; } = "System-wide";

        // ── Tab data ──────────────────────────────────────────────────
        // Overview tab
        public NumericalDashboard?           Overview          { get; private set; }
        public IReadOnlyList<CrossTabRow>    CategoryBreakdown { get; private set; } = [];

        // Numerical tab
        public NumericalDashboard?           Numerical         { get; private set; }
        public IReadOnlyList<CrossTabRow>    CrossTab1         { get; private set; } = [];
        public IReadOnlyList<CrossTabRow>    CrossTab2         { get; private set; } = [];

        // Text tab
        public TextDashboard?                TextData          { get; private set; }
        public IReadOnlyList<TextGroup>      TextGroups        { get; private set; } = [];
        public string?                       GroupedFieldLabel { get; private set; }

        // Search tab
        public IReadOnlyList<TextSearchResult> SearchResults   { get; private set; } = [];

        // Compare tab
        public IReadOnlyList<LGAComparisonRow>    LGAComparison     { get; private set; } = [];
        public IReadOnlyList<CommunityMetricRow>  CommunityMetrics  { get; private set; } = [];

        // Time-series tab
        public IReadOnlyList<TimeSeriesPoint> SubmissionsSeries { get; private set; } = [];
        public IReadOnlyList<TimeSeriesPoint> HouseholdsSeries  { get; private set; } = [];
        public IReadOnlyList<TimeSeriesPoint> ChildrenOOSSeries { get; private set; } = [];

        // Geographic summary (always loaded)
        public IReadOnlyList<GeoSummaryRow>   LGASummary        { get; private set; } = [];
        public IReadOnlyList<GeoSummaryRow>   WardSummary       { get; private set; } = [];
        public IReadOnlyList<GeoSummaryRow>   KindredSummary    { get; private set; } = [];

        public static readonly (string Key, string Label)[] TextFieldOptions =
        [
            ("market_challenges",    "Market Challenges"),
            ("diseases",             "Major Diseases Reported"),
            ("education_challenges", "Education Key Challenges"),
            ("transport_challenges", "Transport Challenges"),
            ("finance_challenges",   "Financial Services Challenges"),
            ("env_challenges",       "Natural Features Challenges"),
            ("telecom_challenges",   "Telecom Challenges"),
            ("security_other",       "Other Security Issues"),
            ("dispute_text",         "How Community Resolves Disputes"),
            ("displacement_other",   "Other Displacement Causes"),
            ("family_lineages",      "Major Family Lineages"),
            ("religious_threats",    "Other Religious Threats"),
            ("env_improvements",     "Urgent Env. Improvements"),
            ("priority_needs_all",   "All Priority Needs"),
            ("priority_needs_top",   "Top Priority Need (#1 only)"),
            ("security_incidents",   "Security Incidents"),
        ];

        public static readonly (string Key, string Label)[] CompareMetrics =
        [
            ("health_facilities",  "Health Facilities (avg/community)"),
            ("schools",            "Schools (avg/community)"),
            ("children_oos",       "Children Not in School (avg/community)"),
            ("markets",            "Markets (avg/community)"),
            ("ambulance_pct",      "% with Functional Ambulance"),
            ("tarred_road_pct",    "% with Tarred Road"),
            ("formal_banking_pct", "% with Formal Banking"),
            ("borehole_pct",       "% with Borehole Water"),
            ("unsafe_pct",         "% Unsafe Communities"),
            ("farmer_herder_pct",  "% Farmer-Herder Conflict"),
            ("flooding_pct",       "% Frequent Flooding"),
            ("coverage_rate",      "Survey Coverage Rate %"),
        ];

        public static readonly (string Key, string Label)[] Categories =
        [
            ("all",          "All Sections"),
            ("demographics", "A: Demographics & IDPs"),
            ("markets",      "B: Markets"),
            ("health",       "C: Health"),
            ("education",    "D: Education"),
            ("roads",        "E: Roads"),
            ("finance",      "F: Finance"),
            ("environment",  "G: Environment"),
            ("security",     "J: Security"),
            ("telecom",      "I: Telecom"),
        ];

        public async Task OnGetAsync(
            int? lgaId, int? wardId, int? kindredId, int? communityId,
            string? tab, string? category, string? q, string? fieldKey,
            string? compareMetric, string? from, string? to, string? groupBy)
        {
            SelectedLGAId      = lgaId;
            SelectedWardId     = wardId;
            SelectedKindredId  = kindredId;
            SelectedCommunityId= communityId;
            ActiveTab          = tab      ?? "overview";
            ActiveCategory     = category ?? "all";
            SearchQuery        = q;
            TextFieldKey       = fieldKey ?? "priority_needs_top";
            CompareMetric      = compareMetric ?? "health_facilities";
            GroupBy            = groupBy  ?? "month";
            FromDate           = DateTime.TryParse(from, out var fd) ? fd : null;
            ToDate             = DateTime.TryParse(to,   out var td) ? td : null;

            var filter = new AnalyticsFilter
            {
                LGAId       = lgaId,
                WardId      = wardId,
                KindredId   = kindredId,
                CommunityId = communityId,
                FromDate    = FromDate,
                ToDate      = ToDate,
            };

            // Dropdown population (cascading)
            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            if (lgaId.HasValue)
                Wards = await _db.Wards
                    .Where(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
                    .OrderBy(w => w.Name).ToListAsync();
            if (wardId.HasValue)
                Kindreds = await _db.Kindreds
                    .Where(k => k.WardId == wardId && k.IsActive)
                    .OrderBy(k => k.Name).ToListAsync();
            if (kindredId.HasValue)
                Communities = await _db.Communities
                    .Where(c => c.KindredId == kindredId && c.IsActive)
                    .OrderBy(c => c.Name).ToListAsync();

            // Scalars – always loaded
            LGACount       = await _analytics.CountLGAsAsync();
            WardCount      = await _analytics.CountWardsAsync(lgaId);
            KindredCount   = await _analytics.CountKindredsAsync(wardId);
            CommunityCount = await _analytics.CountCommunitiesAsync(kindredId);
            TotalCount     = await _analytics.CountTotalAsync(filter);
            ApprovedCount  = await _analytics.CountApprovedAsync(filter);

            // Scope label
            ScopeName = communityId.HasValue ? (await _db.Communities.FindAsync(communityId))?.Name ?? "Community"
                : kindredId.HasValue  ? (await _db.Kindreds.FindAsync(kindredId))?.Name ?? "Kindred"
                : wardId.HasValue     ? (await _db.Wards.FindAsync(wardId))?.Name ?? "Ward"
                : lgaId.HasValue      ? (await _db.LGAs.FindAsync(lgaId))?.Name ?? "LGA"
                : "System-wide";
            if (FromDate.HasValue || ToDate.HasValue)
                ScopeName += $" · {FromDate?.ToString("MMM yyyy") ?? "start"} – {ToDate?.ToString("MMM yyyy") ?? "now"}";

            // Geographic summaries
            LGASummary = await _analytics.GetLGASummaryAsync(filter);
            if (lgaId.HasValue)   WardSummary    = await _analytics.GetWardSummaryAsync(lgaId.Value, filter);
            if (wardId.HasValue) KindredSummary  = await _analytics.GetKindredSummaryAsync(wardId.Value, filter);

            // Tab-specific data
            switch (ActiveTab)
            {
                case "overview":
                    Overview = await _analytics.GetNumericalDashboardAsync(filter);
                    if (ActiveCategory != "all")
                        CategoryBreakdown = await _analytics.GetCategoryBreakdownAsync(ActiveCategory, filter);
                    break;

                case "numerical":
                    Numerical = await _analytics.GetNumericalDashboardAsync(filter);
                    // Category-specific cross-tabs (explicit assignments avoid tuple-await compiler issues)
                    if (ActiveCategory == "health")
                    {
                        CrossTab1 = await _analytics.GetCrossTabAsync("health_facility_type", filter);
                        CrossTab2 = await _analytics.GetCategoryBreakdownAsync("health", filter);
                    }
                    else if (ActiveCategory == "education")
                    {
                        CrossTab1 = await _analytics.GetOutOfSchoolCausesAsync(filter);
                        CrossTab2 = await _analytics.GetCrossTabAsync("school_type", filter);
                    }
                    else if (ActiveCategory == "markets")
                    {
                        CrossTab1 = await _analytics.GetCrossTabAsync("market_type", filter);
                        CrossTab2 = await _analytics.GetCategoryBreakdownAsync("markets", filter);
                    }
                    else if (ActiveCategory == "environment")
                    {
                        CrossTab1 = await _analytics.GetWaterSourcesAsync(filter);
                        CrossTab2 = await _analytics.GetDisplacementCausesAsync(filter);
                    }
                    else if (ActiveCategory == "security")
                    {
                        CrossTab1 = await _analytics.GetSecurityIssuesAsync(filter);
                        CrossTab2 = await _analytics.GetDisputeResolutionAsync(filter);
                    }
                    else if (ActiveCategory == "roads")
                    {
                        CrossTab1 = await _analytics.GetCrossTabAsync("road_surface", filter);
                    }
                    else if (ActiveCategory == "telecom")
                    {
                        CrossTab1 = await _analytics.GetCategoryBreakdownAsync("telecom", filter);
                    }
                    else
                    {
                        CrossTab1 = await _analytics.GetCrossTabAsync("security_situation", filter);
                        CrossTab2 = await _analytics.GetWaterSourcesAsync(filter);
                    }
                    break;

                case "text":
                    TextData    = await _analytics.GetTextDashboardAsync(filter, topN: 25);
                    TextGroups  = await _analytics.GetTextGroupsAsync(TextFieldKey, filter, minGroupSize: 2);
                    GroupedFieldLabel = TextFieldOptions.FirstOrDefault(o => o.Key == TextFieldKey).Label
                                     ?? TextFieldKey;
                    break;

                case "search":
                    if (!string.IsNullOrWhiteSpace(q))
                        SearchResults = await _analytics.SearchTextAsync(q.Trim(), filter, maxResults: 200);
                    break;

                case "compare":
                    LGAComparison    = await _analytics.GetLGAComparisonAsync(CompareMetric, filter);
                    if (lgaId.HasValue || wardId.HasValue || kindredId.HasValue)
                        CommunityMetrics = await _analytics.GetCommunityMetricsAsync(filter);
                    break;

                case "timeseries":
                    SubmissionsSeries = await _analytics.GetSubmissionsTimeSeriesAsync(filter, GroupBy);
                    HouseholdsSeries  = await _analytics.GetMetricTimeSeriesAsync("households",            filter, GroupBy);
                    ChildrenOOSSeries = await _analytics.GetMetricTimeSeriesAsync("children_not_in_school",filter, GroupBy);
                    break;
            }
        }

        // POST: Trigger snapshot refresh
        public async Task<IActionResult> OnPostRefreshSnapshotAsync()
        {
            await _analytics.RefreshAllSnapshotsAsync();
            TempData["Success"] = "Analytics snapshots refreshed successfully.";
            return RedirectToPage(new { tab = "overview" });
        }
    }
}
