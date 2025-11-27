using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;
using MonoPayAggregator.Controllers;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
                ["UseInMemoryDatabase"] = "true"
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
        });
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
}
