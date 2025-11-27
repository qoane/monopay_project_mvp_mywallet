using System;
using System.Collections.Generic;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Represents a simplified payment response. Real implementations may
    /// include additional fields such as fees, exchange rate information, etc.
    /// </summary>
    public class PaymentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LSL";
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Merchant receiving the funds. Included for convenience when
        /// returning a payment status.
        /// </summary>
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>
        /// Phone number of the customer who initiated the payment.
        /// </summary>
        public string? CustomerPhone { get; set; }

        /// <summary>
        /// Identifier returned by the underlying provider for this transaction.
        /// This can be used to query the provider for status updates.
        /// </summary>
        public string ProviderReference { get; set; } = string.Empty;
    }
}
