using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the Khetsi wallet. Functions similarly to
    /// the other demo providers. Replace this with real integration as
    /// documentation becomes available.
    /// </summary>
    public class KhetsiProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "khetsi_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "khetsi",
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
            if (string.IsNullOrWhiteSpace(accountId)) return Task.FromResult<decimal?>(null);
            var hash = Math.Abs(accountId.GetHashCode());
            var balance = (hash % 8000) / 100m + 75m;
            return Task.FromResult<decimal?>(balance);
        }
    }
}