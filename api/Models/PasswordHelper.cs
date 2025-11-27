using System.Security.Cryptography;
using System.Text;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Helper utilities for hashing user passwords consistently across the
    /// application. The hash algorithm is intentionally simple for the MVP
    /// and should be replaced with a stronger approach (e.g. PBKDF2) for
    /// production scenarios.
    /// </summary>
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
