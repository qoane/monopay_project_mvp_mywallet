using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the Lesotho EFT system. EFT payments are
    /// slower to settle so we delay completion longer. In production this
    /// would call the banking EFT switch.
    /// </summary>
    public class EftProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "eft_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "eft",
                CreatedAt = DateTime.UtcNow
            };
            _store[id] = payment;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(20));
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
            // For demo purposes, return a fixed balance. In a real implementation
            // this would call the bank’s API for account balances.
            return Task.FromResult<decimal?>(5000.00m);
        }
    }
}