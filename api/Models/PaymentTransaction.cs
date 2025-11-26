using System;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Represents a payment stored in the database. This class captures
    /// details about the transaction including the merchant, customer and
    /// status. Real implementations may include many more fields such as
    /// fees, exchange rates and provider‑specific metadata.
    /// </summary>
    public class PaymentTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ProviderReference { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LSL";
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}