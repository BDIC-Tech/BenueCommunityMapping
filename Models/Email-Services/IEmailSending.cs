namespace BenueCommunityMapping.Models.Email_Services
{
    public interface IEmailSending
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }
}
