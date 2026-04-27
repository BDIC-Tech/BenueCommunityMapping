using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenueCommunityMapping.Models
{
    // ─────────────────────────────────────────────────────────────
    // ROLE CONSTANTS
    // ─────────────────────────────────────────────────────────────
    public static class AppRoles
    {
        public const string Admin       = "Admin";
        public const string Coordinator = "Coordinator";
        public const string Agent       = "Agent";

        public static readonly string[] All = { Admin, Coordinator, Agent };
    }

    // ─────────────────────────────────────────────────────────────
    // APPLICATION USER
    // ─────────────────────────────────────────────────────────────
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? LocalGovernmentArea { get; set; }

        [MaxLength(200)]
        public string? AssignedWard { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        /// <summary>FK to the Coordinator who supervises this Agent. Null for Admin/Coordinator.</summary>
        public string? CoordinatorId { get; set; }
        public ApplicationUser? Coordinator { get; set; }

        public ICollection<ApplicationUser>         Agents      { get; set; } = new List<ApplicationUser>();
        public ICollection<QuestionnaireSubmission> Submissions { get; set; } = new List<QuestionnaireSubmission>();

        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Set by page models after GetRolesAsync. Not persisted to the database.
        /// </summary>
        [NotMapped]
        public string CachedRole { get; set; } = string.Empty;
    }
}
