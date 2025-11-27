using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;
using MonoPayAggregator.Controllers;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MonoPayAggregator.Data;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Tests;

public class MyWalletApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["PaymentProviders:MyWallet:BaseUrl"] = "http://fake-mywallet",
                ["PaymentProviders:MyWallet:Username"] = SeedData.MyWalletTestEmail,
                ["PaymentProviders:MyWallet:Password"] = SeedData.MyWalletTestPassword
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MonoPayDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var databaseName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<MonoPayDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MonoPayDbContext>();
            dbContext.Database.EnsureCreated();
            SeedData.EnsureSeeded(dbContext);

            // Replace the default HttpClient factory with a fake handler that
            // simulates the MyWallet API, enforcing that bearer tokens and OTPs
            // are provided as expected by the real integration.
            var handler = new FakeMyWalletHttpMessageHandler();
            services.Replace(ServiceDescriptor.Singleton(handler));
            services.Replace(ServiceDescriptor.Singleton<IHttpClientFactory>(new FakeHttpClientFactory(handler)));
        });
    }
}

public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public FakeHttpClientFactory(FakeMyWalletHttpMessageHandler handler)
    {
        _client = new HttpClient(handler) { BaseAddress = new Uri("http://fake-mywallet") };
    }

    public HttpClient CreateClient(string name = "") => _client;
}

public class FakeMyWalletHttpMessageHandler : HttpMessageHandler
{
    public const string ExpectedSessionToken = "fake-session-token";
    public const string PaymentToken = "payment-token-123";
    public const string Reference = "MW-REF-123";

    public List<string> BearerTokensUsed { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath?.Trim('/').ToLowerInvariant();
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

        if (path == "login")
        {
            return JsonResponse(new { token = ExpectedSessionToken });
        }

        if (request.Headers.Authorization is not AuthenticationHeaderValue auth || string.IsNullOrWhiteSpace(auth.Parameter))
        {
            return UnauthorizedResponse("Missing bearer token");
        }

        BearerTokensUsed.Add(auth.Parameter);

        if (path == "checkuser")
        {
            return JsonResponse(new { data = new { token = PaymentToken } });
        }

        if (path == "paymerchant")
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var token = doc.RootElement.GetProperty("token").GetString();
            var otp = doc.RootElement.GetProperty("otp").GetString();

            if (!string.Equals(token, PaymentToken, StringComparison.Ordinal))
            {
                return UnauthorizedResponse("Invalid payment token");
            }
            if (string.IsNullOrWhiteSpace(otp) || otp!.Length < 4)
            {
                return UnauthorizedResponse("Invalid OTP");
            }

            return JsonResponse(new { status = "success", reference = Reference, status_code = 200, errors = Array.Empty<string>() });
        }

        if (path == "checkstatus")
        {
            return JsonResponse(new { status = "success", reference = Reference, state = "success" });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No fake route configured for {path}")
        };
    }

    private static HttpResponseMessage JsonResponse(object payload)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }

    private static HttpResponseMessage UnauthorizedResponse(string message)
    {
        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(new { message })
        };
    }
}

public class MyWalletTestUserTests : IClassFixture<MyWalletApiFactory>
{
    private readonly MyWalletApiFactory _factory;

    public MyWalletTestUserTests(MyWalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Seeded_user_can_log_in_and_provides_known_mywallet_merchant_id()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MonoPayDbContext>();
        var seededUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == SeedData.MyWalletTestEmail);

        Assert.NotNull(seededUser);
        Assert.Equal(SeedData.MyWalletTestMerchantId, seededUser!.MerchantId);
        Assert.True(seededUser.IsVerified);
        Assert.Equal(PasswordHelper.HashPassword(SeedData.MyWalletTestPassword), seededUser.PasswordHash);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/users/login", new LoginRequest
        {
            Email = SeedData.MyWalletTestEmail,
            Password = SeedData.MyWalletTestPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(content);
        Assert.True(content!.ContainsKey("token"));
        Assert.False(string.IsNullOrWhiteSpace(content["token"]));
    }

    [Fact]
    public async Task Mywallet_payment_flow_uses_token_and_completes_successfully()
    {
        var client = _factory.CreateClient();

        // Authenticate as the seeded MyWallet test user
        var loginResponse = await client.PostAsJsonAsync("/v1/users/login", new LoginRequest
        {
            Email = SeedData.MyWalletTestEmail,
            Password = SeedData.MyWalletTestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(loginContent);
        var token = loginContent!["token"];
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var paymentRequest = new PaymentRequest
        {
            Amount = 250,
            Currency = "LSL",
            PaymentMethod = "mywallet",
            MerchantId = SeedData.MyWalletTestMerchantId,
            Customer = new CustomerInfo { Phone = "57661097" },
            Reference = "integration-test-ref",
            Otp = "123456"
        };

        var paymentResponse = await client.PostAsJsonAsync("/v1/payments", paymentRequest);
        Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);

        var createdPayment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(createdPayment);
        Assert.Equal("success", createdPayment!.Status);
        Assert.Equal("mywallet", createdPayment.PaymentMethod);
        Assert.Equal(paymentRequest.MerchantId, createdPayment.MerchantId);
        Assert.Equal(paymentRequest.Customer.Phone, createdPayment.CustomerPhone);
        Assert.False(string.IsNullOrWhiteSpace(createdPayment.Id));
        Assert.False(string.IsNullOrWhiteSpace(createdPayment.ProviderReference));
        Assert.NotNull(createdPayment.CompletedAt);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MonoPayDbContext>();
        var cacheEntry = await dbContext.MyWalletCacheEntries.FindAsync(createdPayment.Id);
        Assert.NotNull(cacheEntry);
        Assert.Equal(FakeMyWalletHttpMessageHandler.ExpectedSessionToken, cacheEntry!.SessionToken);

        var handler = scope.ServiceProvider.GetRequiredService<FakeMyWalletHttpMessageHandler>();
        Assert.Contains(FakeMyWalletHttpMessageHandler.ExpectedSessionToken, handler.BearerTokensUsed);
    }
}
