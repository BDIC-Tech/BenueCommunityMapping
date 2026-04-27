namespace BenueCommunityMapping.Models.Email_Services
{
    /// <summary>
    /// Builds and sends role-specific verification / welcome emails.
    /// </summary>
    public interface IEmailTemplateService
    {
        /// <summary>Send an account-verification email to a newly created Coordinator.</summary>
        Task SendCoordinatorVerificationAsync(
            string toEmail,
            string fullName,
            string lga,
            string confirmationLink,
            CancellationToken ct = default);

        /// <summary>Send an account-verification email to a newly created Agent.</summary>
        Task SendAgentVerificationAsync(
            string toEmail,
            string fullName,
            string lga,
            string ward,
            string coordinatorName,
            string confirmationLink,
            CancellationToken ct = default);

        /// <summary>Send a password-reset email with a tokenised reset link.</summary>
        Task SendPasswordResetAsync(
            string toEmail,
            string fullName,
            string resetLink,
            CancellationToken ct = default);
    }
}
