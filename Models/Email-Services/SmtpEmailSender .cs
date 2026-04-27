using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.IO;

namespace BenueCommunityMapping.Models.Email_Services
{
    public class SmtpEmailSender : IEmailSending
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;
        public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            email.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToEmail));
            email.Subject = message.Subject ?? string.Empty;

            var builder = new BodyBuilder();

            if (!string.IsNullOrEmpty(message.BodyHtml))
                builder.HtmlBody = message.BodyHtml;

            if (!string.IsNullOrEmpty(message.BodyText))
                builder.TextBody = message.BodyText;

            if (message.Attachments != null)
            {
                foreach (var (data, fileName, contentType) in message.Attachments)
                {
                    if (data != null && data.Length > 0)
                        builder.Attachments.Add(fileName, data, ContentType.Parse(contentType ?? "application/octet-stream"));
                }
            }

            email.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            try
            {
                // Accept all certificates only if you understand the risk; prefer valid certs in prod
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                // Port 465  → implicit SSL  → SslOnConnect
                // Port 587  → explicit STARTTLS → StartTls
                // UseSsl=true in appsettings signals implicit SSL (port 465 / SMTPS).
                var secureSocketOptions = _options.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

                if (!string.IsNullOrWhiteSpace(_options.UserName))
                {
                    await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
                }

                var result = await client.SendAsync(email, cancellationToken);

                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}", message.ToEmail);
                throw; // rethrow or wrap based on your error-handling strategy
            }
        }

    }
}
