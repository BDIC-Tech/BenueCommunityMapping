using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BenueCommunityMapping.Models.Email_Services
{
    /// <summary>
    /// Loads HTML templates from <c>wwwroot/emailtemplate/</c>, replaces
    /// <c>{{Placeholder}}</c> tokens, and dispatches via <see cref="IEmailSending"/>.
    /// </summary>
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IEmailSending       _sender;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailTemplateService> _logger;

        // Template file names (relative to wwwroot/emailtemplate/)
        private const string CoordinatorTemplate    = "coordinator-verification.html";
        private const string AgentTemplate          = "agent-verification.html";
        private const string ForgotPasswordTemplate = "forgot-password.html";

        public EmailTemplateService(
            IEmailSending              sender,
            IWebHostEnvironment        env,
            ILogger<EmailTemplateService> logger)
        {
            _sender = sender;
            _env    = env;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        public async Task SendCoordinatorVerificationAsync(
            string toEmail,
            string fullName,
            string lga,
            string confirmationLink,
            CancellationToken ct = default)
        {
            var html = await LoadTemplateAsync(CoordinatorTemplate);

            html = html
                .Replace("{{FullName}}",         HtmlEncode(fullName))
                .Replace("{{LGA}}",              HtmlEncode(lga))
                .Replace("{{ConfirmationLink}}", confirmationLink);   // URL — not encoded

            await _sender.SendAsync(new EmailMessage
            {
                ToEmail  = toEmail,
                ToName   = fullName,
                Subject  = "Benue Community Mapping – Verify Your Coordinator Account",
                BodyHtml = html,
                BodyText = PlainVerificationText(fullName, "Coordinator", confirmationLink)
            }, ct);
        }

        public async Task SendAgentVerificationAsync(
            string toEmail,
            string fullName,
            string lga,
            string ward,
            string coordinatorName,
            string confirmationLink,
            CancellationToken ct = default)
        {
            var html = await LoadTemplateAsync(AgentTemplate);

            html = html
                .Replace("{{FullName}}",        HtmlEncode(fullName))
                .Replace("{{LGA}}",             HtmlEncode(lga))
                .Replace("{{Ward}}",            HtmlEncode(ward))
                .Replace("{{CoordinatorName}}", HtmlEncode(coordinatorName))
                .Replace("{{ConfirmationLink}}", confirmationLink);   // URL — not encoded

            await _sender.SendAsync(new EmailMessage
            {
                ToEmail  = toEmail,
                ToName   = fullName,
                Subject  = "Benue Community Mapping – Verify Your Agent Account",
                BodyHtml = html,
                BodyText = PlainVerificationText(fullName, "Field Agent", confirmationLink)
            }, ct);
        }

        public async Task SendPasswordResetAsync(
            string toEmail,
            string fullName,
            string resetLink,
            CancellationToken ct = default)
        {
            var html = await LoadTemplateAsync(ForgotPasswordTemplate);

            html = html
                .Replace("{{FullName}}",    HtmlEncode(fullName))
                .Replace("{{Email}}",       HtmlEncode(toEmail))
                .Replace("{{RequestTime}}", DateTime.Now.ToString("dd MMM yyyy, HH:mm") + " (WAT)")
                .Replace("{{ResetLink}}",   resetLink);   // URL — not encoded

            await _sender.SendAsync(new EmailMessage
            {
                ToEmail  = toEmail,
                ToName   = fullName,
                Subject  = "Benue Community Mapping – Password Reset Request",
                BodyHtml = html,
                BodyText = PlainResetText(fullName, resetLink)
            }, ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads a template file from <c>wwwroot/emailtemplate/</c>.
        /// Throws <see cref="FileNotFoundException"/> if the file is missing so
        /// the error surfaces clearly rather than sending a blank email.
        /// </summary>
        private async Task<string> LoadTemplateAsync(string fileName)
        {
            var path = Path.Combine(
                _env.WebRootPath, "emailtemplate", fileName);

            if (!File.Exists(path))
            {
                _logger.LogError(
                    "Email template not found at {Path}. " +
                    "Ensure the file exists in wwwroot/emailtemplate/.", path);

                throw new FileNotFoundException(
                    $"Email template '{fileName}' was not found at: {path}");
            }

            return await File.ReadAllTextAsync(path);
        }

        /// <summary>Plain-text fallback for account verification emails.</summary>
        private static string PlainVerificationText(string fullName, string role, string link) =>
            $"Dear {fullName},\r\n\r\n" +
            $"Your {role} account has been created on the Benue Community Mapping System.\r\n\r\n" +
            "Please verify your email address by clicking or copying the link below:\r\n" +
            $"{link}\r\n\r\n" +
            "This link expires in 24 hours.\r\n\r\n" +
            "Benue Community Mapping System\r\n" +
            "Benue State Bureau of Statistics & Information";

        /// <summary>Plain-text fallback for password reset emails.</summary>
        private static string PlainResetText(string fullName, string link) =>
            $"Dear {fullName},\r\n\r\n" +
            "We received a request to reset your password on the Benue Community Mapping System.\r\n\r\n" +
            $"Click or copy the link below to reset your password:\r\n{link}\r\n\r\n" +
            "This link expires in 2 hours.\r\n" +
            "If you did not request a password reset, please ignore this email.\r\n\r\n" +
            "Benue Community Mapping System\r\n" +
            "Benue State Bureau of Statistics & Information";

        private static string HtmlEncode(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
    }
}
