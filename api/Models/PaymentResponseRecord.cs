using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Database representation of a payment response. This ensures payments
    /// survive process restarts and can be reconciled with provider status
    /// checks.
    /// </summary>
    public class PaymentResponseRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LSL";
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorsJson { get; set; } = "[]";
        public string MerchantId { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string ProviderReference { get; set; } = string.Empty;

        public PaymentResponse ToResponse()
        {
            return new PaymentResponse
            {
                Id = Id,
                Status = Status,
                Amount = Amount,
                Currency = Currency,
                PaymentMethod = PaymentMethod,
                CreatedAt = CreatedAt,
                CompletedAt = CompletedAt,
                Errors = DeserializeErrors(),
                MerchantId = MerchantId,
                CustomerPhone = CustomerPhone,
                ProviderReference = ProviderReference
            };
        }

        public static PaymentResponseRecord FromResponse(PaymentResponse response)
        {
            return new PaymentResponseRecord
            {
                Id = response.Id,
                Status = response.Status,
                Amount = response.Amount,
                Currency = response.Currency,
                PaymentMethod = response.PaymentMethod,
                CreatedAt = response.CreatedAt,
                CompletedAt = response.CompletedAt,
                ErrorsJson = JsonSerializer.Serialize(response.Errors ?? new List<string>()),
                MerchantId = response.MerchantId,
                CustomerPhone = response.CustomerPhone,
                ProviderReference = response.ProviderReference
            };
        }

        private List<string> DeserializeErrors()
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(ErrorsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
