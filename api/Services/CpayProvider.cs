using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the CPay wallet. Payments are stored
    /// in memory and marked successful after a delay. Replace with real
    /// API calls when available.
    /// </summary>
    public class CpayProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "cpay_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "cpay",
                MerchantId = request.MerchantId,
                CustomerPhone = request.Customer?.Phone,
                CreatedAt = DateTime.UtcNow
            };
            _store[id] = payment;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(8));
                if (_store.TryGetValue(id, out var p) && p.Status == "pending")
                {
                    p.Status = "success";
                    p.CompletedAt = DateTime.UtcNow;
                }
            });
            return Task.FromResult(payment);
        }

        public Task<PaymentResponse?> GetPaymentAsync(string id)
        {
            _store.TryGetValue(id, out var payment);
            return Task.FromResult(payment);
        }

        public Task<decimal?> GetBalanceAsync(string accountId)
        {
            // Return a deterministic yet seemingly random balance for demo
            if (string.IsNullOrWhiteSpace(accountId)) return Task.FromResult<decimal?>(null);
            var hash = Math.Abs(accountId.GetHashCode());
            var balance = (hash % 5000) / 100m + 50m; // between 50 and 100
            return Task.FromResult<decimal?>(balance);
        }
    }
}