using System;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Represents an email verification token stored in the database. When a
    /// user registers we generate a token and send it via email. Verifying
    /// the token sets the associated user’s IsVerified flag to true.
    /// </summary>
    public class EmailVerification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    }
}