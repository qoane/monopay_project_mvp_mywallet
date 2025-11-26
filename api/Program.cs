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
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using System.Linq;

static string NormalizePathBase(string? pathBase)
{
    if (string.IsNullOrWhiteSpace(pathBase))
    {
        return string.Empty;
    }

    pathBase = pathBase.Trim();
    if (!pathBase.StartsWith("/"))
    {
        pathBase = $"/{pathBase}";
    }

    return pathBase.TrimEnd('/');
}

static string CombinePathSegments(params string[] segments)
{
    return string.Join('/', segments
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim('/')));
}

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json. This file contains provider
// endpoints and API keys, as well as email settings. In production you
// would use environment variables or a secure secret store.
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var pathBase = NormalizePathBase(builder.Configuration["PathBase"]);

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

if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Capture unhandled exceptions and return a structured JSON payload so
// operators can correlate reported 500 errors with server logs. This helps
// when deployments behave differently across environments.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var errorId = Guid.NewGuid().ToString("N");

        if (exceptionHandlerFeature?.Error != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalExceptionHandler");
            logger.LogError(exceptionHandlerFeature.Error, "Unhandled exception {ErrorId} on path {Path}", errorId, exceptionHandlerFeature.Path);
        }

        var payload = JsonSerializer.Serialize(new
        {
            message = "An unexpected error occurred. Please provide the errorId to support for investigation.",
            errorId
        });

        await context.Response.WriteAsync(payload);
    });
});

// Serve static files from the root. When no path base is configured, also expose
// the assets under /api for compatibility with earlier reverse-proxy setups.
var staticFileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot"));

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = staticFileProvider
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = staticFileProvider
});

if (string.IsNullOrEmpty(pathBase))
{
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
}

var swaggerRoutePrefix = CombinePathSegments("swagger");
var swaggerJsonRoute = CombinePathSegments(swaggerRoutePrefix, "{documentName}", "swagger.json");
var swaggerEndpointPath = CombinePathSegments(pathBase, swaggerRoutePrefix, "v1", "swagger.json");
var swaggerCustomCssPath = CombinePathSegments(pathBase, "swagger-ui", "custom.css");

app.UseSwagger(c =>
{
    c.RouteTemplate = swaggerJsonRoute;
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint($"/{swaggerEndpointPath}", "MonoPay API v1");
    options.RoutePrefix = swaggerRoutePrefix;
    options.DocumentTitle = "MonoPay API";
    // Inject our custom CSS to brand the Swagger UI
    options.InjectStylesheet($"/{swaggerCustomCssPath}");
});

app.UseRouting();
// Enable authentication and authorisation. Authentication must come
// before authorisation in the middleware pipeline.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
