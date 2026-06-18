using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Models.Survey;
using System.ComponentModel.DataAnnotations;

namespace BenueCommunityMapping.Models
{
    public enum SubmissionStatus
    {
        Draft, Submitted, ReviewedByCoordinator, ApprovedByAdmin, Rejected
    }

    /// <summary>
    /// Root submission.  Geographic FK:  CommunityId → Community → Kindred → Ward → LGA.
    /// All 1-to-many rows are separate relational tables for statistical analysis.
    /// </summary>
    public class QuestionnaireSubmission
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();

        public SubmissionStatus Status   { get; set; } = SubmissionStatus.Draft;
        public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt        { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt     { get; set; }

        [MaxLength(1000)] public string? CoordinatorNotes { get; set; }
        [MaxLength(1000)] public string? AdminNotes       { get; set; }

        // ── Ownership ─────────────────────────────────────────────────
        [Required] public string AgentId      { get; set; } = string.Empty;
        public ApplicationUser Agent          { get; set; } = null!;
        public string? CoordinatorId          { get; set; }
        public ApplicationUser? Coordinator   { get; set; }

        // ── GEOGRAPHIC LINK ────────────────────────────────────────────
        public int? CommunityId                    { get; set; }
        public Community? Community                { get; set; }

        /// <summary>Free-text community name entered on questionnaire (optional).</summary>
        [MaxLength(300)]
        public string? CommunityName               { get; set; }

        // ══════ SECTION A — scalars captured at survey time ═══════════
        [Range(0, int.MaxValue)] public int? EstimatedNumberOfHouseholds { get; set; }
        public bool AffectedByFarmerHerderConflict  { get; set; }
        public int? ConflictStartYear               { get; set; }
        public bool IsHostCommunityToIDPs           { get; set; }
        [Range(0, int.MaxValue)] public int? IDPHouseholdsOutsideCamps { get; set; }
        [MaxLength(1000)] public string? MajorFamilyLineages { get; set; }
        [MaxLength(1000)] public string? MajorEthnicGroups { get; set; }

        // ══════ SECTION B — market scalars ════════════════════════════
        [MaxLength(1000)] public string? MarketChallenges                { get; set; }
        public bool MarketActivitiesAffectedByInsecurity                 { get; set; }
        public bool TradersFromOutsideAfraid                             { get; set; }
        public bool CommunityPaysIllegalLevy                             { get; set; }

        // ══════ SECTION C — healthcare scalars ════════════════════════
        public bool FunctionalAmbulanceOrReferral                        { get; set; }
        [MaxLength(1000)] public string? MajorDiseasesReported           { get; set; }
        public bool WomenDiedDuringChildbirthLast2Years                         { get; set; }
        [Range(0, int.MaxValue)] public int? WomenDiedDuringChildbirthLast2YearsCount       { get; set; }
        public bool PregnantWomenDiedBeforeChildbirthLast2Years              { get; set; }
        [Range(0, int.MaxValue)] public int? PregnantWomenDiedBeforeChildbirthLast2YearsCount { get; set; }
        public bool PregnantWomenCanAccessEmergencyTransportAtNight      { get; set; }
        public bool ChildrenUnder5DiedLast2Years                         { get; set; }
        [Range(0, int.MaxValue)] public int? ChildrenUnder5DiedLast2YearsCount { get; set; }
        [MaxLength(500)] public string? NearestHealthFacilityIfNone      { get; set; }

        // ══════ SECTION D — education scalars ═════════════════════════
        [MaxLength(1000)] public string? EducationKeyChallenges          { get; set; }
        [MaxLength(500)]  public string? NearestInstitutionIfNone        { get; set; }
        [Range(0, int.MaxValue)] public int? ChildrenNotInSchool         { get; set; }
        public bool OutOfSchoolDueToPoverty                              { get; set; }
        public bool OutOfSchoolDueToInsecurity                           { get; set; }
        public bool OutOfSchoolDueToChildLabour                          { get; set; }
        public bool OutOfSchoolDueToEarlyMarriage                        { get; set; }
        public bool OutOfSchoolDueToDistance                             { get; set; }
        public bool OutOfSchoolDueToIDPRelated                           { get; set; }
        public bool SchoolsCurrentlyHostingIDPs                          { get; set; }

        // ══════ SECTION E — roads scalars ═════════════════════════════
        public RoadSurfaceType? MainAccessRoadType                       { get; set; }
        public bool RainSeasonMotorcycle { get; set; } public bool RainSeasonCarBus  { get; set; }
        public bool RainSeasonTruck      { get; set; } public bool RainSeasonWalking { get; set; }
        public bool RainSeasonCanoeBoat  { get; set; }
        public bool DrySeasonMotorcycle  { get; set; } public bool DrySeasonCarBus   { get; set; }
        public bool DrySeasonTruck       { get; set; } public bool DrySeasonWalking  { get; set; }
        public bool DrySeasonCanoeBoat   { get; set; }
        [MaxLength(1000)] public string? TransportChallenges             { get; set; }

        // ══════ SECTION F — finance scalars ═══════════════════════════
        [MaxLength(1000)] public string? FinancialServicesChallenges     { get; set; }
        public bool RelyMoreOnInformalSavingsGroups                      { get; set; }
        public bool MembersLostMoneyToFailedPOSOrFraud                   { get; set; }

        // ══════ SECTION G — environment scalars ═══════════════════════
        [MaxLength(1000)] public string? NaturalFeaturesChallenges       { get; set; }
        public bool FarmingSubsistence    { get; set; } public bool FarmingCommercial         { get; set; }
        public bool FarmingBoth           { get; set; }
        public bool DomLandResidential    { get; set; } public bool DomLandAgricultural       { get; set; }
        public bool DomLandCommercial     { get; set; } public bool DomLandIndustrial         { get; set; }
        public bool WaterSourceRiverStream { get; set; } public bool WaterSourceBorehole      { get; set; }
        public bool WaterSourceWell        { get; set; } public bool WaterSourceRainwater     { get; set; }
        public bool WaterSourcePipeBorne   { get; set; }
        public bool IrrigationSystemsPresent                             { get; set; }
        [Range(0, int.MaxValue)] public int? NumberOfAgriculturalExtensionWorkers { get; set; }
        [MaxLength(500)] public string? ExtensionWorkerServices          { get; set; }
        public bool FarmlandInaccessibleDueToInsecurity                  { get; set; }
        public FarmlandAbandonmentPercent? PercentFarmlandAbandoned      { get; set; }
        public bool LandDisputesBetweenIndigenesAndIDPs                  { get; set; }
        public bool AccessToTractorsOrMechanizedFarming                  { get; set; }
        public GeneralRating? GeneralEnvironmentalCondition              { get; set; }
        public bool UrgentWasteManagement { get; set; } public bool UrgentDrainage      { get; set; }
        public bool UrgentTreePlanting    { get; set; } public bool UrgentFloodControl  { get; set; }
        public bool UrgentPollutionControl { get; set; }
        [MaxLength(300)] public string? OtherUrgentEnvImprovement        { get; set; }

        // ══════ SECTION H — religion scalars ══════════════════════════
        public bool IntoleranceThreatensReligion            { get; set; }
        public bool ExtremismThreatensReligion              { get; set; }
        public bool DiscriminationThreatensReligion         { get; set; }
        public bool CrisisThreatensReligion                 { get; set; }
        public bool PoliticalInterferenceThreatensReligion  { get; set; }
        public bool NoThreatToReligion                      { get; set; }
        [MaxLength(300)] public string? OtherReligiousThreat { get; set; }
        public bool ReligiousOrEthnicTensionsCausedConflict { get; set; }

        // ══════ SECTION I — telecom scalars ═══════════════════════════
        public bool InternetSourceMobileData    { get; set; } public bool InternetSourceBroadbandFibre { get; set; }
        public bool InternetSourceSatellite     { get; set; } public bool CommunicationBlackSpotsExist { get; set; }
        public bool InfoChannelPhoneCallsSMS    { get; set; } public bool InfoChannelTelevision        { get; set; }
        public bool InfoChannelRadio            { get; set; } public bool InfoChannelTownCrier         { get; set; }
        public bool InfoChannelReligiousCentres { get; set; } public bool InfoChannelCommunityMeetings { get; set; }
        public bool InfoChannelSocialMedia      { get; set; }
        [MaxLength(1000)] public string? TelecommunicationChallenges     { get; set; }

        // ══════ SECTION J — security scalars ══════════════════════════
        public SecuritySituation? GeneralSecuritySituation               { get; set; }
        public bool SecIssueNone              { get; set; } public bool SecIssueFarmerHerder  { get; set; }
        public bool SecIssueCommunalCrisis    { get; set; } public bool SecIssueBanditry      { get; set; }
        public bool SecIssueTensionWithOps    { get; set; } public bool SecIssueTheft         { get; set; }
        public bool SecIssueYouthRestiveness  { get; set; } public bool SecIssueArmedRobbery  { get; set; }
        public bool SecIssueCultism           { get; set; }
        [MaxLength(300)] public string? OtherSecurityIssue               { get; set; }
        public bool DispResTraditionalLeaders  { get; set; } public bool DispResReligiousLeaders { get; set; }
        public bool DispResLocalGovt           { get; set; } public bool DispResCourts           { get; set; }
        public bool DispResADR                 { get; set; }
        [MaxLength(200)] public string? OtherDisputeResolution           { get; set; }
        public DisputeResolutionMethod? MostCommonDisputeResolution       { get; set; }
        public bool MembersHadToSleepInBushOrFlee                        { get; set; }
        public bool NearbyCommunitiesCompletelyDestroyed                 { get; set; }
        public bool WomenAndGirlsExposedToGBV                            { get; set; }
        [Range(0, int.MaxValue)] public int? EstimatedIDPsOutsideCamps   { get; set; }
        public bool DisplacementCauseFarmerHerder  { get; set; } public bool DisplacementCauseArmedConflict { get; set; }
        public bool DisplacementCauseFlooding      { get; set; } public bool DisplacementCauseCommunalViolence { get; set; }
        [MaxLength(300)] public string? OtherDisplacementCause           { get; set; }
        [MaxLength(500)] public string? HowCommunityResolvesDisputes     { get; set; }

        // ══════ NAVIGATION — relational detail rows ════════════════════
        public ICollection<Market>                Markets                 { get; set; } = new List<Market>();
        public ICollection<HealthFacility>        HealthFacilities        { get; set; } = new List<HealthFacility>();
        public ICollection<OtherHealthFacility>   OtherHealthFacilities   { get; set; } = new List<OtherHealthFacility>();
        public ICollection<EducationalInstitution>EducationalInstitutions { get; set; } = new List<EducationalInstitution>();
        public ICollection<AccessRoad>            AccessRoads             { get; set; } = new List<AccessRoad>();
        public ICollection<FinancialService>      FinancialServices       { get; set; } = new List<FinancialService>();
        public ICollection<NaturalFeature>        NaturalFeatures         { get; set; } = new List<NaturalFeature>();
        public ICollection<IndustrialActivity>    IndustrialActivities    { get; set; } = new List<IndustrialActivity>();
        public ICollection<MiningActivity>        MiningActivities        { get; set; } = new List<MiningActivity>();
        public ICollection<EnvironmentalChallenge>EnvironmentalChallenges { get; set; } = new List<EnvironmentalChallenge>();
        public ICollection<ReligiousGroup>        ReligiousGroups         { get; set; } = new List<ReligiousGroup>();
        public ICollection<GSMNetwork>            GSMNetworks             { get; set; } = new List<GSMNetwork>();
        public ICollection<SecurityService>       SecurityServices        { get; set; } = new List<SecurityService>();
        public ICollection<VulnerableGroup>       VulnerableGroups        { get; set; } = new List<VulnerableGroup>();
        public ICollection<SocialProtection>      SocialProtections       { get; set; } = new List<SocialProtection>();
        public ICollection<SecurityProgramme>     SecurityProgrammes      { get; set; } = new List<SecurityProgramme>();
        public ICollection<SecurityIncident>      SecurityIncidents       { get; set; } = new List<SecurityIncident>();
        public ICollection<IDPCamp>               IDPCamps                { get; set; } = new List<IDPCamp>();
        public ICollection<MigrantSettlerActivity>MigrantSettlerActivities{ get; set; } = new List<MigrantSettlerActivity>();
        public ICollection<NGO>                   NGOs                    { get; set; } = new List<NGO>();

        // Q17 & Q18: Electricity sources
        public PublicPowerSupplyHours? PublicPowerSupplyHours { get; set; }
        public bool ElecSourcePublicPower { get; set; }
        public bool ElecSourceGenerators { get; set; }
        public bool ElecSourceSolarPower { get; set; }
        public bool ElecSourceOther { get; set; }
        [MaxLength(300)]
        public string? ElecSourceOtherSpecify { get; set; }

        public ICollection<PriorityNeed>          PriorityNeeds           { get; set; } = new List<PriorityNeed>();
        public ICollection<ConsentSignatory>      ConsentSignatories      { get; set; } = new List<ConsentSignatory>();
        public ICollection<QuestionnaireWorkflowHistory> WorkflowHistory  { get; set; } = new List<QuestionnaireWorkflowHistory>();
    }
}
