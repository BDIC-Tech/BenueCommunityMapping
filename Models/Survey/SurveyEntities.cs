using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ══════════════════════════════════════════════════════════════════════
//  SURVEY ENTITY MODELS
//  All detail rows from every questionnaire section are proper EF tables.
//  Foreign key chain:
//    Community → QuestionnaireSubmission → Section[X] rows
//
//  This enables queries like:
//    "Average health facilities per community per LGA"
//    "% markets with poor infrastructure by Ward"
//    "Count of communities with no school by Kindred"
// ══════════════════════════════════════════════════════════════════════

namespace BenueCommunityMapping.Models.Survey
{
    // ──────────────────────────────────────────────────────────────────
    // SECTION A — COMMUNITY IDENTIFICATION (scalar fields on Submission)
    // ──────────────────────────────────────────────────────────────────
    // Section A scalars live directly on QuestionnaireSubmission as flat
    // columns (not a separate table) because each submission has exactly
    // one community, so there is no 1-to-many relationship to normalise.

    // ──────────────────────────────────────────────────────────────────
    // SECTION B — MARKETS
    // ──────────────────────────────────────────────────────────────────
    public class Market
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name        { get; set; }
        [MaxLength(300)] public string? Location    { get; set; }
        [Range(0, double.MaxValue)] public double? SizeSquareMeters { get; set; }
        public MarketType?           Type                        { get; set; }
        [MaxLength(500)] public string? StorageFacilities        { get; set; }
        [MaxLength(500)] public string? MainGoodsSold            { get; set; }
        [MaxLength(500)] public string? FarmProduceSold          { get; set; }
        public FunctionalStatus?     MarketStatus                { get; set; }
        public InfrastructureCondition? InfrastructureCondition  { get; set; }
        [MaxLength(200)] public string? MostActiveTimeOfYear     { get; set; }
        public bool? WomenAndYouthMajorParticipants              { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION C — HEALTH FACILITIES
    // ──────────────────────────────────────────────────────────────────
    public class HealthFacility
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name     { get; set; }
        [MaxLength(300)] public string? Location { get; set; }
        public HealthFacilityType?       Type                        { get; set; }
        public DistanceCategory?         DistanceFromCentre          { get; set; }
        public StaffAvailability?        HealthcareStaffAvailability { get; set; }
        public InfrastructureCondition?  InfrastructureCondition     { get; set; }
        public ServiceDeliveryCondition? ServiceDeliveryCondition    { get; set; }
        [MaxLength(300)] public string?  WhoBuilt                    { get; set; }
        public int?                      YearEstablished             { get; set; }
        public int?                      YearLastRenovated           { get; set; }
        public WorkQualityRating?        InfrastructureWorkQuality   { get; set; }
        public bool?                     IDPsAllowedWithoutDiscrimination { get; set; }
        public DrugAvailability?         EssentialDrugsAvailability  { get; set; }
    }

