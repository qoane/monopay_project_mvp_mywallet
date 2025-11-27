using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MonoPayAggregator.Models;
using MonoPayAggregator.Data;
using Microsoft.Extensions.Logging;

namespace MonoPayAggregator.Services
{
    /// <summary>
    /// Simulates integration with the MyWallet API. In a production
    /// environment this provider would authenticate using the merchant
    /// credentials, obtain a session token and invoke the remote endpoints
    /// (login, payMerchant, checkStatus, etc.). In this MVP we store
    /// payments and cache entries in the database so they survive restarts
    /// and can be reconciled on subsequent status checks.
    /// </summary>
    public class MyWalletProvider : IPaymentProvider
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MyWalletProvider> _logger;
        private readonly MonoPayDbContext _dbContext;

        public MyWalletProvider(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<MyWalletProvider> logger,
            MonoPayDbContext dbContext)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Create a payment via MyWallet. This method logs in with the
        /// configured credentials, initiates a user check and then performs
        /// the merchant payment. If the remote API fails the payment returns
        /// a failed status with gateway error details.
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            // Generate a local reference ID for our own tracking
            var localId = "mywallet_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var reference = request.Reference ?? localId;
            var payment = new PaymentResponse
            {
                Id = localId,
                Status = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = "mywallet",
                MerchantId = request.MerchantId,
                CustomerPhone = request.Customer?.Phone,
                CreatedAt = DateTime.UtcNow,
                ProviderReference = reference
            };
            var otp = request.Otp?.Trim();
            if (string.IsNullOrWhiteSpace(otp))
            {
                payment.Status = "failed";
                payment.Errors.Add("OTP is required for MyWallet payments.");
                await SaveCacheEntryAsync(localId, reference, null);
                return payment;
            }
            if (!otp.All(char.IsDigit) || otp.Length < 4 || otp.Length > 8)
            {
                payment.Status = "failed";
                payment.Errors.Add("The OTP provided is invalid for MyWallet payMerchant.");
                await SaveCacheEntryAsync(localId, reference, null);
                return payment;
            }
            // Attempt to call the real API. If any step fails we return a
            // failed payment with details for the caller.
            try
            {
                var baseUrl = _config["PaymentProviders:MyWallet:BaseUrl"] ?? string.Empty;
                var username = _config["PaymentProviders:MyWallet:Username"] ?? string.Empty;
                var password = _config["PaymentProviders:MyWallet:Password"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("MyWallet configuration missing.");
                }
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                // 1. Login to obtain a bearer token
                var sessionToken = await LoginAsync(client, baseUrl, username, password);

                // 2. Check recipient and prepare payment. MyWallet expects a reference
                // string to be unique per transaction. We fall back to our local ID if no reference provided.
                var paymentToken = await CheckUserAsync(client, baseUrl, sessionToken, new
                {
                    recipientCell = request.Customer?.Phone,
                    amount = request.Amount,
                    reference,
                    mywalletUser = false,
                    mywalletAccount = (string?)null,
                    commission = 0
                });

                // 3. Pay merchant using token and OTP
                var payResult = await PayMerchantAsync(client, baseUrl, sessionToken, paymentToken, otp);
                payment.Status = payResult.status;
                payment.CompletedAt = payResult.status == "success" ? DateTime.UtcNow : null;
                payment.ProviderReference = payResult.reference ?? reference;
                payment.Errors.AddRange(payResult.errors);
                if (payResult.status != "success" && payResult.errors.Count == 0)
                {
                    payment.Errors.Add("MyWallet payMerchant returned a non-success status.");
                }

                await SaveCacheEntryAsync(localId, payment.ProviderReference, sessionToken);
                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MyWallet payment creation failed for reference {Reference}", reference);
                payment.Status = "failed";
                payment.Errors.Add(GetInnermostMessage(ex));
                await SaveCacheEntryAsync(localId, reference, null);
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
            var record = await _dbContext.PaymentResponses.FindAsync(id);
            var cacheEntry = await _dbContext.MyWalletCacheEntries.FindAsync(id);

            if (record == null || cacheEntry == null)
            {
                return null;
            }
            var payment = record.ToResponse();
            // If we have a provider reference and the status is pending, try
            // to refresh status from the remote API
            if (!string.IsNullOrWhiteSpace(payment.ProviderReference) && payment.Status == "pending")
            {
                try
                {
                    var baseUrl = _config["PaymentProviders:MyWallet:BaseUrl"] ?? string.Empty;
                    var username = _config["PaymentProviders:MyWallet:Username"] ?? string.Empty;
                    var password = _config["PaymentProviders:MyWallet:Password"] ?? string.Empty;
                    var client = _httpClientFactory.CreateClient();
                    var sessionToken = cacheEntry.SessionToken ?? await LoginAsync(client, baseUrl, username, password);
                    var statusPayload = new { reference = payment.ProviderReference };
                    var statusResponse = await PostWithBearerAsync(client, baseUrl, sessionToken, "checkStatus", statusPayload);
                    statusResponse.EnsureSuccessStatusCode();
                    using var doc = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
                    var newStatus = TryGetString(doc.RootElement, "status") ?? TryGetString(doc.RootElement, "state");
                    if (string.IsNullOrEmpty(newStatus) && doc.RootElement.TryGetProperty("data", out var dataEl))
                    {
                        newStatus = TryGetString(dataEl, "status");
                    }
                    if (!string.IsNullOrEmpty(newStatus))
                    {
                        payment.Status = newStatus;
                        if (newStatus == "success")
                        {
                            payment.CompletedAt = DateTime.UtcNow;
                        }
                    }
                    cacheEntry.SessionToken = sessionToken;
                    cacheEntry.LastStatusCheck = DateTime.UtcNow;
                    await SaveCacheEntryAsync(id, payment.ProviderReference, cacheEntry.SessionToken, cacheEntry.LastStatusCheck);
                    await SavePaymentRecordAsync(payment);
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

        private async Task<string> LoginAsync(HttpClient client, string baseUrl, string username, string password)
        {
            var loginPayload = new { email = username, password = password };
            var response = await client.PostAsync(
                $"{baseUrl.TrimEnd('/')}/login",
                new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json"));
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = BuildGatewayErrorMessage("login", response, content);
                _logger.LogError("MyWallet login failed: {Message}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            using var doc = JsonDocument.Parse(content);
            var token = TryGetString(doc.RootElement, "token");
            if (string.IsNullOrEmpty(token) && doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                token = TryGetString(dataEl, "token");
            }
            return token ?? throw new Exception("No token returned from MyWallet login.");
        }

        private async Task<string> CheckUserAsync(HttpClient client, string baseUrl, string bearer, object payload)
        {
            var response = await PostWithBearerAsync(client, baseUrl, bearer, "checkUser", payload);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = BuildGatewayErrorMessage("checkUser", response, content);
                _logger.LogError("MyWallet checkUser failed: {Message}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var token = TryGetString(dataEl, "token");
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
            throw new Exception("MyWallet checkUser did not return a token.");
        }

        private async Task<(string status, string? reference, List<string> errors)> PayMerchantAsync(HttpClient client, string baseUrl, string bearer, string paymentToken, string otp)
        {
            var response = await PostWithBearerAsync(client, baseUrl, bearer, "payMerchant", new { token = paymentToken, otp });
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = BuildGatewayErrorMessage("payMerchant", response, content);
                _logger.LogError("MyWallet payMerchant failed: {Message}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            using var doc = JsonDocument.Parse(content);
            var statusCode = TryGetInt(doc.RootElement, "status_code");
            var status = TryGetString(doc.RootElement, "status") ?? (statusCode == 200 ? "success" : "failed");
            var errors = ExtractGatewayMessages(doc.RootElement);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                status = TryGetString(dataEl, "status") ?? status;
                errors.AddRange(ExtractGatewayMessages(dataEl));
            }
            var reference = TryGetString(doc.RootElement, "reference");
            if (string.IsNullOrEmpty(reference) && doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                reference = TryGetString(dataElement, "reference");
            }
            return (status ?? "pending", reference, errors);
        }

        private async Task<HttpResponseMessage> PostWithBearerAsync(HttpClient client, string baseUrl, string bearer, string path, object payload)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return await client.PostAsync($"{baseUrl.TrimEnd('/')}/{path}", content);
        }

        private async Task SaveCacheEntryAsync(string paymentId, string providerReference, string? sessionToken, DateTime? lastCheck = null)
        {
            var entry = await _dbContext.MyWalletCacheEntries.FindAsync(paymentId);
            if (entry == null)
            {
                entry = new MyWalletCacheEntryRecord
                {
                    PaymentId = paymentId,
                    ProviderReference = providerReference,
                    SessionToken = sessionToken,
                    LastStatusCheck = lastCheck,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.MyWalletCacheEntries.Add(entry);
            }
            else
            {
                entry.ProviderReference = providerReference;
                entry.SessionToken = sessionToken;
                entry.LastStatusCheck = lastCheck ?? entry.LastStatusCheck;
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task SavePaymentRecordAsync(PaymentResponse payment)
        {
            var record = await _dbContext.PaymentResponses.FindAsync(payment.Id);
            if (record == null)
            {
                _dbContext.PaymentResponses.Add(PaymentResponseRecord.FromResponse(payment));
            }
            else
            {
                record.Status = payment.Status;
                record.CompletedAt = payment.CompletedAt;
                record.ProviderReference = payment.ProviderReference;
                record.ErrorsJson = System.Text.Json.JsonSerializer.Serialize(payment.Errors ?? new List<string>());
            }

            await _dbContext.SaveChangesAsync();
        }

        private string BuildGatewayErrorMessage(string operation, HttpResponseMessage response, string content)
        {
            return $"MyWallet {operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {content}";
        }

        private List<string> ExtractGatewayMessages(JsonElement element)
        {
            var errors = new List<string>();
            var message = TryGetString(element, "message");
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message!);
            }
            var error = TryGetString(element, "error");
            if (!string.IsNullOrWhiteSpace(error) && error != message)
            {
                errors.Add(error!);
            }
            if (element.TryGetProperty("errors", out var errorsElement))
            {
                if (errorsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in errorsElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                        {
                            errors.Add(item.GetString()!);
                        }
                    }
                }
                else if (errorsElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(errorsElement.GetString()))
                {
                    errors.Add(errorsElement.GetString()!);
                }
            }
            return errors;
        }

        private static string GetInnermostMessage(Exception ex)
        {
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex.Message;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }

        private static int? TryGetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
                ? prop.GetInt32()
                : null;
        }
    }
}