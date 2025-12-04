using System.Net;
using System.Net.Mail;

namespace Campus_Virtul_GRLL.Services
{
    public class EmailService
    {
        private readonly string _gmailUser;
        private readonly string _gmailAppPassword;

        public EmailService()
        {
            _gmailUser = Environment.GetEnvironmentVariable("GMAIL_USER") ?? string.Empty;
            _gmailAppPassword = Environment.GetEnvironmentVariable("GMAIL_PASSWORD_APP") ?? string.Empty;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_gmailUser) && !string.IsNullOrWhiteSpace(_gmailAppPassword);

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            if (!IsConfigured) throw new InvalidOperationException("EmailService not configured: set GMAIL_USER and GMAIL_PASSWORD_APP in .env");
            using var msg = new MailMessage();
            msg.From = new MailAddress(_gmailUser, "Campus Virtual GRLL");
            msg.To.Add(new MailAddress(to));
            msg.Subject = subject;
            msg.Body = htmlBody;
            msg.IsBodyHtml = true;

            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_gmailUser, _gmailAppPassword)
            };
            await client.SendMailAsync(msg);
        }
    }
}
