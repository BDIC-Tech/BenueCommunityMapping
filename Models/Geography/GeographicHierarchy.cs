using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenueCommunityMapping.Models.Geography
{
    // ══════════════════════════════════════════════════════════════════
    // GEOGRAPHIC HIERARCHY
    // LGA ──► Ward ──► Kindred ──► Community
    //
    // Each level is a proper relational table.
    // Every QuestionnaireSubmission links to a Community (leaf node),
    // giving analysts the full LGA→Ward→Kindred→Community chain via FK joins.
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Local Government Area – top of the Benue geographic hierarchy.</summary>
    public class LocalGovernmentArea
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Official LGA code (e.g. BN-MKD for Makurdi).</summary>
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Ward> Wards { get; set; } = new List<Ward>();
    }

    /// <summary>Council Ward within an LGA.</summary>
    public class Ward
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // FK to LGA
        public int LocalGovernmentAreaId { get; set; }
        public LocalGovernmentArea LocalGovernmentArea { get; set; } = null!;

        // Navigation
        public ICollection<Kindred> Kindreds { get; set; } = new List<Kindred>();
    }

    /// <summary>Kindred (family/clan grouping) within a Ward.</summary>
    public class Kindred
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // FK to Ward
        public int WardId { get; set; }
        public Ward Ward { get; set; } = null!;

        // Navigation
        public ICollection<Community> Communities { get; set; } = new List<Community>();
    }

    /// <summary>
    /// Community – the leaf-level geographic unit.
    /// Every questionnaire submission belongs to exactly one community,
    /// giving analysts automatic rollup to Kindred → Ward → LGA.
    /// </summary>
    public class Community
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Official community code issued by BSBS.</summary>
        [MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? MajorEthnicGroups { get; set; }

        [MaxLength(1000)]
        public string? MajorFamilyLineages { get; set; }

        [Range(0, int.MaxValue)]
        public int? EstimatedPopulation { get; set; }

        public bool IsActive { get; set; } = true;

        // FK to Kindred
        public int KindredId { get; set; }
        public Kindred Kindred { get; set; } = null!;

        // Computed convenience navigation (read-only for queries)
        [NotMapped]
        public Ward Ward => Kindred?.Ward!;

        [NotMapped]
        public LocalGovernmentArea LGA => Kindred?.Ward?.LocalGovernmentArea!;

        // Navigation – submissions for this community
        public ICollection<Models.QuestionnaireSubmission> Submissions { get; set; } =
            new List<Models.QuestionnaireSubmission>();
    }
}
