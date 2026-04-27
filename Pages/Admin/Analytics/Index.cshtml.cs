using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services.Analytics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Pages.Admin.Analytics
{
    public class IndexModel : PageModel
    {
        private readonly IAnalyticsService _analytics;
        private readonly AppDbContext      _db;
        public IndexModel(IAnalyticsService analytics, AppDbContext db)
        { _analytics = analytics; _db = db; }

        public int?      SelectedLGAId     { get; private set; }
        public int?      SelectedWardId    { get; private set; }
        public int?      SelectedKindredId { get; private set; }
        public string    ActiveTab         { get; private set; } = "numerical";
        public string?   SearchQuery       { get; private set; }
        public string    GroupBy           { get; private set; } = "month";
        public DateTime? FromDate          { get; private set; }
        public DateTime? ToDate            { get; private set; }

        public List<LocalGovernmentArea> LGAs     { get; private set; } = [];
        public List<Ward>                Wards    { get; private set; } = [];
        public List<Kindred>             Kindreds { get; private set; } = [];

        public int LGACount { get; private set; }
        public int WardCount { get; private set; }
        public int KindredCount { get; private set; }
        public int CommunityCount { get; private set; }
        public int TotalCount { get; private set; }
        public int ApprovedCount { get; private set; }

        public NumericalDashboard? Numerical { get; private set; }
        public IReadOnlyList<CrossTabRow> SecuritySituations  { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> HealthFacilityTypes { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> SchoolTypes         { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> MarketTypes         { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> WaterSources        { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> OutOfSchoolCauses   { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> SecurityIssues      { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> DisputeMethods      { get; private set; } = [];
        public IReadOnlyList<CrossTabRow> DisplacementCauses  { get; private set; } = [];

        public TextDashboard? TextData { get; private set; }
        public IReadOnlyList<TextSearchResult> SearchResults  { get; private set; } = [];
        public IReadOnlyList<TimeSeriesPoint> SubmissionsSeries { get; private set; } = [];
        public IReadOnlyList<TimeSeriesPoint> HouseholdsSeries  { get; private set; } = [];
        public IReadOnlyList<TimeSeriesPoint> ChildrenOOSSeries { get; private set; } = [];
        public IReadOnlyList<GeoSummaryRow>   LGASummary      { get; private set; } = [];
        public IReadOnlyList<GeoSummaryRow>   WardSummary     { get; private set; } = [];
        public IReadOnlyList<GeoSummaryRow>   KindredSummary  { get; private set; } = [];

        public async Task OnGetAsync(
            int? lgaId, int? wardId, int? kindredId,
            string? tab, string? q, string? from, string? to, string? groupBy)
        {
            SelectedLGAId     = lgaId;
            SelectedWardId    = wardId;
            SelectedKindredId = kindredId;
            ActiveTab         = tab ?? "numerical";
            SearchQuery       = q;
            GroupBy           = groupBy ?? "month";
            FromDate          = DateTime.TryParse(from, out var fd) ? fd : null;
            ToDate            = DateTime.TryParse(to,   out var td) ? td : null;

            var filter = new AnalyticsFilter
            {
                LGAId = lgaId, WardId = wardId, KindredId = kindredId,
                FromDate = FromDate, ToDate = ToDate,
            };

            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            if (lgaId.HasValue)
                Wards = await _db.Wards
                    .Where(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
                    .OrderBy(w => w.Name).ToListAsync();
            if (wardId.HasValue)
                Kindreds = await _db.Kindreds
                    .Where(k => k.WardId == wardId && k.IsActive)
                    .OrderBy(k => k.Name).ToListAsync();

            LGACount       = await _analytics.CountLGAsAsync();
            WardCount      = await _analytics.CountWardsAsync(lgaId);
            KindredCount   = await _analytics.CountKindredsAsync(wardId);
            CommunityCount = await _analytics.CountCommunitiesAsync(kindredId);
            TotalCount     = await _analytics.CountTotalAsync(filter);
            ApprovedCount  = await _analytics.CountApprovedAsync(filter);
            LGASummary     = await _analytics.GetLGASummaryAsync(filter);
            if (lgaId.HasValue)   WardSummary    = await _analytics.GetWardSummaryAsync(lgaId.Value, filter);
            if (wardId.HasValue) KindredSummary  = await _analytics.GetKindredSummaryAsync(wardId.Value, filter);

            switch (ActiveTab)
            {
                case "numerical":
                    Numerical           = await _analytics.GetNumericalDashboardAsync(filter);
                    SecuritySituations  = await _analytics.GetCrossTabAsync("security_situation",   filter);
                    HealthFacilityTypes = await _analytics.GetCrossTabAsync("health_facility_type", filter);
                    SchoolTypes         = await _analytics.GetCrossTabAsync("school_type",          filter);
                    MarketTypes         = await _analytics.GetCrossTabAsync("market_type",          filter);
                    WaterSources        = await _analytics.GetWaterSourcesAsync(filter);
                    OutOfSchoolCauses   = await _analytics.GetOutOfSchoolCausesAsync(filter);
                    SecurityIssues      = await _analytics.GetSecurityIssuesAsync(filter);
                    DisputeMethods      = await _analytics.GetDisputeResolutionAsync(filter);
                    DisplacementCauses  = await _analytics.GetDisplacementCausesAsync(filter);
                    break;
                case "text":
                    TextData = await _analytics.GetTextDashboardAsync(filter, topN: 20);
                    break;
                case "search":
                    if (!string.IsNullOrWhiteSpace(q))
                        SearchResults = await _analytics.SearchTextAsync(q.Trim(), filter);
                    break;
                case "timeseries":
                    SubmissionsSeries = await _analytics.GetSubmissionsTimeSeriesAsync(filter, GroupBy);
                    HouseholdsSeries  = await _analytics.GetMetricTimeSeriesAsync("households",             filter, GroupBy);
                    ChildrenOOSSeries = await _analytics.GetMetricTimeSeriesAsync("children_not_in_school", filter, GroupBy);
                    break;
            }
        }
    }
}
