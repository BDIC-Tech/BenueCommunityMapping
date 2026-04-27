using BenueCommunityMapping.Models.Survey;

namespace BenueCommunityMapping.Services.Analytics
{
    /// <summary>
    /// Analytics service interface.
    /// Implements the four data analysis pillars:
    ///   1. Numerical  – counts, percentages, per-1000-HH ratios
    ///   2. Text       – keyword extraction, frequency, grouping
    ///   3. Dashboards – filterable by location, date, category
    ///   4. Data handling – raw vs computed separation, fast queries, search
    /// </summary>
    public interface IAnalyticsService
    {
        // ── 1. Numerical ─────────────────────────────────────────────
        /// <summary>Full metric dashboard: Count + % + per-1000-HH ratio for every section.</summary>
        Task<NumericalDashboard> GetNumericalDashboardAsync(AnalyticsFilter filter);

        /// <summary>Cross-tabulation by named dimension (e.g. "health_facility_type", "road_surface").</summary>
        Task<IReadOnlyList<CrossTabRow>> GetCrossTabAsync(string dimension, AnalyticsFilter filter);

        Task<IReadOnlyList<CrossTabRow>> GetOutOfSchoolCausesAsync(AnalyticsFilter filter);
        Task<IReadOnlyList<CrossTabRow>> GetWaterSourcesAsync(AnalyticsFilter filter);
        Task<IReadOnlyList<CrossTabRow>> GetDisplacementCausesAsync(AnalyticsFilter filter);
        Task<IReadOnlyList<CrossTabRow>> GetSecurityIssuesAsync(AnalyticsFilter filter);
        Task<IReadOnlyList<CrossTabRow>> GetDisputeResolutionAsync(AnalyticsFilter filter);

        /// <summary>
        /// Category breakdown for dashboard filter pills.
        /// category: "health" | "education" | "markets" | "roads" |
        ///           "finance" | "environment" | "security" | "telecom" | "demographics"
        /// </summary>
        Task<IReadOnlyList<CrossTabRow>> GetCategoryBreakdownAsync(string category, AnalyticsFilter filter);

        // ── 2. Text analysis ─────────────────────────────────────────
        /// <summary>All text fields analysed: keyword frequency per field (topN keywords each).</summary>
        Task<TextDashboard> GetTextDashboardAsync(AnalyticsFilter filter, int topN = 20);

        /// <summary>Keyword frequency for a single named text field.</summary>
        Task<IReadOnlyList<KeywordFrequency>> GetKeywordsAsync(string fieldKey, AnalyticsFilter filter, int topN = 30);

        /// <summary>
        /// Groups text responses by dominant keyword theme.
        /// Implements "simple grouping of similar answers" — responses sharing a keyword
        /// are clustered together with representative sample quotes.
        /// </summary>
        Task<IReadOnlyList<TextGroup>> GetTextGroupsAsync(string fieldKey, AnalyticsFilter filter, int minGroupSize = 2);

        // ── 3. Full-text search ───────────────────────────────────────
        /// <summary>
        /// Full-text search across all 13 free-text fields, Priority Needs,
        /// and Security Incidents. Returns results with context snippets.
        /// </summary>
        Task<IReadOnlyList<TextSearchResult>> SearchTextAsync(string query, AnalyticsFilter filter, int maxResults = 100);

        // ── 4. Time-series ────────────────────────────────────────────
        /// <summary>Submission counts grouped by month / quarter / year.</summary>
        Task<IReadOnlyList<TimeSeriesPoint>> GetSubmissionsTimeSeriesAsync(AnalyticsFilter filter, string groupBy = "month");

        /// <summary>
        /// Average value of a numeric metric over time.
        /// metricKey: "households" | "children_not_in_school" | "idps_outside_camps"
        /// </summary>
        Task<IReadOnlyList<TimeSeriesPoint>> GetMetricTimeSeriesAsync(string metricKey, AnalyticsFilter filter, string groupBy = "month");

        // ── 5. Geographic summaries ───────────────────────────────────
        Task<IReadOnlyList<GeoSummaryRow>> GetLGASummaryAsync(AnalyticsFilter filter);
        Task<IReadOnlyList<GeoSummaryRow>> GetWardSummaryAsync(int lgaId, AnalyticsFilter filter);
        Task<IReadOnlyList<GeoSummaryRow>> GetKindredSummaryAsync(int wardId, AnalyticsFilter filter);
        Task<IReadOnlyList<GeoSummaryRow>> GetCommunitySummaryAsync(int kindredId);

        // ── 6. Pre-computed snapshots (raw data stays separate) ───────
        /// <summary>
        /// Materialises all statistics for the given scope into AnalyticsSnapshot table.
        /// Raw survey data is never modified — only the snapshot row is written.
        /// Called automatically on submission approval; also available on demand.
        /// </summary>
        Task RefreshSnapshotAsync(string scopeType, int scopeId);
        Task RefreshAllSnapshotsAsync();
        Task<AnalyticsSnapshot?> GetSnapshotAsync(string scopeType, int scopeId);

        // ── 7. Scalar counts ─────────────────────────────────────────
        Task<int> CountLGAsAsync();
        Task<int> CountWardsAsync(int? lgaId = null);
        Task<int> CountKindredsAsync(int? wardId = null);
        Task<int> CountCommunitiesAsync(int? kindredId = null);
        Task<int> CountApprovedAsync(AnalyticsFilter filter);
        Task<int> CountTotalAsync(AnalyticsFilter filter);

        // ── 8. Data Analysis page ─────────────────────────────────────
        /// <summary>One row per community with key metrics across all sections.</summary>
        Task<IReadOnlyList<CommunityMetricRow>> GetCommunityMetricsAsync(AnalyticsFilter filter);

        /// <summary>
        /// Side-by-side LGA comparison for one metric.
        /// metric: "health_facilities" | "schools" | "children_oos" | "markets" |
        ///         "ambulance_pct" | "tarred_road_pct" | "formal_banking_pct" |
        ///         "borehole_pct" | "unsafe_pct" | "farmer_herder_pct" |
        ///         "flooding_pct" | "coverage_rate"
        /// </summary>
        Task<IReadOnlyList<LGAComparisonRow>> GetLGAComparisonAsync(string metric, AnalyticsFilter filter);
    }
}
