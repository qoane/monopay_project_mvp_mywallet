using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json. This file contains provider
// endpoints and API keys, as well as email settings. In production you
// would use environment variables or a secure secret store.
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure database context. Use SQL Server when a connection string is
// available, otherwise fall back to an in-memory database to keep the API
// functional in local or test environments where SQL Server is unavailable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDatabase") ||
                   string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddDbContext<MonoPayAggregator.Data.MonoPayDbContext>(options =>
{
    if (useInMemoryDb)
    {
        options.UseInMemoryDatabase("MonoPayDb");
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Register HttpClient factory for calling external APIs (e.g. MyWallet)
builder.Services.AddHttpClient();
// Add services to the container.
// Use the default System.Text.Json serializer. The call to
// AddNewtonsoftJson() required an additional package (Microsoft.AspNetCore.Mvc.NewtonsoftJson)
// which isn't referenced in this project. Removing the call avoids a
// compile‑time error and falls back to the built‑in JSON serializer.
builder.Services.AddControllers();

// Add Swagger services. This uses Swashbuckle to generate OpenAPI docs
// and configure the Swagger UI. A JWT bearer security scheme is defined
// so that authenticated endpoints can be tested from the UI.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MonoPay Aggregator API",
        Version = "v1",
        Description = "API documentation for the MonoPay payment aggregator."
    });
    // Define bearer auth scheme
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your Bearer token in the format `Bearer {token}`"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Bind configuration sections to strongly typed option classes
builder.Services.Configure<MonoPayAggregator.Models.EmailOptions>(builder.Configuration.GetSection("EmailSettings"));

// Register email service (dummy implementation). Replace SmtpEmailService with
// a real implementation when ready to send actual emails.
builder.Services.AddSingleton<MonoPayAggregator.Services.IEmailService, MonoPayAggregator.Services.SmtpEmailService>();

// Configure authentication using JWT bearer tokens. The signing key and other
// properties are bound from the Jwt section of appsettings.json.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!))
        };
    });
builder.Services.AddAuthorization();

// Register payment providers and aggregator. In a production system these
// registrations would be more dynamic and configurable via appsettings.json.
builder.Services.AddSingleton<MonoPayAggregator.Services.MpesaProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.EcoCashProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.EftProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.CardProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.MyWalletProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.CpayProvider>();
builder.Services.AddSingleton<MonoPayAggregator.Services.KhetsiProvider>();

// Register the payment aggregator. We build the provider dictionary from
// configuration keys so that supported methods can be toggled in
// appsettings.json. Unknown methods remain unsupported until you add
// additional provider classes.
builder.Services.AddSingleton<MonoPayAggregator.Services.PaymentAggregator>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var providerSection = config.GetSection("PaymentProviders");
    var providersDict = new Dictionary<string, MonoPayAggregator.Services.IPaymentProvider>(StringComparer.OrdinalIgnoreCase);
    foreach (var child in providerSection.GetChildren())
    {
        var key = child.Key.ToLowerInvariant();
        switch (key)
        {
            case "mpesa":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.MpesaProvider>();
                break;
            case "ecocash":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.EcoCashProvider>();
                break;
            case "eft":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.EftProvider>();
                break;
            case "card":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.CardProvider>();
                break;
            case "mywallet":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.MyWalletProvider>();
                break;
            case "cpay":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.CpayProvider>();
                break;
            case "khetsi":
                providersDict[key] = sp.GetRequiredService<MonoPayAggregator.Services.KhetsiProvider>();
                break;
            // Additional providers go here
        }
    }
    return new MonoPayAggregator.Services.PaymentAggregator(providersDict);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Serve static files from the root and also under /api so reverse-proxy
// configurations that mount the app at /api can still reach the HTML docs.
app.UseDefaultFiles();
app.UseStaticFiles();

var staticFileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = staticFileProvider,
    RequestPath = "/api"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = staticFileProvider,
    RequestPath = "/api"
});

// Enable Swagger middleware. Route the generated OpenAPI document under /api/swagger
app.UseSwagger(c =>
{
    c.RouteTemplate = "api/swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "MonoPay API v1");
    options.RoutePrefix = "api/swagger";
    options.DocumentTitle = "MonoPay API";
    // Inject our custom CSS to brand the Swagger UI
    options.InjectStylesheet("/swagger-ui/custom.css");
});

app.UseRouting();
// Enable authentication and authorisation. Authentication must come
// before authorisation in the middleware pipeline.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
