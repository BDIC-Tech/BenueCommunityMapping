using BenueCommunityMapping.Authorization;
using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Survey;
using BenueCommunityMapping.Models.Geography;
using BenueCommunityMapping.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Pages.Questionnaire
{
    public class EditModel : PageModel
    {
        private readonly ISubmissionService            _submissions;
        private readonly UserManager<ApplicationUser>  _userMgr;
        private readonly IAuthorizationService         _authz;
        private readonly AppDbContext                  _db;

        public EditModel(
            ISubmissionService           submissions,
            UserManager<ApplicationUser> userMgr,
            IAuthorizationService        authz,
            AppDbContext                 db)
        {
            _submissions = submissions;
            _userMgr     = userMgr;
            _authz       = authz;
            _db          = db;
        }

        public QuestionnaireSubmission Submission  { get; private set; } = null!;
        public bool IsNew                          { get; private set; }

        // Cascading dropdown data
        public List<Models.Geography.LocalGovernmentArea> LGAs        { get; private set; } = [];
        public List<Models.Geography.Ward>                Wards       { get; private set; } = [];
        public List<Models.Geography.Kindred>             Kindreds    { get; private set; } = [];
        public List<Models.Geography.Community>           Communities { get; private set; } = [];

        // ── GET ──────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return Challenge();
            user.CachedRole = (await _userMgr.GetRolesAsync(user)).FirstOrDefault() ?? AppRoles.Agent;

            await LoadGeoDropdownsAsync();

            if (id.HasValue && id != Guid.Empty)
            {
                var existing = await _submissions.GetByIdAsync(id.Value);
                if (existing is null) return NotFound();

                var auth = await _authz.AuthorizeAsync(User, existing, new SubmissionOwnerRequirement());
                if (!auth.Succeeded) return Forbid();

                if (existing.Status != SubmissionStatus.Draft && existing.Status != SubmissionStatus.Rejected && user.CachedRole != AppRoles.Admin)
                {
                    TempData["Error"] = "Only Draft or Rejected submissions can be edited.";
                    return RedirectToPage("/Agent/MySubmissions");
                }

                Submission = existing;
                IsNew      = false;

                // Pre-load ward/kindred/community for the dropdowns to show correct selection
                if (existing.Community is not null)
                {
                    Wards       = await _db.Wards
                        .Where(w => w.LocalGovernmentAreaId == existing.Community.Kindred.Ward.LocalGovernmentAreaId)
                        .OrderBy(w => w.Name).ToListAsync();
                    Kindreds    = await _db.Kindreds
                        .Where(k => k.WardId == existing.Community.Kindred.WardId)
                        .OrderBy(k => k.Name).ToListAsync();
                    Communities = await _db.Communities
                        .Where(c => c.KindredId == existing.Community.KindredId)
                        .OrderBy(c => c.Name).ToListAsync();
                }
            }
            else
            {
                // New draft — no communityId yet; agent must select via cascading dropdowns
                Submission = new QuestionnaireSubmission
                {
                    Id     = Guid.Empty,
                    Status = SubmissionStatus.Draft
                };
                IsNew = true;
            }

            return Page();
        }

        // ── POST: Save / Submit ───────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user is null) return Challenge();
            user.CachedRole = (await _userMgr.GetRolesAsync(user)).FirstOrDefault() ?? AppRoles.Agent;

            // Pull key ids from form
            if (!Guid.TryParse(Request.Form["submissionId"], out Guid submissionId))
                submissionId = Guid.Empty;

            // Pull community name from form (optional free text)
            string? communityName = Request.Form["communityName"].ToString();
            if (string.IsNullOrWhiteSpace(communityName)) communityName = null;

            int? kindredId = null;
            if (int.TryParse(Request.Form["kindredId"], out int kId) && kId > 0)
                kindredId = kId;

            int communityId = 0;
            if (int.TryParse(Request.Form["communityId"], out int cid) && cid > 0)
            {
                communityId = cid;
            }

            // If community name is provided manually, dynamically find or create the Community entity
            if (communityId == 0 && !string.IsNullOrWhiteSpace(communityName))
            {
                var existingCommunity = await _db.Communities
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == communityName.ToLower() && c.KindredId == kindredId);

                if (existingCommunity != null)
                {
                    communityId = existingCommunity.Id;
                }
                else
                {
                    var newCommunity = new Community
                    {
                        Name = communityName,
                        Code = "NEW-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                        KindredId = kindredId, // Safely nullable
                        IsActive = true
                    };
                    _db.Communities.Add(newCommunity);
                    await _db.SaveChangesAsync();
                    communityId = newCommunity.Id;
                }
            }

            if (communityId == 0)
            {
                await LoadGeoDropdownsAsync();
                TempData["Error"] = "Please select a community or enter one manually before saving.";
                Submission = new QuestionnaireSubmission { Id = Guid.Empty, Status = SubmissionStatus.Draft };
                IsNew = true;
                return Page();
            }

            string action = Request.Form["action"].ToString();

            // Get or create submission
            QuestionnaireSubmission submission;
            if (submissionId == Guid.Empty)
            {
                submission = await _submissions.CreateDraftAsync(user.Id, user.CoordinatorId, communityId);
                submissionId = submission.Id;
            }
            else
            {
                submission = await _submissions.GetByIdAsync(submissionId)
                    ?? throw new KeyNotFoundException("Submission not found.");
                var auth = await _authz.AuthorizeAsync(User, submission, new SubmissionOwnerRequirement());
                if (!auth.Succeeded) return Forbid();
            }

            // Update community link
            submission.CommunityId = communityId;

            // ── SECTION A scalars ─────────────────────────────────────
            submission.EstimatedNumberOfHouseholds    = ParseNullInt("SectionA.EstimatedNumberOfHouseholds");
            submission.AffectedByFarmerHerderConflict = ParseBool("SectionA.AffectedByFarmerHerderConflict");
            submission.ConflictStartYear              = ParseNullInt("SectionA.ConflictStartYear");
            submission.IsHostCommunityToIDPs          = ParseBool("SectionA.IsHostCommunityToIDPs");
            submission.IDPHouseholdsOutsideCamps      = ParseNullInt("SectionA.IDPHouseholdsOutsideCamps");
            submission.MajorFamilyLineages            = F("SectionA.MajorFamilyLineages");
            submission.MajorEthnicGroups              = F("SectionA.MajorEthnicGroups");

            // ── SECTION B ─────────────────────────────────────────────
            submission.MarketChallenges                       = F("SectionB.MajorChallenges");
            submission.MarketActivitiesAffectedByInsecurity   = ParseBool("SectionB.MarketActivitiesAffectedByInsecurity");
            submission.TradersFromOutsideAfraid               = ParseBool("SectionB.TradersFromOutsideAfraid");
            submission.CommunityPaysIllegalLevy               = ParseBool("SectionB.CommunityPaysIllegalLevy");

            // Rebuild Markets collection
            RebuildCollection(submission.Markets, "SectionB.Markets", (i, row) => new Market
            {
                SubmissionId     = submission.Id,
                Name             = row("Name"),
                Location         = row("Location"),
                SizeSquareMeters = ParseNullDouble($"SectionB.Markets[{i}].SizeSquareMeters"),
                Type             = ParseNullEnum<MarketType>($"SectionB.Markets[{i}].Type"),
                StorageFacilities = row("StorageFacilities"),
                MainGoodsSold    = row("MainGoodsSold"),
                FarmProduceSold  = row("FarmProduceSold"),
                MarketStatus     = ParseNullEnum<FunctionalStatus>($"SectionB.Markets[{i}].MarketStatus"),
                InfrastructureCondition = ParseNullEnum<InfrastructureCondition>($"SectionB.Markets[{i}].InfrastructureCondition"),
                MostActiveTimeOfYear = row("MostActiveTimeOfYear"),
                WomenAndYouthMajorParticipants = ParseNullBool($"SectionB.Markets[{i}].WomenAndYouthMajorParticipants"),
                OperatesAtNight  = ParseNullBool($"SectionB.Markets[{i}].OperatesAtNight")
            });

            // ── SECTION C ─────────────────────────────────────────────
            submission.FunctionalAmbulanceOrReferral                   = ParseBool("SectionC.FunctionalAmbulanceOrReferral");
            submission.MajorDiseasesReported                           = F("SectionC.MajorDiseasesReported");
            submission.WomenDiedDuringChildbirthLast2Years             = ParseBool("SectionC.WomenDiedDuringChildbirthLast2Years");
            submission.WomenDiedDuringChildbirthLast2YearsCount        = ParseNullInt("SectionC.WomenDiedDuringChildbirthLast2YearsCount");
            submission.PregnantWomenDiedBeforeChildbirthLast2Years     = ParseBool("SectionC.PregnantWomenDiedBeforeChildbirthLast2Years");
            submission.PregnantWomenDiedBeforeChildbirthLast2YearsCount = ParseNullInt("SectionC.PregnantWomenDiedBeforeChildbirthLast2YearsCount");
            submission.PregnantWomenCanAccessEmergencyTransportAtNight = ParseBool("SectionC.PregnantWomenCanAccessEmergencyTransportAtNight");
            submission.ChildrenUnder5DiedLast2Years                    = ParseBool("SectionC.ChildrenUnder5DiedLast2Years");
            submission.ChildrenUnder5DiedLast2YearsCount               = ParseNullInt("SectionC.ChildrenUnder5DiedLast2YearsCount");
            submission.NearestHealthFacilityIfNone                     = F("SectionC.NearestHealthFacilityIfNoneInCommunity");
            RebuildCollection(submission.HealthFacilities, "SectionC.HealthFacilities", (i, row) => new HealthFacility
            {
                SubmissionId                     = submission.Id,
                Name                             = row("Name"),
                Location                         = row("Location"),
                Type                             = ParseNullEnum<HealthFacilityType>($"SectionC.HealthFacilities[{i}].Type"),
                DistanceFromCentre               = ParseNullEnum<DistanceCategory>($"SectionC.HealthFacilities[{i}].DistanceFromCentre"),
                HealthcareStaffAvailability      = ParseNullEnum<StaffAvailability>($"SectionC.HealthFacilities[{i}].HealthcareStaffAvailability"),
                InfrastructureCondition          = ParseNullEnum<InfrastructureCondition>($"SectionC.HealthFacilities[{i}].InfrastructureCondition"),
                ServiceDeliveryCondition         = ParseNullEnum<ServiceDeliveryCondition>($"SectionC.HealthFacilities[{i}].ServiceDeliveryCondition"),
                WhoBuilt                         = row("WhoBuilt"),
                YearEstablished                  = ParseNullInt($"SectionC.HealthFacilities[{i}].YearEstablished"),
                YearLastRenovated                = ParseNullInt($"SectionC.HealthFacilities[{i}].YearLastRenovated"),
                InfrastructureWorkQuality        = ParseNullEnum<WorkQualityRating>($"SectionC.HealthFacilities[{i}].InfrastructureWorkQuality"),
                IDPsAllowedWithoutDiscrimination = ParseNullBool($"SectionC.HealthFacilities[{i}].IDPsAllowedWithoutDiscrimination"),
                EssentialDrugsAvailability       = ParseNullEnum<DrugAvailability>($"SectionC.HealthFacilities[{i}].EssentialDrugsAvailability")
            });

            // Section C – Q9: Other health facilities (Pharmacy, Patent Medicine, Mortuary)
            RebuildCollection(submission.OtherHealthFacilities, "SectionC.OtherHealthFacilities", (i, row) => new OtherHealthFacility
            {
                SubmissionId            = submission.Id,
                Name                    = row("Name"),
                Location                = row("Location"),
                Type                    = ParseNullEnum<OtherFacilityType>($"SectionC.OtherHealthFacilities[{i}].Type"),
                DistanceFromCentre      = ParseNullEnum<DistanceCategory>($"SectionC.OtherHealthFacilities[{i}].DistanceFromCentre"),
                StaffAvailability       = ParseNullEnum<StaffAvailability>($"SectionC.OtherHealthFacilities[{i}].StaffAvailability"),
                InfrastructureCondition = ParseNullEnum<InfrastructureCondition>($"SectionC.OtherHealthFacilities[{i}].InfrastructureCondition"),
                ServiceDeliveryCondition= ParseNullEnum<ServiceDeliveryCondition>($"SectionC.OtherHealthFacilities[{i}].ServiceDeliveryCondition"),
                WhoBuilt                = row("WhoBuilt"),
                YearEstablished         = ParseNullInt($"SectionC.OtherHealthFacilities[{i}].YearEstablished"),
                YearLastRenovated       = ParseNullInt($"SectionC.OtherHealthFacilities[{i}].YearLastRenovated"),
                InfrastructureWorkQuality = ParseNullEnum<WorkQualityRating>($"SectionC.OtherHealthFacilities[{i}].InfrastructureWorkQuality")
            });

            // ── SECTION D ─────────────────────────────────────────────
            submission.EducationKeyChallenges         = F("SectionD.KeyChallenges");
            submission.NearestInstitutionIfNone        = F("SectionD.NearestInstitutionIfNoneInCommunity");
            submission.ChildrenNotInSchool             = ParseNullInt("SectionD.NumberOfSchoolAgeChildrenNotInSchool");
            submission.OutOfSchoolDueToPoverty         = ParseBool("SectionD.OutOfSchoolDueToPoverty");
            submission.OutOfSchoolDueToInsecurity      = ParseBool("SectionD.OutOfSchoolDueToInsecurity");
            submission.OutOfSchoolDueToChildLabour     = ParseBool("SectionD.OutOfSchoolDueToChildLabour");
            submission.OutOfSchoolDueToEarlyMarriage   = ParseBool("SectionD.OutOfSchoolDueToEarlyMarriage");
            submission.OutOfSchoolDueToDistance        = ParseBool("SectionD.OutOfSchoolDueToDistance");
            submission.OutOfSchoolDueToIDPRelated      = ParseBool("SectionD.OutOfSchoolDueToIDPRelated");
            submission.SchoolsCurrentlyHostingIDPs     = ParseBool("SectionD.SchoolsCurrentlyHostingIDPs");
            RebuildCollection(submission.EducationalInstitutions, "SectionD.Institutions", (i, row) => new EducationalInstitution
            {
                SubmissionId               = submission.Id,
                Name                       = row("Name"),
                Location                   = row("Location"),
                Type                       = ParseNullEnum<EducationLevel>($"SectionD.Institutions[{i}].Type"),
                Owner                      = row("Owner"),
                DistanceFromCentre         = ParseNullEnum<DistanceCategory>($"SectionD.Institutions[{i}].DistanceFromCentre"),
                TeacherAvailability        = ParseNullEnum<StaffAvailability>($"SectionD.Institutions[{i}].TeacherAvailability"),
                InfrastructureCondition    = ParseNullEnum<InfrastructureCondition>($"SectionD.Institutions[{i}].InfrastructureCondition"),
                ServiceDeliveryQuality     = ParseNullEnum<ServiceDeliveryCondition>($"SectionD.Institutions[{i}].ServiceDeliveryQuality"),
                WhoBuilt                   = row("WhoBuilt"),
                YearEstablished            = ParseNullInt($"SectionD.Institutions[{i}].YearEstablished"),
                YearLastRenovated          = ParseNullInt($"SectionD.Institutions[{i}].YearLastRenovated"),
                InfrastructureWorkQuality  = ParseNullEnum<WorkQualityRating>($"SectionD.Institutions[{i}].InfrastructureWorkQuality"),
                DestroyedOrClosedDueToConflict = ParseNullBool($"SectionD.Institutions[{i}].DestroyedOrClosedDueToConflict"),
                ConflictClosureYear        = ParseNullInt($"SectionD.Institutions[{i}].ConflictClosureYear")
            });

            // ── SECTION E ─────────────────────────────────────────────
            submission.MainAccessRoadType   = ParseNullEnum<RoadSurfaceType>("SectionE.MainAccessRoadType");
            submission.RainSeasonMotorcycle = ParseBool("SectionE.RainSeasonMotorcycle");
            submission.RainSeasonCarBus     = ParseBool("SectionE.RainSeasonCarBus");
            submission.RainSeasonTruck      = ParseBool("SectionE.RainSeasonTruck");
            submission.RainSeasonWalking    = ParseBool("SectionE.RainSeasonWalking");
            submission.RainSeasonCanoeBoat  = ParseBool("SectionE.RainSeasonCanoeBoat");
            submission.DrySeasonMotorcycle  = ParseBool("SectionE.DrySeasonMotorcycle");
            submission.DrySeasonCarBus      = ParseBool("SectionE.DrySeasonCarBus");
            submission.DrySeasonTruck       = ParseBool("SectionE.DrySeasonTruck");
            submission.DrySeasonWalking     = ParseBool("SectionE.DrySeasonWalking");
            submission.DrySeasonCanoeBoat   = ParseBool("SectionE.DrySeasonCanoeBoat");
            submission.TransportChallenges  = F("SectionE.TransportChallenges");
            RebuildCollection(submission.AccessRoads, "SectionE.AccessRoads", (i, row) => new AccessRoad
            {
                SubmissionId              = submission.Id,
                RoadName                  = row("RoadName"),
                RainySeasonCondition      = ParseNullEnum<RoadPassability>($"SectionE.AccessRoads[{i}].RainySeasonCondition"),
                DrySeasonCondition        = ParseNullEnum<RoadPassability>($"SectionE.AccessRoads[{i}].DrySeasonCondition"),
                DrainageSystem            = ParseNullEnum<DrainagePresence>($"SectionE.AccessRoads[{i}].DrainageSystem"),
                WhoBuilt                  = row("WhoBuilt"),
                YearConstructed           = ParseNullInt($"SectionE.AccessRoads[{i}].YearConstructed"),
                YearLastRenovated         = ParseNullInt($"SectionE.AccessRoads[{i}].YearLastRenovated"),
                InfrastructureWorkQuality = ParseNullEnum<WorkQualityRating>($"SectionE.AccessRoads[{i}].InfrastructureWorkQuality"),
                MonthsAccessVeryDifficult = ParseNullInt($"SectionE.AccessRoads[{i}].MonthsAccessVeryDifficult"),
                DangersFromBadAccess      = ParseNullEnum<RoadDangerLevel>($"SectionE.AccessRoads[{i}].DangersFromBadAccess")
            });

            // ── SECTION F ─────────────────────────────────────────────
            submission.FinancialServicesChallenges       = F("SectionF.MajorChallenges");
            submission.RelyMoreOnInformalSavingsGroups   = ParseBool("SectionF.RelyMoreOnInformalSavingsGroups");
            submission.MembersLostMoneyToFailedPOSOrFraud = ParseBool("SectionF.MembersLostMoneyToFailedPOSOrFraud");
            RebuildCollection(submission.FinancialServices, "SectionF.FinancialServices", (i, row) => new FinancialService
            {
                SubmissionId             = submission.Id,
                Name                     = row("Name"),
                Location                 = row("Location"),
                Type                     = ParseNullEnum<FinancialServiceType>($"SectionF.FinancialServices[{i}].Type"),
                OffersLoansOrCredit      = ParseNullBool($"SectionF.FinancialServices[{i}].OffersLoansOrCredit"),
                DistanceFromCentre       = ParseNullEnum<DistanceCategory>($"SectionF.FinancialServices[{i}].DistanceFromCentre"),
                CommunityFindsBeneficial = ParseNullBool($"SectionF.FinancialServices[{i}].CommunityFindsBeneficial"),
                WomenAndYouthHaveEqualAccess = ParseNullBool($"SectionF.FinancialServices[{i}].WomenAndYouthHaveEqualAccess")
            });

            // ── SECTION G ─────────────────────────────────────────────
            // Q1: Natural features table
            RebuildCollection(submission.NaturalFeatures, "SectionG.NaturalFeatures", (i, row) => new NaturalFeature
            {
                SubmissionId      = submission.Id,
                Name              = row("Name"),
                Type              = ParseNullEnum<NaturalFeatureType>($"SectionG.NaturalFeatures[{i}].Type"),
                Location          = row("Location"),
                SupervisorManager = row("SupervisorManager"),
                CommunityUse      = row("CommunityUse")
            });

            // Q2: Major challenges with natural features
            submission.NaturalFeaturesChallenges             = F("SectionG.NaturalFeaturesChallenges");

            // Q3: Industrial activities table
            RebuildCollection(submission.IndustrialActivities, "SectionG.IndustrialActivities", (i, row) => new IndustrialActivity
            {
                SubmissionId             = submission.Id,
                ActivityType             = ParseNullEnum<IndustrialActivityType>($"SectionG.IndustrialActivities[{i}].ActivityType"),
                Location                 = row("Location"),
                Owner                    = row("Owner"),
                FinishedProducts         = row("FinishedProducts"),
                Byproducts               = row("Byproducts"),
                RawMaterials             = row("RawMaterials"),
                RawMaterialsSourcedFrom  = row("RawMaterialsSourcedFrom"),
                ProductsSoldWithinCommunity = ParseNullBool($"SectionG.IndustrialActivities[{i}].ProductsSoldWithinCommunity"),
                CommunityBenefits        = row("CommunityBenefits")
            });

            // Q4: Mining activities table
            RebuildCollection(submission.MiningActivities, "SectionG.MiningActivities", (i, row) => new MiningActivity
            {
                SubmissionId             = submission.Id,
                MineralBeingMined        = row("MineralBeingMined"),
                Location                 = row("Location"),
                Owner                    = row("Owner"),
                InputMaterials           = row("InputMaterials"),
                InputMaterialsSourcedFrom = row("InputMaterialsSourcedFrom"),
                ProductsSoldWithinCommunity = ParseNullBool($"SectionG.MiningActivities[{i}].ProductsSoldWithinCommunity"),
                CommunityBenefits        = row("CommunityBenefits"),
                NegativeImpacts          = row("NegativeImpacts")
            });

            submission.FarmingSubsistence                    = ParseBool("SectionG.FarmingSubsistence");
            submission.FarmingCommercial                     = ParseBool("SectionG.FarmingCommercial");
            submission.FarmingBoth                           = ParseBool("SectionG.FarmingBoth");
            submission.DomLandResidential                    = ParseBool("SectionG.DomLandResidential");
            submission.DomLandAgricultural                   = ParseBool("SectionG.DomLandAgricultural");
            submission.DomLandCommercial                     = ParseBool("SectionG.DomLandCommercial");
            submission.DomLandIndustrial                     = ParseBool("SectionG.DomLandIndustrial");
            submission.WaterSourceRiverStream                = ParseBool("SectionG.WaterSourceRiverStream");
            submission.WaterSourceBorehole                   = ParseBool("SectionG.WaterSourceBorehole");
            submission.WaterSourceWell                       = ParseBool("SectionG.WaterSourceWell");
            submission.WaterSourceRainwater                  = ParseBool("SectionG.WaterSourceRainwater");
            submission.WaterSourcePipeBorne                  = ParseBool("SectionG.WaterSourcePipeBorne");
            submission.IrrigationSystemsPresent              = ParseBool("SectionG.IrrigationSystemsPresent");
            submission.NumberOfAgriculturalExtensionWorkers  = ParseNullInt("SectionG.NumberOfAgriculturalExtensionWorkers");
            submission.ExtensionWorkerServices               = F("SectionG.ExtensionWorkerServices");
            submission.FarmlandInaccessibleDueToInsecurity   = ParseBool("SectionG.FarmlandInaccessibleDueToInsecurity");
            submission.PercentFarmlandAbandoned              = ParseNullEnum<FarmlandAbandonmentPercent>("SectionG.PercentFarmlandAbandoned");
            submission.LandDisputesBetweenIndigenesAndIDPs   = ParseBool("SectionG.LandDisputesBetweenIndigenesAndIDPs");
            submission.AccessToTractorsOrMechanizedFarming   = ParseBool("SectionG.AccessToTractorsOrMechanizedFarming");
            submission.GeneralEnvironmentalCondition         = ParseNullEnum<GeneralRating>("SectionG.GeneralEnvironmentalCondition");
            submission.UrgentWasteManagement                 = ParseBool("SectionG.UrgentWasteManagement");
            submission.UrgentDrainage                        = ParseBool("SectionG.UrgentDrainage");
            submission.UrgentTreePlanting                    = ParseBool("SectionG.UrgentTreePlanting");
            submission.UrgentFloodControl                    = ParseBool("SectionG.UrgentFloodControl");
            submission.UrgentPollutionControl                = ParseBool("SectionG.UrgentPollutionControl");
            submission.OtherUrgentEnvImprovement             = F("SectionG.OtherUrgentEnvImprovement");

            // Environmental challenges (one row per type, pre-existing rows updated)
            foreach (var ct in Enum.GetValues<EnvironmentalChallengeType>())
            {
                var prefix = $"SectionG.EnvChallenge.{ct}";
                var existing = submission.EnvironmentalChallenges.FirstOrDefault(e => e.ChallengeType == ct);
                if (existing is null)
                {
                    existing = new EnvironmentalChallenge { SubmissionId = submission.Id, ChallengeType = ct };
                    submission.EnvironmentalChallenges.Add(existing);
                }
                existing.Frequency           = ParseNullEnum<OccurrenceFrequency>($"{prefix}.Frequency");
                existing.TimeOfYear          = F($"{prefix}.TimeOfYear");
                existing.AreasAffected       = F($"{prefix}.AreasAffected");
                existing.Severity            = ParseNullEnum<SeverityLevel>($"{prefix}.Severity");
                existing.YearStarted         = ParseNullInt($"{prefix}.YearStarted");
                existing.MostAffected        = F($"{prefix}.MostAffected");
                existing.InterventionsCarriedOut = ParseNullBool($"{prefix}.InterventionsCarriedOut");
                existing.YearOfIntervention  = ParseNullInt($"{prefix}.YearOfIntervention");
                existing.WhoIntervened       = F($"{prefix}.WhoIntervened");
                existing.InterventionsHelped = ParseNullBool($"{prefix}.InterventionsHelped");
            }

            // ── SECTION H ─────────────────────────────────────────────
            submission.IntoleranceThreatensReligion           = ParseBool("SectionH.IntoleranceThreatensReligion");
            submission.ExtremismThreatensReligion             = ParseBool("SectionH.ExtremismThreatensReligion");
            submission.DiscriminationThreatensReligion        = ParseBool("SectionH.DiscriminationThreatensReligion");
            submission.CrisisThreatensReligion                = ParseBool("SectionH.CrisisThreatensReligion");
            submission.PoliticalInterferenceThreatensReligion = ParseBool("SectionH.PoliticalInterferenceThreatensReligion");
            submission.NoThreatToReligion                     = ParseBool("SectionH.NoThreatToReligion");
            submission.ReligiousOrEthnicTensionsCausedConflict = ParseBool("SectionH.ReligiousOrEthnicTensionsCausedConflict");

            foreach (var ct in Enum.GetValues<ReligiousWorshipCentreType>())
            {
                var prefix = $"SectionH.ReligiousGroup.{ct}";
                var existing = submission.ReligiousGroups.FirstOrDefault(r => r.Type == ct);
                if (existing is null)
                {
                    existing = new ReligiousGroup { SubmissionId = submission.Id, Type = ct };
                    submission.ReligiousGroups.Add(existing);
                }
                existing.NumberExisting                        = ParseNullInt($"{prefix}.NumberExisting");
                existing.EstimatedMembershipPopulation         = ParseNullInt($"{prefix}.EstimatedMembershipPopulation");
                existing.ContributesToEducation                = ParseBool($"{prefix}.ContributesToEducation");
                existing.ContributesToHealthServices           = ParseBool($"{prefix}.ContributesToHealthServices");
                existing.ContributesToPeaceBuilding            = ParseBool($"{prefix}.ContributesToPeaceBuilding");
                existing.ContributesToCharitySocialWelfare     = ParseBool($"{prefix}.ContributesToCharitySocialWelfare");
                existing.ContributesToMoralGuidance            = ParseBool($"{prefix}.ContributesToMoralGuidance");
                existing.ContributesToRoads                    = ParseBool($"{prefix}.ContributesToRoads");
                existing.ContributesToWater                    = ParseBool($"{prefix}.ContributesToWater");
                existing.ContributesToElectricity              = ParseBool($"{prefix}.ContributesToElectricity");
                existing.LeadersActivelyParticipateInPeaceBuilding = ParseNullBool($"{prefix}.LeadersParticipate");
                existing.NumberNoLongerInUseOrDestroyed        = ParseNullInt($"{prefix}.NumberDestroyed");
                if (ct == ReligiousWorshipCentreType.Other)
                    existing.Name = F($"{prefix}.Name");
            }

            // ── SECTION I ─────────────────────────────────────────────
            submission.InternetSourceMobileData    = ParseBool("SectionI.InternetSourceMobileData");
            submission.InternetSourceBroadbandFibre = ParseBool("SectionI.InternetSourceBroadbandFibre");
            submission.InternetSourceSatellite      = ParseBool("SectionI.InternetSourceSatellite");
            submission.CommunicationBlackSpotsExist = ParseBool("SectionI.CommunicationBlackSpotsExist");
            submission.InfoChannelPhoneCallsSMS     = ParseBool("SectionI.InfoChannelPhoneCallsSMS");
            submission.InfoChannelTelevision        = ParseBool("SectionI.InfoChannelTelevision");
            submission.InfoChannelRadio             = ParseBool("SectionI.InfoChannelRadio");
            submission.InfoChannelTownCrier         = ParseBool("SectionI.InfoChannelTownCrier");
            submission.InfoChannelReligiousCentres  = ParseBool("SectionI.InfoChannelReligiousCentres");
            submission.InfoChannelCommunityMeetings = ParseBool("SectionI.InfoChannelCommunityMeetings");
            submission.InfoChannelSocialMedia       = ParseBool("SectionI.InfoChannelSocialMedia");
            submission.TelecommunicationChallenges  = F("SectionI.TelecommunicationChallenges");

            // GSM Networks: dynamic rows keyed as SectionI.GSMNetworks[i]
            RebuildCollection(submission.GSMNetworks, "SectionI.GSMNetworks", (i, row) => new GSMNetwork
            {
                SubmissionId      = submission.Id,
                Provider          = ParseNullEnum<GSMProvider>($"SectionI.GSMNetworks[{i}].Provider") ?? GSMProvider.Others,
                OtherProviderName = row("OtherProviderName"),
                CoverageStrength  = ParseNullEnum<NetworkCoverage>($"SectionI.GSMNetworks[{i}].CoverageStrength"),
                AvailabilityArea  = ParseNullEnum<NetworkAvailability>($"SectionI.GSMNetworks[{i}].AvailabilityArea"),
                CallAndSMSQuality = ParseNullEnum<NetworkQuality>($"SectionI.GSMNetworks[{i}].CallAndSMSQuality"),
                InternetQuality   = ParseNullEnum<NetworkQuality>($"SectionI.GSMNetworks[{i}].InternetQuality"),
                NetworkType       = ParseNullEnum<NetworkGeneration>($"SectionI.GSMNetworks[{i}].NetworkType"),
                AffectedSecurityReportingOrEmergencyCalls = ParseNullBool($"SectionI.GSMNetworks[{i}].AffectedSecurityReportingOrEmergencyCalls")
            });

            // ── SECTION J ─────────────────────────────────────────────
            submission.GeneralSecuritySituation               = ParseNullEnum<SecuritySituation>("SectionJ.GeneralSecuritySituation");
            submission.SecIssueNone                           = ParseBool("SectionJ.SecurityIssueNone");
            submission.SecIssueFarmerHerder                   = ParseBool("SectionJ.SecurityIssueFarmerHerderConflict");
            submission.SecIssueCommunalCrisis                 = ParseBool("SectionJ.SecurityIssueCommunalCrisis");
            submission.SecIssueBanditry                       = ParseBool("SectionJ.SecurityIssueBanditryKidnapping");
            submission.SecIssueTensionWithOps                 = ParseBool("SectionJ.SecurityIssueTensionWithSecurityOperatives");
            submission.SecIssueTheft                          = ParseBool("SectionJ.SecurityIssueTheft");
            submission.SecIssueYouthRestiveness               = ParseBool("SectionJ.SecurityIssueYouthRestiveness");
            submission.SecIssueArmedRobbery                   = ParseBool("SectionJ.SecurityIssueArmedRobbery");
            submission.SecIssueCultism                        = ParseBool("SectionJ.SecurityIssueCultism");
            submission.DispResTraditionalLeaders              = ParseBool("SectionJ.DisputeResolutionTraditionalLeaders");
            submission.DispResReligiousLeaders                = ParseBool("SectionJ.DisputeResolutionReligiousLeaders");
            submission.DispResLocalGovt                       = ParseBool("SectionJ.DisputeResolutionLocalGovernment");
            submission.DispResCourts                          = ParseBool("SectionJ.DisputeResolutionCourts");
            submission.DispResADR                             = ParseBool("SectionJ.DisputeResolutionAlternativeDisputeResolution");
            submission.MostCommonDisputeResolution            = ParseNullEnum<DisputeResolutionMethod>("SectionJ.MostCommonDisputeResolution");
            submission.MembersHadToSleepInBushOrFlee          = ParseBool("SectionJ.MembersHadToSleepInBushOrFlee");
            submission.NearbyCommunitiesCompletelyDestroyed   = ParseBool("SectionJ.NearbyCommunitiesCompletelyDestroyed");
            submission.WomenAndGirlsExposedToGBV              = ParseBool("SectionJ.WomenAndGirlsExposedToGBVDueToDisplacement");
            submission.EstimatedIDPsOutsideCamps              = ParseNullInt("SectionJ.EstimatedIDPsInHostCommunityOutsideCamps");
            submission.DisplacementCauseFarmerHerder          = ParseBool("SectionJ.DisplacementCauseFarmerHerderCrisis");
            submission.DisplacementCauseArmedConflict         = ParseBool("SectionJ.DisplacementCauseArmedConflict");
            submission.DisplacementCauseFlooding              = ParseBool("SectionJ.DisplacementCauseFlooding");
            submission.DisplacementCauseCommunalViolence      = ParseBool("SectionJ.DisplacementCauseCommunalViolence");
            submission.HowCommunityResolvesDisputes           = F("SectionJ.HowCommunityResolvesDisputes");

            foreach (var st in Enum.GetValues<SecurityServiceType>())
            {
                var prefix  = $"SectionJ.SecSvc.{st}";
                var existing = submission.SecurityServices.FirstOrDefault(s => s.Type == st);
                if (existing is null)
                {
                    existing = new SecurityService { SubmissionId = submission.Id, Type = st };
                    submission.SecurityServices.Add(existing);
                }
                existing.NumberFrequentlyAvailable   = ParseNullInt($"{prefix}.Number");
                existing.SecurityPostType            = ParseNullEnum<SecurityPostType>($"{prefix}.PostType");
                existing.CommunityPerception         = ParseNullEnum<CommunityPerceptionRating>($"{prefix}.Perception");
                existing.CommunityNeedsMoreOperatives = ParseNullBool($"{prefix}.NeedsMore");
                existing.PermanentlyStationed        = ParseNullBool($"{prefix}.Permanent");
                existing.AverageResponseTime         = ParseNullEnum<ResponseTime>($"{prefix}.ResponseTime");
            }

            foreach (var vt in Enum.GetValues<VulnerabilityType>())
            {
                var prefix  = $"SectionJ.Vuln.{vt}";
                var existing = submission.VulnerableGroups.FirstOrDefault(v => v.Type == vt);
                if (existing is null)
                {
                    existing = new VulnerableGroup { SubmissionId = submission.Id, Type = vt };
                    submission.VulnerableGroups.Add(existing);
                }
                existing.NumberOfPeople               = ParseNullInt($"{prefix}.Number");
                existing.HasAccessToSpecialServices   = ParseNullBool($"{prefix}.Access");
                existing.CommunityPerceptionOfPresence= ParseNullEnum<CommunityPresencePerception>($"{prefix}.Perception");
                existing.CommunityNeedsMoreSupport    = ParseNullBool($"{prefix}.NeedsSupport");
            }

            foreach (var spt in Enum.GetValues<SocialProtectionType>())
            {
                var prefix  = $"SectionJ.SocProt.{spt}";
                var existing = submission.SocialProtections.FirstOrDefault(s => s.Type == spt);
                if (existing is null)
                {
                    existing = new SocialProtection { SubmissionId = submission.Id, Type = spt };
                    submission.SocialProtections.Add(existing);
                }
                existing.Available              = ParseNullBool($"{prefix}.Available");
                existing.Provider               = F($"{prefix}.Provider");
                existing.YearStarted            = ParseNullInt($"{prefix}.YearStarted");
                existing.MakesRealDifference    = ParseNullBool($"{prefix}.MakesDifference");
                existing.CommunityFindsAdequate = ParseNullBool($"{prefix}.Adequate");
                existing.IDPsBenefit            = ParseNullBool($"{prefix}.IDPsBenefit");
            }

            // Q17 & Q18: Electricity Sources
            submission.PublicPowerSupplyHours= ParseNullEnum<PublicPowerSupplyHours>("SectionJ.PublicPowerSupplyHours");
            submission.ElecSourcePublicPower = ParseBool("SectionJ.ElecSourcePublicPower");
            submission.ElecSourceGenerators  = ParseBool("SectionJ.ElecSourceGenerators");
            submission.ElecSourceSolarPower  = ParseBool("SectionJ.ElecSourceSolarPower");
            submission.ElecSourceOther       = ParseBool("SectionJ.ElecSourceOther");
            submission.ElecSourceOtherSpecify= F("SectionJ.ElecSourceOtherSpecify");

            // Q11: Security Programmes
            RebuildCollection(submission.SecurityProgrammes, "SectionJ.SecProg", (i, row) => new SecurityProgramme
            {
                SubmissionId               = submission.Id,
                Name                       = row("Name"),
                NumberOfOperatives         = ParseNullInt($"SectionJ.SecProg[{i}].Number"),
                MainActivity               = row("Activity"),
                CommunityRole              = ParseNullEnum<CommunityRoleInProgramme>($"SectionJ.SecProg[{i}].Role"),
                CommunityPerception        = ParseNullEnum<CommunityPerceptionRating>($"SectionJ.SecProg[{i}].Perception"),
                CommunityNeedsMoreOperatives = ParseNullBool($"SectionJ.SecProg[{i}].NeedsMore")
            });

            // Q12: Security Incidents
            RebuildCollection(submission.SecurityIncidents, "SectionJ.SecInc", (i, row) => new SecurityIncident
            {
                SubmissionId            = submission.Id,
                Incident                = row("Incident"),
                Cause                   = row("Cause"),
                YearOccurred            = ParseNullInt($"SectionJ.SecInc[{i}].YearOccurred"),
                StillImpactingCommunity = ParseNullBool($"SectionJ.SecInc[{i}].StillImpacting"),
                YearEffortsToAddress    = ParseNullInt($"SectionJ.SecInc[{i}].YearEfforts"),
                WhoMadeEfforts          = row("WhoMadeEfforts"),
                HelpNeeded              = row("HelpNeeded")
            });

            // Q13: IDP Camps
            RebuildCollection(submission.IDPCamps, "SectionJ.IDPCamp", (i, row) => new IDPCamp
            {
                SubmissionId                  = submission.Id,
                Name                          = row("Name"),
                Location                      = row("Location"),
                NumberOfIDPHouseholds         = ParseNullInt($"SectionJ.IDPCamp[{i}].Households"),
                LivingConditions              = ParseNullEnum<GeneralRating>($"SectionJ.IDPCamp[{i}].Conditions"),
                CampLargeEnough               = ParseNullBool($"SectionJ.IDPCamp[{i}].LargeEnough"),
                SecurityInCampAdequate        = ParseNullBool($"SectionJ.IDPCamp[{i}].SecurityAdequate"),
                WomenAndGirlsExposedToGBV     = ParseNullBool($"SectionJ.IDPCamp[{i}].GBV"),
                RelationshipWithHostCommunity = ParseNullEnum<CommunityRelationship>($"SectionJ.IDPCamp[{i}].Relationship")
            });

            // Q17: MigrantSettlerActivities
            RebuildCollection(submission.MigrantSettlerActivities, "SectionJ.MigrantSettler", (i, row) => new MigrantSettlerActivity
            {
                SubmissionId              = submission.Id,
                BusinessOrDevelopmentType = row("BusinessOrDevelopmentType"),
                ServicesOffered           = row("ServicesOffered"),
                YearCommenced             = ParseNullInt($"SectionJ.MigrantSettler[{i}].YearCommenced"),
                Location                  = row("Location"),
                NumberOfIndigenesEmployed = ParseNullInt($"SectionJ.MigrantSettler[{i}].NumberOfIndigenesEmployed"),
                CommunityBenefiting       = ParseNullBool($"SectionJ.MigrantSettler[{i}].CommunityBenefiting"),
                ChallengesWithOwners      = row("ChallengesWithOwners")
            });

            // Q19: NGOs
            RebuildCollection(submission.NGOs, "SectionJ.NGO", (i, row) => new NGO
            {
                SubmissionId               = submission.Id,
                Name                       = row("Name"),
                ServicesInIDPCamp          = row("ServicesInIDPCamp"),
                YearCommencedInIDPCamp     = ParseNullInt($"SectionJ.NGO[{i}].YearCommencedInIDPCamp"),
                ServicesToHostCommunity    = row("ServicesToHostCommunity"),
                YearCommencedHostCommunity = ParseNullInt($"SectionJ.NGO[{i}].YearCommencedHostCommunity"),
                CommunityIsSatisfied       = ParseNullBool($"SectionJ.NGO[{i}].CommunityIsSatisfied")
            });

            // ── SECTION K — Priority Needs ────────────────────────────
            submission.PriorityNeeds.Clear();
            for (int rank = 1; rank <= 5; rank++)
            {
                var desc = F($"SectionK.PriorityNeed{rank}");
                if (!string.IsNullOrWhiteSpace(desc))
                    submission.PriorityNeeds.Add(new PriorityNeed
                    {
                        SubmissionId = submission.Id,
                        Rank         = rank,
                        Description  = desc.Trim()
                    });
            }

            // ── CONSENT ────────────────────────────────────────────────
            submission.ConsentSignatories.Clear();
            var sigRoles = new[] { "TraditionalRuler", "WomenLeader", "YouthLeader", "ReligiousLeader" };
            foreach (var role in sigRoles)
            {
                var name  = F($"Consent.{role}.Name");
                var phone = F($"Consent.{role}.PhoneNumber");
                var sig   = F($"Consent.{role}.Signature");
                if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(phone))
                    submission.ConsentSignatories.Add(new ConsentSignatory
                    {
                        SubmissionId = submission.Id, Role = role,
                        Name = name, PhoneNumber = phone, Signature = sig
                    });
            }
            var authLevels = new[]
            {
                ("ClanLevelTorKpande",    "Clan Level",    "Tor Kpande"),
                ("KindredLevelOrtar",     "Kindred Level", "Ortar"),
                ("CouncilWardLevelTyoor", "Council Ward",  "Tyoor"),
                ("LGALineageLevelMueTer", "LGA Lineage",   "Mue Ter"),
                ("LGALevelTer",           "LGA Level",     "Ter")
            };
            foreach (var (prop, level, title) in authLevels)
            {
                var name = F($"Consent.{prop}.Name");
                var sig  = F($"Consent.{prop}.Signature");
                if (!string.IsNullOrWhiteSpace(name))
                    submission.ConsentSignatories.Add(new ConsentSignatory
                    {
                        SubmissionId = submission.Id, Role = level, AuthLevel = level,
                        AuthTitle = title, Name = name, Signature = sig
                    });
            }

            await _submissions.SaveAsync(submission);

            if (action == "submit")
            {
                if (submission.CommunityId == 0)
                { TempData["Error"] = "Community selection required."; return RedirectToPage(new { id = submissionId }); }

                await _submissions.SubmitAsync(submissionId, user.Id);
                TempData["Success"] = "Questionnaire submitted for coordinator review!";
                return RedirectToPage("/Agent/MySubmissions");
            }

            TempData["Success"] = "Draft saved.";
            return RedirectToPage(new { id = submissionId });
        }

        // ── Geo dropdown loaders ──────────────────────────────────────
        private async Task LoadGeoDropdownsAsync()
        {
            LGAs = await _db.LGAs.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
        }

        // ── Form helpers ─────────────────────────────────────────────
        private string? F(string key) =>
            Request.Form.TryGetValue(key, out var v) ? v.ToString().NullIfEmpty() : null;

        private bool ParseBool(string key) =>
            Request.Form.TryGetValue(key, out var v) && v.ToString() == "true";

        private int? ParseNullInt(string key) =>
            int.TryParse(F(key), out var i) ? i : null;

        private double? ParseNullDouble(string key) =>
            double.TryParse(F(key), out var d) ? d : null;

        private bool? ParseNullBool(string key) =>
            Request.Form.TryGetValue(key, out var v)
                ? v.ToString() switch { "true" => true, "false" => false, _ => null }
                : null;

        private T? ParseNullEnum<T>(string key) where T : struct, Enum =>
            Enum.TryParse<T>(F(key), out var e) ? e : null;

        // ── Collection rebuilder ─────────────────────────────────────
        private void RebuildCollection<T>(
            ICollection<T> collection,
            string prefix,
            Func<int, Func<string, string?>, T> factory)
        {
            collection.Clear();
            for (int i = 0; ; i++)
            {
                // Check if any field for this index exists
                bool any = Request.Form.Keys.Any(k => k.StartsWith($"{prefix}[{i}]."));
                if (!any) break;
                Func<string, string?> row = (field) => F($"{prefix}[{i}].{field}");
                var item = factory(i, row);
                collection.Add(item);
            }
        }
    }

    // Extension method
    internal static class StringExt
    {
        public static string? NullIfEmpty(this string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
