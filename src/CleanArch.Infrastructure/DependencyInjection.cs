using Azure.Identity;
using CleanArch.Application.Interfaces;
using CleanArch.Infrastructure.HealthChecks;
using CleanArch.Infrastructure.Persistence;
using CleanArch.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;


namespace CleanArch.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core against Azure SQL with resiliency (retry-on-transient-failure via EnableRetryOnFailure,
    /// plus a query timeout), Key-Vault-aware config, repositories, health checks, and the current-user service.
    /// The connection string is expected to already be resolved into IConfiguration by the time this runs —
    /// see Program.cs, which adds the Key Vault configuration provider before this method is called.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not configured.");
        //var connectionString = configuration["SqlConnectionString"]
        //    ?? throw new InvalidOperationException(
        //        "Connection string 'SqlConnectionString' was not found. " +
        //        "In production this must come from Key Vault secret 'SqlConnectionString'; " +
        //        "in local dev, set it in appsettings.Development.json or user-secrets.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null); // uses SqlServer's built-in transient error list (deadlocks, timeouts, throttling)
                sqlOptions.CommandTimeout(30);
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            }));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHealthChecks()
    .AddSqlServer(
        connectionString,
        name: "sql-connectivity",
        tags: new[] { "ready" },
        timeout: TimeSpan.FromSeconds(5));

        return services;
    }
}
