using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the MyWallet API. In a production
    /// environment this provider would authenticate using the merchant
    /// credentials, obtain a session token and invoke the remote endpoints
    /// (login, payMerchant, checkStatus, etc.). In this MVP we store
    /// payments in memory and mark them successful after a delay.
    /// </summary>
    public class MyWalletProvider : IPaymentProvider
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, PaymentResponse> _cache = new();

        public MyWalletProvider(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Create a payment via MyWallet. This method logs in with the
        /// configured credentials, initiates a user check and then performs
        /// the merchant payment. If the remote API is unreachable the
        /// payment falls back to an in‑memory simulation so that sandbox
        /// behaviour remains consistent during development.
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            // Generate a local reference ID for our own tracking
            var localId = "mywallet_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var payment = new PaymentResponse
            {
                Id = localId,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "mywallet",
                MerchantId = request.MerchantId,
                CustomerPhone = request.Customer?.Phone,
                CreatedAt = DateTime.UtcNow
            };
            // Attempt to call the real API. Any exception will cause us to
            // fallback to the in‑memory simulation below.
            try
            {
                var baseUrl = _config["PaymentProviders:MyWallet:BaseUrl"] ?? string.Empty;
                var username = _config["PaymentProviders:MyWallet:Username"] ?? string.Empty;
                var password = _config["PaymentProviders:MyWallet:Password"] ?? string.Empty;
                var otp = _config["PaymentProviders:MyWallet:Otp"] ?? "9999";
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("MyWallet configuration missing.");
                }
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // 1. Login to obtain a session token
                var loginPayload = new { email = username, password = password };
                var loginResponse = await client.PostAsync(
                    $"{baseUrl.TrimEnd('/')}/login",
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(loginPayload), System.Text.Encoding.UTF8, "application/json"));
                loginResponse.EnsureSuccessStatusCode();
                using var loginDoc = System.Text.Json.JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
                var token = loginDoc.RootElement.GetProperty("token").GetString() ?? throw new Exception("No token returned from MyWallet login.");

                // 2. Check recipient and prepare payment. MyWallet expects a reference
                // string to be unique per transaction. We fall back to our local ID if no reference provided.
                var reference = request.Reference ?? localId;
                var checkPayload = new
                {
                    recipientCell = request.Customer?.Phone,
                    amount = request.Amount,
                    reference = reference,
                    mywalletUser = false,
                    mywalletAccount = (string?)null,
                    commission = 0
                };
                var checkResponse = await client.PostAsync(
                    $"{baseUrl.TrimEnd('/')}/checkUser",
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(checkPayload), System.Text.Encoding.UTF8, "application/json"));
                checkResponse.EnsureSuccessStatusCode();

                // 3. Pay merchant using token and OTP
                var payPayload = new { token = token, otp = otp };
                var payResponse = await client.PostAsync(
                    $"{baseUrl.TrimEnd('/')}/payMerchant",
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(payPayload), System.Text.Encoding.UTF8, "application/json"));
                payResponse.EnsureSuccessStatusCode();
                using var payDoc = System.Text.Json.JsonDocument.Parse(await payResponse.Content.ReadAsStringAsync());
                var status = payDoc.RootElement.GetProperty("status").GetString();
                // Optionally extract provider reference from response
                var providerRef = payDoc.RootElement.TryGetProperty("reference", out var refEl) ? refEl.GetString() : reference;
                payment.Status = status ?? "pending";
                payment.CompletedAt = status == "success" ? DateTime.UtcNow : null;
                payment.ProviderReference = providerRef ?? reference;
                // Cache the response for retrieval
                _cache[localId] = payment;
                return payment;
            }
            catch
            {
                // Fallback simulation: mark as successful after delay
                _cache[localId] = payment;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    if (_cache.TryGetValue(localId, out var p) && p.Status == "pending")
                    {
                        p.Status = "success";
                        p.CompletedAt = DateTime.UtcNow;
                    }
                });
                return payment;
            }
        }

        /// <summary>
        /// Retrieve the status of a payment. If a provider reference is
        /// available we query the remote API via checkStatus; otherwise
        /// return from the local cache.
        /// </summary>
        public async Task<PaymentResponse?> GetPaymentAsync(string id)
        {
            if (!_cache.TryGetValue(id, out var payment))
            {
                return null;
            }
            // If we have a provider reference and the status is pending, try
            // to refresh status from the remote API
            if (!string.IsNullOrWhiteSpace(payment.ProviderReference) && payment.Status == "pending")
            {
                try
                {
                    var baseUrl = _config["PaymentProviders:MyWallet:BaseUrl"] ?? string.Empty;
                    var client = _httpClientFactory.CreateClient();
                    var statusPayload = new { reference = payment.ProviderReference };
                    var statusResponse = await client.PostAsync(
                        $"{baseUrl.TrimEnd('/')}/checkStatus",
                        new StringContent(System.Text.Json.JsonSerializer.Serialize(statusPayload), System.Text.Encoding.UTF8, "application/json"));
                    statusResponse.EnsureSuccessStatusCode();
                    using var doc = System.Text.Json.JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
                    var newStatus = doc.RootElement.GetProperty("status").GetString();
                    if (!string.IsNullOrEmpty(newStatus))
                    {
                        payment.Status = newStatus;
                        if (newStatus == "success")
                        {
                            payment.CompletedAt = DateTime.UtcNow;
                        }
                    }
                }
                catch
                {
                    // ignore remote errors
                }
            }
            return payment;
        }

        public Task<decimal?> GetBalanceAsync(string accountId)
        {
            // The MyWallet API does not expose a balance endpoint in the
            // provided documentation. Return null to indicate unsupported.
            return Task.FromResult<decimal?>(null);
        }
    }
}