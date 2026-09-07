using Asp.Versioning;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CleanArch.API.Extensions;
using CleanArch.API.Middleware;
using CleanArch.Application;
using CleanArch.Infrastructure;
using CleanArch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// ============================================================================
// 1. CONFIGURATION — Azure Key Vault via Managed Identity (no secrets in appsettings, ever)
// ============================================================================
// In AKS this resolves via Workload Identity / the Key Vault CSI driver's federated credential.
// Locally, DefaultAzureCredential falls back to `az login` / Visual Studio / environment vars.
// KeyVault:Uri itself is NOT a secret — it's fine in appsettings.Production.json or an env var.
var keyVaultUri = builder.Configuration["KeyVault:Uri"]
    ?? throw new InvalidOperationException(
        "KeyVault:Uri is not configured.");
TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new DefaultAzureCredential();

var secretClient = new SecretClient(
    new Uri(keyVaultUri),
    credential);

KeyVaultSecret sqlSecret;
KeyVaultSecret appInsightsSecret;

try
{
    var sqlResponse = await secretClient.GetSecretAsync("SqlConnectionString");
    sqlSecret = sqlResponse.Value;

    var appInsightsResponse =
        await secretClient.GetSecretAsync("ApplicationInsightsConnectionString");
    appInsightsSecret = appInsightsResponse.Value;
}
catch (Exception ex)
{
    throw new InvalidOperationException(
        "Unable to retrieve required secrets from Azure Key Vault.",
        ex);
}

if (string.IsNullOrWhiteSpace(sqlSecret.Value))
{
    throw new InvalidOperationException(
        "Key Vault secret 'SqlConnectionString' is empty.");
}

if (string.IsNullOrWhiteSpace(appInsightsSecret.Value))
{
    throw new InvalidOperationException(
        "Key Vault secret 'ApplicationInsightsConnectionString' is empty.");
}

// Make secrets available to the rest of the application.
builder.Configuration["ConnectionStrings:DefaultConnection"] =
    sqlSecret.Value;

builder.Configuration["ApplicationInsights:ConnectionString"] =
    appInsightsSecret.Value;
//KeyVaultSecret sqlSecret;

//try
//{
//    var response = await secretClient.GetSecretAsync("SqlConnectionString");
//    sqlSecret = response.Value;
//}
//catch (Exception ex)
//{
//    throw new InvalidOperationException(
//        "Unable to retrieve 'SqlConnectionString' from Azure Key Vault.",
//        ex);
//}

//if (string.IsNullOrWhiteSpace(sqlSecret.Value))
//{
//    throw new InvalidOperationException(
//        "Key Vault secret 'SqlConnectionString' is empty.");
//}

//// Make the secret available to the rest of the application.
//builder.Configuration["ConnectionStrings:DefaultConnection"] =
//    sqlSecret.Value;

Console.WriteLine("========================================");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine("Key Vault: Connected");
Console.WriteLine("SqlConnectionString: Retrieved from Key Vault");
Console.WriteLine("ApplicationInsights: ConnectionString retrieved from Key Vault");
Console.WriteLine("========================================");
//if (!string.IsNullOrWhiteSpace(keyVaultUri) && !builder.Environment.IsDevelopment())
//{
//    builder.Configuration.AddAzureKeyVault(
//        new Uri(keyVaultUri),
//        new AzureCliCredential());
//    // Secrets expected in the vault (mapped to config keys via Key Vault secret naming, "--" -> ":"):
//    //   ConnectionStrings--DefaultConnection   -> ConnectionStrings:DefaultConnection
//    //   Jwt--Authority, Jwt--Audience          -> Jwt:Authority / Jwt:Audience
//    //   ApplicationInsights--ConnectionString  -> ApplicationInsights:ConnectionString
//}
//var sqlSecret = builder.Configuration["SqlConnectionString"];

//Console.WriteLine("========================================");
//Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
//Console.WriteLine($"KeyVault URI: {keyVaultUri}");
//Console.WriteLine($"SqlConnectionString loaded: {!string.IsNullOrWhiteSpace(sqlSecret)}");
//Console.WriteLine("========================================");

//if (string.IsNullOrWhiteSpace(sqlSecret))
//{
//    throw new InvalidOperationException(
//        "Key Vault provider loaded, but secret 'SqlConnectionString' is not available.");
//}
// ============================================================================
// 2. LOGGING — Serilog -> Console (captured by AKS/Container Insights) + Application Insights
// ============================================================================
builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Service", "CleanArch.API")
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

    var aiConnectionString = context.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
    {
        loggerConfig.WriteTo.ApplicationInsights(aiConnectionString, TelemetryConverter.Traces);
    }
});

// ============================================================================
// 3. APPLICATION INSIGHTS — distributed tracing, dependency tracking (SQL calls), live metrics
// ============================================================================
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = false
    ;
    options.EnableHeartbeat = true;
});

// ============================================================================
// 4. CLEAN ARCHITECTURE LAYERS
// ============================================================================
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ============================================================================
// 5. AUTHENTICATION / AUTHORIZATION — Azure AD (Entra ID) JWT bearer
// ============================================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];   // e.g. https://login.microsoftonline.com/{tenantId}/v2.0
        options.Audience = builder.Configuration["Jwt:Audience"];     // API's App Registration client id / App ID URI
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
    });
builder.Services.AddAuthorization();

// ============================================================================
// 6. API VERSIONING
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ============================================================================
// 7. RATE LIMITING — protects against abuse/DoS at the app layer (in addition to WAF/ingress limits)
// ============================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// ============================================================================
// 8. CORS — explicit allow-list only; never AllowAnyOrigin in production
// ============================================================================
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        else
            policy.DisallowCredentials(); // no origins configured -> effectively closed
    });
});

// ============================================================================
// 9. CONTROLLERS / SWAGGER
// ============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CleanArch Product API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ============================================================================
// 10. STARTUP MIGRATION — apply pending EF migrations with a bounded retry (handles SQL cold-start / DTU throttling)
//     Guarded by config so it can be disabled in favor of a dedicated migration job/pipeline stage if preferred.
// ============================================================================
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, delay, attempt, _) => Log.Warning(ex, "Migration attempt {Attempt} failed, retrying in {Delay}s", attempt, delay.TotalSeconds));

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await retryPolicy.ExecuteAsync(() => db.Database.MigrateAsync());
}

// ============================================================================
// 11. MIDDLEWARE PIPELINE
// ============================================================================
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
//app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
//app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ProductionCorsPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // GlobalLimiter above already applies rate limiting to every endpoint
app.MapProductionHealthChecks();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
