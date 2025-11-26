using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the EcoCash mobile wallet API. Payments are
    /// stored in memory and marked as successful after a short delay. In
    /// production this would call Econet APIs.
    /// </summary>
    public class EcoCashProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "ecocash_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "ecocash",
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
            // For demo purposes, return a fixed balance. A real implementation would
            // call the EcoCash API to retrieve the wallet balance.
            return Task.FromResult<decimal?>(2000.00m);
        }
    }
}