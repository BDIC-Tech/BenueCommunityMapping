# GAPS AND IMPLEMENTATION PLAN

This document outlines the areas in the `Edit.cshtml` page (and its associated partials) that are not fully implemented according to the `BENUE_COMMUNITY_MAPPING_QUESTIONNAIRE_2026.md` specification. For each gap, a brief implementation plan is provided.

## Section A: Community Identification
- **Status**: ✅ COMPLETED
- **Implementation**: Added `MajorEthnicGroups` property to model and textarea input in both Edit.cshtml and DataAnalysis/Edit.cshtml.

## Section B: Markets and Economic Activities
- **Status**: ✅ COMPLETED
- **Implementation**: Added `OperatesAtNight` boolean property to Market model; added column to _SectionB_New.cshtml markets table.

## Section C: Healthcare Facilities
- **Status**: ✅ COMPLETED
- **Implementation**: 
  1. Added conditional count inputs for women died during childbirth, pregnant women died before childbirth, and children under 5 deaths.
  2. Added `InfrastructureWorkQuality` column to health facilities table.
  3. Added "Other Types of Facilities" table with columns: Name, Location, Type, Distance, Staff, Infrastructure, Service, Work Quality.
  4. Updated _ViewSectionC.cshtml to display both tables.

## Section D: Educational Institutions
- **Status**: ✅ COMPLETED
- **Implementation**: Added `YearLastRenovated`, `InfrastructureWorkQuality`, `WhoBuilt`, and `ConflictClosureYear` columns to educational institutions table in _SectionD_New.cshtml.

## Section E: Access Roads and Transportation
- **Status**: ✅ COMPLETED
- **Implementation**: Added "Truck" checkbox to both rainy season and dry season transport groups in _SectionE_New.cshtml.

## Section F: Financial Services
- **Status**: Fully implemented. No gaps found.

## Section G: Environmental Resources and Agriculture
- **Status**: ✅ COMPLETED
- **Implementation**:
  1. Added `FarmingBoth` property to QuestionnaireSubmission model.
  2. Added natural features table UI in _SectionG_New.cshtml.
  3. Added extension workers services textarea.
  4. Added industrial activities table UI.
  5. Added mining activities table UI.
  6. Added intervention year field in environmental challenges table.
  7. Added "Both" option to farming type checkboxes.
  8. Added environmental improvement urgent options with "Others (specify)" input.
  9. Updated _ViewSectionG.cshtml to display all new data.

## Section H: Religion
- **Status**: ✅ COMPLETED
- **Implementation**:
  1. Added `Name` property to ReligiousGroup model for "Other" specification.
  2. Added "Other" name input field in the religious groups table.
  3. Added "Others (specify):" checkbox with conditional text input for religious challenges.
  4. Updated _ViewSectionH.cshtml to display challenges to religion.

## Section I: Telecommunications and Information
- **Status**: ✅ COMPLETED
- **Implementation**:
  1. Added `OtherProviderName` property to GSMNetwork model.
  2. Converted GSM network table to dynamic table with add/delete buttons in _SectionI_New.cshtml.
  3. Added JavaScript toggle to show/hide "Other Provider Name" field when "Others" is selected.

## Section J: Security and Social Protection
- **Status**: ✅ COMPLETED
- **Implementation**:
  1. Added "None", "Tension between community and security operatives", "Youth restiveness" checkboxes.
  2. Added "Others (Please specify):" checkbox with conditional text input for security issues.
  3. Added "Others (Specify):" checkbox with conditional text input for dispute resolution methods.
  4. Added "Others (Specify):" checkbox with conditional text input for displacement causes.
  5. Updated _ViewSectionJ.cshtml to display all new "Other" values.

## Section K: Community Priority Needs
- **Status**: Fully implemented. No gaps found.

## Consent and Authorization Sections
- **Status**: Fully implemented. No gaps found.

## General Notes
- All newly added model properties are correctly mapped in the `QuestionnaireSubmission` model.
- Database migration `20260614152102_AddMissingQuestionnaireFields` created for new nullable columns.
- Client-side JavaScript handles conditional visibility for new fields.
- Pages/Questionnaire/View.cshtml uses partial views (_ViewSection*) that have been updated.
- Pages/Admin/Analytics/Index.cshtml uses model-aggregated data (no direct field changes needed).
- Pages/DataAnalysis/Index.cshtml uses model-aggregated data (no direct field changes needed).