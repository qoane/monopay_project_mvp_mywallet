using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MonoPayAggregator.Data;
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
        private readonly MonoPayDbContext _dbContext;

        public PaymentAggregator(IDictionary<string, IPaymentProvider> providers, MonoPayDbContext dbContext)
        {
            _providers = providers;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Create a new payment by delegating to the underlying provider.
        /// Records the payment in persistent storage for reporting purposes.
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            var method = request.PaymentMethod?.ToLowerInvariant() ?? string.Empty;
            if (!_providers.TryGetValue(method, out var provider))
            {
                throw new KeyNotFoundException($"Unsupported payment method '{request.PaymentMethod}'");
            }
            var response = await provider.CreatePaymentAsync(request);
            await SaveOrUpdatePaymentAsync(response);
            return response;
        }

        /// <summary>
        /// Retrieve a payment by ID by asking all providers. In a real
        /// implementation the provider could be determined from the ID prefix.
        /// </summary>
        public async Task<PaymentResponse?> GetPaymentAsync(string id)
        {
            var storedRecord = await _dbContext.PaymentResponses.FindAsync(id);
            if (storedRecord != null && _providers.TryGetValue(storedRecord.PaymentMethod.ToLowerInvariant(), out var provider))
            {
                var providerResult = await provider.GetPaymentAsync(id);
                if (providerResult != null)
                {
                    await SaveOrUpdatePaymentAsync(providerResult);
                    return providerResult;
                }
                return storedRecord.ToResponse();
            }

            foreach (var candidateProvider in _providers.Values)
            {
                var result = await candidateProvider.GetPaymentAsync(id);
                if (result != null)
                {
                    await SaveOrUpdatePaymentAsync(result);
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
        /// Data is returned from persistent storage so history survives
        /// process restarts.
        /// </summary>
        public async Task<IEnumerable<PaymentResponse>> GetAllPayments()
        {
            var records = await _dbContext.PaymentResponses
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return records.Select(r => r.ToResponse());
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

        private async Task SaveOrUpdatePaymentAsync(PaymentResponse response)
        {
            var record = await _dbContext.PaymentResponses.FindAsync(response.Id);
            if (record == null)
            {
                record = PaymentResponseRecord.FromResponse(response);
                _dbContext.PaymentResponses.Add(record);
            }
            else
            {
                record.Status = response.Status;
                record.Amount = response.Amount;
                record.Currency = response.Currency;
                record.PaymentMethod = response.PaymentMethod;
                record.CreatedAt = response.CreatedAt;
                record.CompletedAt = response.CompletedAt;
                record.ErrorsJson = System.Text.Json.JsonSerializer.Serialize(response.Errors ?? new List<string>());
                record.MerchantId = response.MerchantId;
                record.CustomerPhone = response.CustomerPhone;
                record.ProviderReference = response.ProviderReference;
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}