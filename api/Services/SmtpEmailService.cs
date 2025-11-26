using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Basic SMTP email service implementation. Reads configuration from
    /// EmailOptions and uses SmtpClient to send messages. In this demo the
    /// SendVerificationEmailAsync method simply writes to the console as
    /// there are no real SMTP credentials configured.
    /// </summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly Models.EmailOptions _options;

        public SmtpEmailService(IOptions<Models.EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendVerificationEmailAsync(string email, string token)
        {
            // Compose verification URL (in a real deployment this should be
            // your publicly accessible endpoint). For the MVP we assume
            // https://monopay.local/verify?token={token}
            var verifyUrl = $"https://monopay.local/verify?token={Uri.EscapeDataString(token)}";
            var subject = "Verify your MonoPay account";
            // Construct a simple yet attractive HTML body. Feel free to
            // customise colours and layout to match the MonoPay brand.
            var body = $@"<html><body style='font-family:Arial,sans-serif;background-color:#0a192f;color:#d4f4dd;padding:30px;'>
                <h2 style='color:#76d9b0;'>Welcome to MonoPay</h2>
                <p>Hi there,</p>
                <p>Thank you for signing up. Please verify your email address by clicking the button below:</p>
                <p style='text-align:center;margin:30px 0;'>
                    <a href='{verifyUrl}' style='background-color:#76d9b0;color:#0a192f;padding:12px 24px;border-radius:5px;text-decoration:none;font-weight:bold;'>Verify Email</a>
                </p>
                <p>If you did not create an account, please ignore this email.</p>
                <p style='margin-top:40px;font-size:0.8rem;color:#94c8b5;'>MonoPay &copy; {DateTime.UtcNow:yyyy}</p>
            </body></html>";

            var message = new MailMessage()
            {
                From = new MailAddress(_options.SenderEmail, _options.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(email);

            using var client = new SmtpClient(_options.SmtpServer, _options.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(
                    string.IsNullOrEmpty(_options.Username) ? _options.SenderEmail : _options.Username,
                    _options.Password)
            };
            await client.SendMailAsync(message);
        }
    }
}