    public class OtherHealthFacility
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name      { get; set; }
        [MaxLength(300)] public string? Location  { get; set; }
        public OtherFacilityType?        Type                     { get; set; }
        public DistanceCategory?         DistanceFromCentre       { get; set; }
        public StaffAvailability?        StaffAvailability        { get; set; }
        public InfrastructureCondition?  InfrastructureCondition  { get; set; }
        public ServiceDeliveryCondition? ServiceDeliveryCondition { get; set; }
        [MaxLength(300)] public string?  WhoBuilt                 { get; set; }
        public int?                      YearEstablished          { get; set; }
        public int?                      YearLastRenovated        { get; set; }
        public WorkQualityRating?        InfrastructureWorkQuality { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION D — EDUCATIONAL INSTITUTIONS
    // ──────────────────────────────────────────────────────────────────
    public class EducationalInstitution
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name      { get; set; }
        [MaxLength(300)] public string? Location  { get; set; }
        public EducationLevel?           Type                       { get; set; }
        [MaxLength(200)] public string?  Owner                      { get; set; }
        public DistanceCategory?         DistanceFromCentre         { get; set; }
        public StaffAvailability?        TeacherAvailability        { get; set; }
        public InfrastructureCondition?  InfrastructureCondition    { get; set; }
        public ServiceDeliveryCondition? ServiceDeliveryQuality     { get; set; }
        [MaxLength(300)] public string?  WhoBuilt                   { get; set; }
        public int?                      YearEstablished            { get; set; }
        public int?                      YearLastRenovated          { get; set; }
        public WorkQualityRating?        InfrastructureWorkQuality  { get; set; }
        public bool?                     DestroyedOrClosedDueToConflict { get; set; }
        public int?                      ConflictClosureYear        { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION E — ACCESS ROADS
    // ──────────────────────────────────────────────────────────────────
    public class AccessRoad
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(300)] public string?     RoadName                   { get; set; }
        public RoadPassability?             RainySeasonCondition       { get; set; }
        public RoadPassability?             DrySeasonCondition         { get; set; }
        public DrainagePresence?            DrainageSystem             { get; set; }
        [MaxLength(300)] public string?     WhoBuilt                   { get; set; }
        public int?                         YearConstructed            { get; set; }
        public int?                         YearLastRenovated          { get; set; }
        public WorkQualityRating?           InfrastructureWorkQuality  { get; set; }
        [Range(0, 12)] public int?          MonthsAccessVeryDifficult  { get; set; }
        public RoadDangerLevel?             DangersFromBadAccess       { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION F — FINANCIAL SERVICES
    // ──────────────────────────────────────────────────────────────────
    public class FinancialService
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string?  Name                      { get; set; }
        [MaxLength(300)] public string?  Location                  { get; set; }
        public FinancialServiceType?     Type                      { get; set; }
        public bool?                     OffersLoansOrCredit        { get; set; }
        public DistanceCategory?         DistanceFromCentre        { get; set; }
        public bool?                     CommunityFindsBeneficial  { get; set; }
        public bool?                     WomenAndYouthHaveEqualAccess { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION G — ENVIRONMENT & AGRICULTURE
    // ──────────────────────────────────────────────────────────────────
    public class NaturalFeature
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name              { get; set; }
        public NaturalFeatureType?      Type              { get; set; }
        [MaxLength(300)] public string? Location          { get; set; }
        [MaxLength(300)] public string? SupervisorManager { get; set; }
        [MaxLength(500)] public string? CommunityUse      { get; set; }
    }

    public class IndustrialActivity
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public IndustrialActivityType? ActivityType            { get; set; }
        [MaxLength(300)] public string? Location               { get; set; }
        [MaxLength(200)] public string? Owner                  { get; set; }
        [MaxLength(300)] public string? FinishedProducts       { get; set; }
        [MaxLength(300)] public string? Byproducts             { get; set; }
        [MaxLength(300)] public string? RawMaterials           { get; set; }
        [MaxLength(300)] public string? RawMaterialsSourcedFrom { get; set; }
        public bool?                    ProductsSoldWithinCommunity { get; set; }
        [MaxLength(500)] public string? CommunityBenefits     { get; set; }
    }

    public class MiningActivity
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? MineralBeingMined       { get; set; }
        [MaxLength(300)] public string? Location                { get; set; }
        [MaxLength(200)] public string? Owner                   { get; set; }
        [MaxLength(300)] public string? InputMaterials          { get; set; }
        [MaxLength(300)] public string? InputMaterialsSourcedFrom { get; set; }
        public bool?                    ProductsSoldWithinCommunity { get; set; }
        [MaxLength(500)] public string? CommunityBenefits       { get; set; }
        [MaxLength(500)] public string? NegativeImpacts         { get; set; }
    }

