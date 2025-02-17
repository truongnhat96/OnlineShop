using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UseCase.MailService
{
    public class MailSender : IMailSender
    {
        private readonly MailSettings _mailSettings;
        private readonly ILogger<MailSender> _logger;

        public MailSender(IOptions<MailSettings> mailSettings, ILogger<MailSender> logger)
        {
            _mailSettings = mailSettings.Value;
            _logger = logger;
        }
        public async Task<bool> SendMailAsync(MailContent content)
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress(_mailSettings.DisplayName, _mailSettings.Mail);
            email.From.Add(email.Sender);
            email.To.Add(MailboxAddress.Parse(content.To));
            email.Subject = content.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = content.Body
            };
            email.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_mailSettings.Mail, _mailSettings.Password);
                await client.SendAsync(email);
                _logger.LogInformation("Email sent to {T} successfully", content.To);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
            finally
            {
                await client.DisconnectAsync(true);
                client.Dispose();
            }
        }
    }
}
