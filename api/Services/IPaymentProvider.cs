using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Defines a common interface for interacting with a payment provider.
    /// Each provider implements the methods necessary to create a payment and
    /// query its status. In a real implementation these would perform
    /// HTTP calls to third‑party APIs and handle authentication, signing and
    /// error handling. For the purposes of this demo the providers simply
    /// simulate behaviour in memory.
    /// </summary>
    public interface IPaymentProvider
    {
        /// <summary>
        /// Create a new payment.
        /// </summary>
        Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request);

        /// <summary>
        /// Retrieve the status of a payment.
        /// </summary>
        Task<PaymentResponse?> GetPaymentAsync(string id);

        /// <summary>
        /// Retrieve the balance for a specific account or wallet. Not all providers
        /// implement this functionality; unsupported providers should return null.
        /// </summary>
        /// <param name="accountId">The identifier of the user or wallet to check.</param>
        /// <returns>The balance if available; otherwise null.</returns>
        Task<decimal?> GetBalanceAsync(string accountId);
    }
}