    /// <summary>
    /// One row per environmental challenge type per submission.
    /// Supports queries like "frequency of flooding across all communities in LGA X".
    /// </summary>
    public class EnvironmentalChallenge
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public EnvironmentalChallengeType ChallengeType        { get; set; }
        public OccurrenceFrequency?       Frequency            { get; set; }
        [MaxLength(200)] public string?   TimeOfYear           { get; set; }
        [MaxLength(500)] public string?   AreasAffected        { get; set; }
        public SeverityLevel?             Severity             { get; set; }
        public int?                       YearStarted          { get; set; }
        [MaxLength(500)] public string?   MostAffected         { get; set; }
        public bool?                      InterventionsCarriedOut { get; set; }
        public int?                       YearOfIntervention   { get; set; }
        [MaxLength(300)] public string?   WhoIntervened        { get; set; }
        public bool?                      InterventionsHelped  { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION H — RELIGION
    // ──────────────────────────────────────────────────────────────────
    public class ReligiousGroup
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public ReligiousWorshipCentreType Type                      { get; set; }
        [Range(0, int.MaxValue)] public int? NumberExisting          { get; set; }
        [Range(0, int.MaxValue)] public int? EstimatedMembershipPopulation { get; set; }
        public bool ContributesToEducation                          { get; set; }
        public bool ContributesToHealthServices                     { get; set; }
        public bool ContributesToPeaceBuilding                      { get; set; }
        public bool ContributesToCharitySocialWelfare               { get; set; }
        public bool ContributesToMoralGuidance                      { get; set; }
        public bool ContributesToRoads                              { get; set; }
        public bool ContributesToWater                              { get; set; }
        public bool ContributesToElectricity                        { get; set; }
        public bool? LeadersActivelyParticipateInPeaceBuilding      { get; set; }
        [Range(0, int.MaxValue)] public int? NumberNoLongerInUseOrDestroyed { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION I — TELECOMMUNICATIONS
    // ──────────────────────────────────────────────────────────────────
    public class GSMNetwork
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public GSMProvider          Provider                             { get; set; }
        public NetworkCoverage?     CoverageStrength                    { get; set; }
        public NetworkAvailability? AvailabilityArea                    { get; set; }
        public NetworkQuality?      CallAndSMSQuality                   { get; set; }
        public NetworkQuality?      InternetQuality                     { get; set; }
        public NetworkGeneration?   NetworkType                         { get; set; }
        public bool?                AffectedSecurityReportingOrEmergencyCalls { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // SECTION J — SECURITY & SOCIAL PROTECTION
    // ──────────────────────────────────────────────────────────────────
    public class SecurityService
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public SecurityServiceType      Type                        { get; set; }
        [Range(0, int.MaxValue)] public int? NumberFrequentlyAvailable { get; set; }
        public SecurityPostType?        SecurityPostType            { get; set; }
        public CommunityPerceptionRating? CommunityPerception       { get; set; }
        public bool?                    CommunityNeedsMoreOperatives { get; set; }
        public bool?                    PermanentlyStationed        { get; set; }
        public ResponseTime?            AverageResponseTime         { get; set; }
    }

    public class VulnerableGroup
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public VulnerabilityType            Type                          { get; set; }
        [Range(0, int.MaxValue)] public int? NumberOfPeople               { get; set; }
        public bool?                         HasAccessToSpecialServices   { get; set; }
        public CommunityPresencePerception?  CommunityPerceptionOfPresence { get; set; }
        public bool?                         CommunityNeedsMoreSupport    { get; set; }
    }

    public class SocialProtection
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        public SocialProtectionType Type                 { get; set; }
        public bool?                Available           { get; set; }
        [MaxLength(200)] public string? Provider        { get; set; }
        public int?                  YearStarted        { get; set; }
        public bool?                 MakesRealDifference { get; set; }
        public bool?                 CommunityFindsAdequate { get; set; }
        public bool?                 IDPsBenefit         { get; set; }
    }

