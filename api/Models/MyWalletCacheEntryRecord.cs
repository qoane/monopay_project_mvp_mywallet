using System;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Persists MyWallet session tokens and provider references so status
    /// reconciliations can occur after process restarts.
    /// </summary>
    public class MyWalletCacheEntryRecord
    {
        public string PaymentId { get; set; } = string.Empty;
        public string ProviderReference { get; set; } = string.Empty;
        public string? SessionToken { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastStatusCheck { get; set; }
    }
}
