using BenueCommunityMapping.Models;

namespace BenueCommunityMapping.Services.Analytics
{
    // ─────────────────────────────────────────────────────────────────────
    // PILLAR 1 — NUMERICAL DATA
    // Every metric is returned as Count + Percentage + Ratio so callers
    // always have all three forms without additional queries.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One analysed metric.
    /// Count      = absolute total
    /// Percentage = count / N × 100
    /// RatioPer1000HH = count / total_households × 1000
    /// </summary>
    public record MetricRow(
        string  Label,
        int     Count,
        double  Percentage,
        double  RatioPer1000HH,
        string? Note = null);

    /// <summary>Cross-tabulation row: one category, its count, and % share.</summary>
    public record CrossTabRow(string Category, int Count, double Percentage);

    /// <summary>Full numerical dashboard for one geographic scope.</summary>
    public record NumericalDashboard(
        string ScopeName,
        int    N,
        int    TotalHouseholds,
        IReadOnlyList<MetricRow> HealthMetrics,
        IReadOnlyList<MetricRow> EducationMetrics,
        IReadOnlyList<MetricRow> MarketMetrics,
        IReadOnlyList<MetricRow> RoadMetrics,
        IReadOnlyList<MetricRow> FinanceMetrics,
        IReadOnlyList<MetricRow> EnvironmentMetrics,
        IReadOnlyList<MetricRow> SecurityMetrics,
        IReadOnlyList<MetricRow> TelecomMetrics);

    // ─────────────────────────────────────────────────────────────────────
    // PILLAR 2 — TEXT DATA
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Keyword frequency for text analysis. Percentage = doc-freq / N × 100.</summary>
    public record KeywordFrequency(string Word, int Count, double Percentage);

    /// <summary>Text analysis result for a single field.</summary>
    public record TextFieldAnalysis(
        string FieldKey,
        string FieldLabel,
        int    ResponseCount,
        IReadOnlyList<KeywordFrequency> TopKeywords);

    /// <summary>Full text dashboard: all fields with keyword frequencies.</summary>
    public record TextDashboard(
        string ScopeName,
        int    N,
        IReadOnlyList<TextFieldAnalysis> Fields);

    /// <summary>
    /// Grouped text responses — responses sharing a dominant keyword are
    /// clustered into one theme group. Implements "simple grouping of similar answers".
    /// </summary>
    public record TextGroup(
        string                Theme,       // dominant keyword
        int                   Count,       // number of responses in group
        double                Percentage,  // % of all responses in the field
        IReadOnlyList<string> Samples);    // up to 5 representative quotes

    // ─────────────────────────────────────────────────────────────────────
    // PILLAR 3 — FULL-TEXT SEARCH
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>One full-text search hit with context snippet (±55 chars around match).</summary>
    public record TextSearchResult(
        Guid     SubmissionId,
        string   CommunityName,
        string   LGA,
        string   Ward,
        string   FieldName,
        string   Snippet,
        DateTime Date);

    // ─────────────────────────────────────────────────────────────────────
    // TIME-SERIES
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>One data point for a time-series chart.</summary>
    public record TimeSeriesPoint(string Period, int Count, double? AvgValue = null);

    // ─────────────────────────────────────────────────────────────────────
    // GEOGRAPHIC SUMMARIES
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Coverage summary row for LGA / Ward / Kindred / Community level.</summary>
    public record GeoSummaryRow(
        int    Id,
        string Name,
        string Code,
        int    TotalSubmissions,
        int    ApprovedSubmissions,
        double CoverageRate);

    // ─────────────────────────────────────────────────────────────────────
    // DATA ANALYSIS PAGE — comparison & community-level DTOs
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One community's key metrics across all sections.
    /// Powers the community comparison table on the data analysis page.
    /// </summary>
    public record CommunityMetricRow(
        int     CommunityId,
        string  CommunityName,
        string  KindredName,
        string  WardName,
        string  LGAName,
        string  CommunityCode,
        int     EstHouseholds,
        int     HealthFacilities,
        int     Schools,
        int     ChildrenNotInSchool,
        int     Markets,
        bool    FunctionalAmbulance,
        bool    TarredRoad,
        bool    FormalBanking,
        bool    Borehole,
        string? SecuritySituation,
        bool    FarmerHerderConflict,
        string? TopPriorityNeed);

    /// <summary>LGA side-by-side comparison row for a single selected metric.</summary>
    public record LGAComparisonRow(
        int    LGAId,
        string LGAName,
        string LGACode,
        int    Communities,
        int    Submissions,
        double Value,
        string ValueLabel);

    // ─────────────────────────────────────────────────────────────────────
    // FILTER — location + date window (passed to every service method)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Immutable filter record. All geographic levels are optional;
    /// the service applies the most specific non-null level.
    /// </summary>
    public record AnalyticsFilter
    {
        public int?      LGAId       { get; init; }
        public int?      WardId      { get; init; }
        public int?      KindredId   { get; init; }
        public int?      CommunityId { get; init; }
        public DateTime? FromDate    { get; init; }
        public DateTime? ToDate      { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEXT FIELD REGISTRY
    // Central list of all free-text submission fields available for analysis.
    // Adding a new field here automatically includes it in text analysis,
    // keyword extraction, full-text search, and snapshot generation.
    // ─────────────────────────────────────────────────────────────────────

    public static class TextFieldRegistry
    {
        /// <summary>
        /// (Key, Label, Getter) for every free-text field on QuestionnaireSubmission
        /// that should participate in text analysis and full-text search.
        /// </summary>
        public static readonly (string Key, string Label, Func<QuestionnaireSubmission, string?> Get)[] All =
        [
            ("market_challenges",    "Market Challenges",              s => s.MarketChallenges),
            ("diseases",             "Major Diseases Reported",        s => s.MajorDiseasesReported),
            ("education_challenges", "Education Key Challenges",       s => s.EducationKeyChallenges),
            ("transport_challenges", "Transport Challenges",           s => s.TransportChallenges),
            ("finance_challenges",   "Financial Services Challenges",  s => s.FinancialServicesChallenges),
            ("env_challenges",       "Natural Features Challenges",    s => s.NaturalFeaturesChallenges),
            ("telecom_challenges",   "Telecom Challenges",             s => s.TelecommunicationChallenges),
            ("security_other",       "Other Security Issues",          s => s.OtherSecurityIssue),
            ("dispute_text",         "How Community Resolves Disputes",s => s.HowCommunityResolvesDisputes),
            ("displacement_other",   "Other Displacement Causes",      s => s.OtherDisplacementCause),
            ("family_lineages",      "Major Family Lineages",          s => s.MajorFamilyLineages),
            ("religious_threats",    "Other Religious Threats",        s => s.OtherReligiousThreat),
            ("env_improvements",     "Urgent Environmental Improvements", s => s.OtherUrgentEnvImprovement),
        ];

        /// <summary>Stop-words excluded from keyword extraction.</summary>
        public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","a","an","and","or","in","of","to","is","are","we","our","for","with",
            "that","this","it","at","by","on","have","has","was","be","as","no","not",
            "more","need","very","most","from","but","some","also","been","they","their",
            "there","which","when","who","what","how","can","will","would","should","still",
            "community","communities","area","areas","lack","poor","good","bad","many","few",
            "due","often","always","never","than","then","these","those","other","others",
            "about","only","both","any","all","each","such","much","like","well","help",
            "make","made","does","did","do","into","through","during","before","after"
        };
    }
}
