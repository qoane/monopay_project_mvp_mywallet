using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the M‑Pesa mobile money API. Payments are
    /// stored in memory and marked as successful after a short delay. In a
    /// production environment this class would call Safaricom/Vodacom APIs.
    /// </summary>
    public class MpesaProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "mpesa_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "mpesa",
                CreatedAt = DateTime.UtcNow
            };
            _store[id] = payment;
            // Simulate asynchronous completion
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
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
            // this would call the M‑Pesa API to retrieve the user’s wallet balance.
            return Task.FromResult<decimal?>(1234.56m);
        }
    }
}