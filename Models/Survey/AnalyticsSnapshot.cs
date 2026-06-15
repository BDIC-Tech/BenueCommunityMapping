using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenueCommunityMapping.Models.Survey
{
    /// <summary>
    /// Pre-computed analytics snapshot.
    /// 
    /// PURPOSE: Separates raw survey data from derived statistics.
    /// Dashboard queries hit this table (fast, indexed) rather than
    /// re-aggregating thousands of submission rows on every page load.
    /// 
    /// Refresh trigger: called when a submission is approved, or on demand.
    /// Scope: one row per (ScopeType + ScopeId) combination.
    ///   ScopeType = "System" | "LGA" | "Ward" | "Kindred" | "Community"
    ///   ScopeId   = the integer ID of the geographic entity (0 for System)
    /// </summary>
    public class AnalyticsSnapshot
    {
        [Key]
        public int Id { get; set; }

        // ── Scope ──────────────────────────────────────────────────────
        [Required, MaxLength(20)]
        public string ScopeType { get; set; } = "System"; // System|LGA|Ward|Kindred|Community

        public int ScopeId { get; set; } = 0;             // 0 for system-wide

        [MaxLength(200)]
        public string ScopeName { get; set; } = string.Empty;

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

        // Optional date-range window this snapshot covers (null = all time)
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate   { get; set; }

        // ── COVERAGE ──────────────────────────────────────────────────
        public int TotalSubmissions    { get; set; }
        public int ApprovedSubmissions { get; set; }
        public int TotalCommunities    { get; set; }
        /// <summary>ApprovedSubmissions / TotalCommunities × 100</summary>
        public double CoverageRate     { get; set; }

        // ── SECTION A — DEMOGRAPHICS ───────────────────────────────────
        /// <summary>Sum of EstimatedNumberOfHouseholds across approved submissions</summary>
        public int    TotalEstimatedHouseholds    { get; set; }
        /// <summary>Average households per community</summary>
        public double AvgHouseholdsPerCommunity   { get; set; }
        /// <summary>Count of submissions with AffectedByFarmerHerderConflict = true</summary>
        public int    CountFarmerHerderConflict    { get; set; }
        /// <summary>% communities affected by farmer-herder conflict</summary>
        public double PctFarmerHerderConflict      { get; set; }
        public int    CountHostCommunityToIDPs     { get; set; }
        public double PctHostCommunityToIDPs       { get; set; }
        public int    TotalIDPHouseholdsOutsideCamps { get; set; }

        // ── SECTION B — MARKETS ────────────────────────────────────────
        public int    TotalMarkets                        { get; set; }
        /// <summary>Average markets per community (ratio)</summary>
        public double AvgMarketsPerCommunity              { get; set; }
        /// <summary>Markets per 1000 households (ratio)</summary>
        public double MarketsPer1000Households            { get; set; }
        public int    CountMarketsWithPoorInfrastructure  { get; set; }
        public double PctMarketsWithPoorInfrastructure    { get; set; }
        public int    CountMarketAffectedByInsecurity     { get; set; }
        public double PctMarketAffectedByInsecurity       { get; set; }
        public int    CountCommunityPaysIllegalLevy       { get; set; }
        public double PctCommunityPaysIllegalLevy         { get; set; }

        // ── SECTION C — HEALTH ─────────────────────────────────────────
        public int    TotalHealthFacilities               { get; set; }
        public double AvgHealthFacilitiesPerCommunity     { get; set; }
        /// <summary>Health facilities per 1000 households</summary>
        public double HealthFacilitiesPer1000Households   { get; set; }
        public int    TotalOtherHealthFacilities          { get; set; }
        public int    CountFunctionalAmbulance            { get; set; }
        public double PctFunctionalAmbulance              { get; set; }
        public int    CountChildbirthDeaths               { get; set; }
        public double PctChildbirthDeaths                 { get; set; }
        public int    CountNightEmergencyTransport        { get; set; }
        public double PctNightEmergencyTransport          { get; set; }

        // ── SECTION D — EDUCATION ──────────────────────────────────────
        public int    TotalSchools                        { get; set; }
        public double AvgSchoolsPerCommunity              { get; set; }
        /// <summary>Schools per 1000 households</summary>
        public double SchoolsPer1000Households            { get; set; }
        public int    TotalChildrenNotInSchool            { get; set; }
        public double AvgChildrenNotInSchoolPerCommunity  { get; set; }
        public int    CountSchoolsDestroyedByConflict     { get; set; }
        public double PctSchoolsDestroyedByConflict       { get; set; }
        public int    CountCommunitiesHostingIDPs         { get; set; }
        public double PctCommunitiesHostingIDPs           { get; set; }
        // Out-of-school causes
        public double PctOutOfSchoolPoverty               { get; set; }
        public double PctOutOfSchoolInsecurity            { get; set; }
        public double PctOutOfSchoolChildLabour           { get; set; }
        public double PctOutOfSchoolEarlyMarriage         { get; set; }
        public double PctOutOfSchoolDistance              { get; set; }

        // ── SECTION E — ROADS ──────────────────────────────────────────
        public int    TotalAccessRoads                    { get; set; }
        public double PctTarredMainRoad                   { get; set; }
        public double PctUntarredMainRoad                 { get; set; }
        public double PctImpassableRainy                  { get; set; }
        public double PctImpassableDry                    { get; set; }

        // ── SECTION F — FINANCE ────────────────────────────────────────
        public int    TotalFinancialServices              { get; set; }
        public double AvgFinancialServicesPerCommunity    { get; set; }
        public double PctWithFormalBanking                { get; set; }
        public double PctRelyOnInformalSavings            { get; set; }
        public double PctLostMoneyToFraud                 { get; set; }

        // ── SECTION G — ENVIRONMENT ────────────────────────────────────
        public double PctWithBorehole                     { get; set; }
        public double PctWithPipedWater                   { get; set; }
        public double PctWithIrrigation                   { get; set; }
        public double PctFarmlandInaccessible             { get; set; }
        public double PctLandDisputesWithIDPs             { get; set; }
        public double PctFloodingOften                    { get; set; }
        public double PctErosionOften                     { get; set; }
        public double PctAirPollutionOften                { get; set; }
        public double PctDroughtOften                     { get; set; }
        public double AvgExtensionWorkers                 { get; set; }

        // ── SECTION H — RELIGION ───────────────────────────────────────
        public double PctReligiousTensionsCausedConflict  { get; set; }

        // ── SECTION I — TELECOM ────────────────────────────────────────
        public double PctMTNCoverage                      { get; set; }
        public double PctAirtelCoverage                   { get; set; }
        public double PctGloCoverage                      { get; set; }
        public double PctMobileInternet                   { get; set; }
        public double PctBroadbandInternet                { get; set; }
        public double PctCommunicationBlackSpots          { get; set; }

        // ── SECTION J — SECURITY ───────────────────────────────────────
        public double PctUnsafeCommunities                { get; set; }
        public double PctFairlySafeCommunities            { get; set; }
        public double PctSafeCommunities                  { get; set; }
        public double PctFarmerHerderSecurityIssue        { get; set; }
        public double PctBanditryIssue                    { get; set; }
        public double PctGBVDueToDisplacement             { get; set; }
        public double PctMembersHadToFlee                 { get; set; }
        public double PctNearbyCommunitiesDestroyed       { get; set; }
        public int    TotalIDPCamps                       { get; set; }
        public int    TotalSecurityIncidents              { get; set; }
        public int    TotalIDPsOutsideCamps               { get; set; }

        public double PctElecPublicPower                  { get; set; }
        public double PctElecGenerators                   { get; set; }
        public double PctElecSolarPower                   { get; set; }
        public double PctElecOther                        { get; set; }
        public int    TotalNGOs                           { get; set; }
        public int    TotalMigrantSettlerActivities       { get; set; }

        // ── TEXT ANALYSIS — JSON frequency arrays ──────────────────────
        // Each stores top-N keywords as JSON: [{"word":"water","count":42,"pct":31.2}, ...]
        [Column(TypeName = "TEXT")]
        public string? KeywordsMarketChallenges { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsHealthDiseases { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsEducationChallenges { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsTransportChallenges { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsFinancialChallenges { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsNaturalFeatures { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsTelecomChallenges { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsSecurityIssues { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsDisputeResolution { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsDisplacementCauses { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsPriorityNeeds { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsSecurityIncidents { get; set; }

        [Column(TypeName = "TEXT")]
        public string? KeywordsAllChallenges { get; set; }
        // [MaxLength(4000)] public string? KeywordsMarketChallenges          { get; set; }
        // [MaxLength(4000)] public string? KeywordsHealthDiseases            { get; set; }
        // [MaxLength(4000)] public string? KeywordsEducationChallenges       { get; set; }
        // [MaxLength(4000)] public string? KeywordsTransportChallenges       { get; set; }
        // [MaxLength(4000)] public string? KeywordsFinancialChallenges       { get; set; }
        // [MaxLength(4000)] public string? KeywordsNaturalFeatures           { get; set; }
        // [MaxLength(4000)] public string? KeywordsTelecomChallenges         { get; set; }
        // [MaxLength(4000)] public string? KeywordsSecurityIssues            { get; set; }
        // [MaxLength(4000)] public string? KeywordsDisputeResolution         { get; set; }
        // [MaxLength(4000)] public string? KeywordsDisplacementCauses        { get; set; }
        // [MaxLength(8000)] public string? KeywordsPriorityNeeds             { get; set; }
        // [MaxLength(4000)] public string? KeywordsSecurityIncidents         { get; set; }
        // [MaxLength(8000)] public string? KeywordsAllChallenges             { get; set; } // union
    }
}