    public class SecurityProgramme
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string?    Name                       { get; set; }
        [Range(0, int.MaxValue)] public int? NumberOfOperatives       { get; set; }
        [MaxLength(500)] public string?    MainActivity               { get; set; }
        public CommunityRoleInProgramme?   CommunityRole              { get; set; }
        public CommunityPerceptionRating?  CommunityPerception        { get; set; }
        public bool?                       CommunityNeedsMoreOperatives { get; set; }
    }

    public class SecurityIncident
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(300)] public string? Incident              { get; set; }
        [MaxLength(300)] public string? Cause                 { get; set; }
        public int?                     YearOccurred          { get; set; }
        public bool?                    StillImpactingCommunity { get; set; }
        public int?                     YearEffortsToAddress  { get; set; }
        [MaxLength(300)] public string? WhoMadeEfforts        { get; set; }
        [MaxLength(500)] public string? HelpNeeded            { get; set; }
    }

    public class IDPCamp
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string?  Name                             { get; set; }
        [MaxLength(300)] public string?  Location                         { get; set; }
        [Range(0, int.MaxValue)] public int? NumberOfIDPHouseholds        { get; set; }
        public GeneralRating?             LivingConditions                { get; set; }
        public bool?                      CampLargeEnough                 { get; set; }
        public bool?                      SecurityInCampAdequate          { get; set; }
        public bool?                      WomenAndGirlsExposedToGBV       { get; set; }
        public CommunityRelationship?     RelationshipWithHostCommunity   { get; set; }
    }

    public class MigrantSettlerActivity
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(300)] public string? BusinessOrDevelopmentType { get; set; }
        [MaxLength(300)] public string? ServicesOffered           { get; set; }
        public int?                     YearCommenced             { get; set; }
        [MaxLength(300)] public string? Location                  { get; set; }
        [Range(0, int.MaxValue)] public int? NumberOfIndigenesEmployed { get; set; }
        public bool?                    CommunityBenefiting       { get; set; }
        [MaxLength(500)] public string? ChallengesWithOwners      { get; set; }
    }

    public class NGO
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [MaxLength(200)] public string? Name                       { get; set; }
        [MaxLength(500)] public string? ServicesInIDPCamp          { get; set; }
        public int?                     YearCommencedInIDPCamp     { get; set; }
        [MaxLength(500)] public string? ServicesToHostCommunity    { get; set; }
        public int?                     YearCommencedHostCommunity { get; set; }
        public bool?                    CommunityIsSatisfied       { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────
    // PRIORITY NEEDS — stored as rows for easier aggregation
    // ──────────────────────────────────────────────────────────────────
    public class PriorityNeed
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        /// <summary>1 = most urgent, 5 = least urgent among the five.</summary>
        [Range(1, 5)]
        public int Rank { get; set; }

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }

    // ──────────────────────────────────────────────────────────────────
    // CONSENT SIGNATORIES
    // ──────────────────────────────────────────────────────────────────
    public class ConsentSignatory
    {
        [Key] public int Id { get; set; }

        [Required] public Guid SubmissionId { get; set; }
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Role { get; set; } = string.Empty; // "TraditionalRuler", "WomenLeader", etc.

        [MaxLength(200)] public string? Name        { get; set; }
        [MaxLength(20)]  public string? PhoneNumber { get; set; }
        [MaxLength(200)] public string? Signature   { get; set; }

        // Authorization levels (null if not an authorization row)
        [MaxLength(50)]  public string? AuthLevel   { get; set; } // "ClanLevel", "KindredLevel", etc.
        [MaxLength(100)] public string? AuthTitle   { get; set; } // "Tor Kpande", "Ortar", etc.
    }
}

