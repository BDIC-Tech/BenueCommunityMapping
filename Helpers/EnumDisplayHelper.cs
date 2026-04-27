namespace BenueCommunityMapping.Helpers
{
    public static class EnumDisplayHelper
    {
        private static readonly Dictionary<string, string> Overrides = new(StringComparer.Ordinal)
        {
            // DistanceCategory
            ["LessThan1km"]    = "Less than 1 km",
            ["Between1And2km"] = "1 – 2 km",
            ["MoreThan2km"]    = "More than 2 km",

            // FarmlandAbandonmentPercent
            ["ZeroTo25"]        = "0 – 25%",
            ["TwentySixTo50"]   = "26 – 50%",
            ["FiftyOneTo75"]    = "51 – 75%",
            ["SeventySixTo100"] = "76 – 100%",

            // NetworkGeneration
            ["FiveG"]  = "5G",
            ["FourG"]  = "4G",
            ["ThreeG"] = "3G",

            // GSMProvider
            ["NineMobile"] = "9mobile",

            // ResponseTime
            ["LessThan30Mins"] = "Less than 30 minutes",
            ["OneToTwoHours"]  = "1 – 2 hours",
            ["MoreThan2Hours"] = "More than 2 hours",

            // FinancialServiceType
            ["POSAgentBanking"]      = "POS / Agent Banking",
            ["CooperativeThriftGroup"] = "Cooperative / Thrift Group",

            // FunctionalStatus
            ["Nonfunctional"] = "Non-functional",

            // SocialProtectionType
            ["LabourIntensivePublicWorkfare"] = "Labour-Intensive Public Workfare",
            ["EarlyWarningEarlyResponse"]     = "Early Warning / Early Response",

            // DisputeResolutionMethod
            ["AlternativeDisputeResolution"] = "Alternative Dispute Resolution (ADR)",
        };

        /// <summary>
        /// Converts a PascalCase enum value to human-readable text.
        /// Uses an override dictionary for special cases and falls back
        /// to inserting spaces before uppercase transitions.
        /// </summary>
        public static string ToDisplayText(this Enum value)
        {
            var name = value.ToString();
            if (Overrides.TryGetValue(name, out var display))
                return display;
            return SplitPascalCase(name);
        }

        private static string SplitPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new System.Text.StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0)
                {
                    char prev = text[i - 1];

                    // Space before uppercase that follows a lowercase letter
                    if (char.IsUpper(c) && char.IsLower(prev))
                        sb.Append(' ');
                    // Space before uppercase followed by lowercase, after an uppercase run (e.g. "IDPRelated" → "IDP Related")
                    else if (char.IsUpper(c) && char.IsUpper(prev) && i + 1 < text.Length && char.IsLower(text[i + 1]))
                        sb.Append(' ');
                    // Space at letter-digit or digit-letter boundary
                    else if (char.IsDigit(c) && char.IsLetter(prev))
                        sb.Append(' ');
                    else if (char.IsLetter(c) && char.IsDigit(prev))
                        sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
