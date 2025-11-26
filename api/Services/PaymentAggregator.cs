using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Provides a unified API over multiple payment providers. Consumers call
    /// this service instead of working directly with specific providers. The
    /// aggregator chooses the appropriate provider based on the payment
    /// method specified in the request.
    /// </summary>
    public class PaymentAggregator
    {
        private readonly IDictionary<string, IPaymentProvider> _providers;

        // Keep an in‑memory record of all payments created through the aggregator.
        private readonly List<PaymentResponse> _allPayments = new();

        public PaymentAggregator(IDictionary<string, IPaymentProvider> providers)
        {
            _providers = providers;
        }

        /// <summary>
        /// Create a new payment by delegating to the underlying provider.
        /// Records the payment in an internal list for reporting purposes.
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var method = request.PaymentMethod?.ToLowerInvariant() ?? string.Empty;
            if (!_providers.TryGetValue(method, out var provider))
            {
                throw new KeyNotFoundException($"Unsupported payment method '{request.PaymentMethod}'");
            }
            var response = await provider.CreatePaymentAsync(request);
            lock (_allPayments)
            {
                _allPayments.Add(response);
            }
            return response;
        }

        /// <summary>
        /// Retrieve a payment by ID by asking all providers. In a real
        /// implementation the provider could be determined from the ID prefix.
        /// </summary>
        public async Task<PaymentResponse?> GetPaymentAsync(string id)
        {
            foreach (var provider in _providers.Values)
            {
                var result = await provider.GetPaymentAsync(id);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Return a list of supported payment rails. This would normally be
        /// configured externally.
        /// </summary>
        public IEnumerable<object> ListWallets()
        {
            return new[]
            {
                new { code = "mpesa", name = "M‑Pesa", country = "Lesotho" },
                new { code = "ecocash", name = "EcoCash", country = "Lesotho" },
                new { code = "mywallet", name = "MyWallet", country = "Lesotho" },
                new { code = "cpay", name = "CPay", country = "Lesotho" },
                new { code = "khetsi", name = "Khetsi", country = "Lesotho" },
                new { code = "eft", name = "Bank EFT", country = "Lesotho" },
                new { code = "card", name = "Card", country = "International" }
            };
        }

        /// <summary>
        /// Return a snapshot of all payments created through the aggregator.
        /// A copy is returned to avoid consumers enumerating over the internal
        /// list while it is being modified, which could lead to race conditions
        /// or collection‑modified exceptions.
        /// </summary>
        public IEnumerable<PaymentResponse> GetAllPayments()
        {
            lock (_allPayments)
            {
                return _allPayments.ToArray();
            }
        }

        /// <summary>
        /// Retrieve the balance for a specific account on a given payment method.
        /// Returns null if the provider does not support balance queries.
        /// </summary>
        public Task<decimal?> GetBalanceAsync(string method, string accountId)
        {
            if (string.IsNullOrWhiteSpace(method) || !_providers.TryGetValue(method.ToLowerInvariant(), out var provider))
            {
                return Task.FromResult<decimal?>(null);
            }
            return provider.GetBalanceAsync(accountId);
        }
    }
}