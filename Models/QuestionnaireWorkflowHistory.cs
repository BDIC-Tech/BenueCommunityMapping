using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenueCommunityMapping.Models
{
    public class QuestionnaireWorkflowHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid SubmissionId { get; set; }
        
        [ForeignKey("SubmissionId")]
        public QuestionnaireSubmission Submission { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // e.g. "Submitted", "Resubmitted", "Reviewed", "Approved", "Rejected"

        [Required]
        public string ActorId { get; set; } = string.Empty;
        
        [ForeignKey("ActorId")]
        public ApplicationUser Actor { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string ActorRole { get; set; } = string.Empty; // e.g. "Agent", "Coordinator", "Admin"

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(2000)]
        public string? Comments { get; set; }
    }
}
