using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with card payment processors. In a real system
    /// this would connect to a payment gateway like PayGate/Paystack/Stripe.
    /// Here we mark transactions as successful instantly to demonstrate the
    /// faster settlement of card rails.
    /// </summary>
    public class CardProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<string, PaymentResponse> _store = new();

        public Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var id = "card_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = id,
                Status = "success",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "card",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            _store[id] = payment;
            return Task.FromResult(payment);
        }

        public Task<PaymentResponse?> GetPaymentAsync(string id)
        {
            _store.TryGetValue(id, out var payment);
            return Task.FromResult(payment);
        }

        public Task<decimal?> GetBalanceAsync(string accountId)
        {
            // Card-based gateways typically don’t expose a wallet balance, so return null.
            return Task.FromResult<decimal?>(null);
        }
    }
}