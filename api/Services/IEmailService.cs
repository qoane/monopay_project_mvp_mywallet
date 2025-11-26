using System.Threading.Tasks;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Defines a contract for sending emails. In a production environment
    /// implementations can send via SMTP or a transactional email provider.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Send an account verification email containing a one‑time token.
        /// </summary>
        /// <param name="email">Recipient email address</param>
        /// <param name="token">Verification token</param>
        Task SendVerificationEmailAsync(string email, string token);
    }
}