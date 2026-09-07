using CleanArch.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace CleanArch.Infrastructure.HealthChecks;

/// <summary>
/// Explicit DB health check beyond raw connectivity — verifies EF Core can actually query the schema.
/// Distinguishes "SQL reachable" from "SQL reachable AND migrations applied", which matters for AKS readiness probes.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseHealthCheck(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
                return HealthCheckResult.Unhealthy("Cannot connect to Azure SQL Database.");

            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Any())
                return HealthCheckResult.Degraded($"{pendingMigrations.Count} pending migration(s) not applied.");

            return HealthCheckResult.Healthy("Database reachable and schema up to date.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}