// ════════════════════════════════════════════════════════════════════
// SHARED ENUMERATIONS  (referenced by both survey entities & display)
// ════════════════════════════════════════════════════════════════════
namespace BenueCommunityMapping.Models.Survey
{
    public enum MarketType                  { Daily, Weekly, Periodic }
    public enum FunctionalStatus            { Functional, Nonfunctional }
    public enum InfrastructureCondition    { Excellent, Moderate, Poor }
    public enum ServiceDeliveryCondition   { Excellent, Good, Poor }
    public enum WorkQualityRating          { Excellent, Good, Poor }
    public enum GeneralRating              { Excellent, Good, Poor }
    public enum StaffAvailability          { Adequate, Inadequate }
    public enum DistanceCategory           { LessThan1km, Between1And2km, MoreThan2km }
    public enum DrugAvailability           { Always, Sometimes, Rarely, Never }
    public enum OccurrenceFrequency        { Often, Occasionally, Rarely, None }
    public enum SeverityLevel              { Severe, Moderate, Mild }
    public enum FarmlandAbandonmentPercent { ZeroTo25, TwentySixTo50, FiftyOneTo75, SeventySixTo100 }
    public enum HealthFacilityType         { Tertiary, General, PrimaryHealthCare, PrivateClinic, PrivateHospital, Dispensary, TraditionalHealingCentre }
    public enum OtherFacilityType          { Pharmacy, PatentMedicineStore, Mortuary }
    public enum EducationLevel             { Tertiary, Vocational, Secondary, Primary, Nursery }
    public enum RoadSurfaceType            { Tarred, Untarred, Footpath }
    public enum RoadPassability            { Passable, Difficult, NotPassable }
    public enum DrainagePresence           { Adequate, Inadequate, None }
    public enum RoadDangerLevel            { Death, SeriousInjury, None }
    public enum FinancialServiceType       { CommercialBank, MicrofinanceBank, POSAgentBanking, CooperativeThriftGroup }
    public enum NaturalFeatureType         { River, Stream, Forest, Wetland, Hills }
    public enum IndustrialActivityType     { Manufacturing, Milling }
    public enum EnvironmentalChallengeType { Flooding, Erosion, AirPollution, WaterPollution, Deforestation, WastePollution, Drought }
    public enum ReligiousWorshipCentreType { Church, Mosque, Temple, PrayerHouse, Shrine, Other }
    public enum GSMProvider                { MTN, Airtel, Glo, NineMobile, Others }
    public enum NetworkCoverage            { Strong, Fair, Weak, None }
    public enum NetworkAvailability        { AllParts, MostParts, FewParts }
    public enum NetworkQuality             { Stable, Fair, Unstable }
    public enum NetworkGeneration          { FiveG, FourG, ThreeG }
    public enum SecuritySituation          { Safe, FairlySafe, Unsafe }
    public enum SecurityServiceType        { Police, MobilePolice, Military, CivilDefense, Vigilante, FireService, VolunteerGuards, LivestockGuards, Others }
    public enum SecurityPostType           { SecurityPost, SecurityStation, SecurityPatrol, OccasionalVisit }
    public enum CommunityPerceptionRating  { Positive, Neutral, Negative }
    public enum ResponseTime               { LessThan30Mins, OneToTwoHours, MoreThan2Hours, NoResponse }
    public enum CommunityRoleInProgramme   { LeadRole, SupportiveRole, None }
    public enum VulnerabilityType          { Orphans, Widows, Disabled, Displaced, Refugees, Unemployed, VeryOldPeople, Destitute, Childless }
    public enum CommunityPresencePerception { TooMany, Manageable }
    public enum SocialProtectionType       { CashTransfer, FoodDistribution, SchoolFeedingProgram, LabourIntensivePublicWorkfare, EarlyWarningEarlyResponse, HealthInsurance, Sensitisation, SocialPension, SubsidizedFertilizer, SubsidizedSeeds, LandClearing, OrphanSupport, WidowSupport, DisabilitySupport, Others }
    public enum DisputeResolutionMethod    { TraditionalLeaders, ReligiousLeaders, LocalGovernment, Courts, AlternativeDisputeResolution, Other }
    public enum CommunityRelationship      { Cordial, Neutral, NotCordial }
}
