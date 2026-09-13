using CleanArch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CleanArch.Infrastructure.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect =
                await _dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy(
                    "Cannot connect to Azure SQL Database.");
        }
        catch (OperationCanceledException ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database health check timed out or was cancelled.",
                ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database health check threw an exception.",
                ex);
        }
    }
}