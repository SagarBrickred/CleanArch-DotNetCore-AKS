using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CleanArch.API.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Maps three distinct probes for AKS:
    /// - /health/live   -> liveness: process is up (no dependency checks; avoids cascading restarts on transient DB blips)
    /// - /health/ready  -> readiness: DB + Key Vault reachable (tagged "ready"); pod is pulled from Service endpoints if this fails
    /// - /health/startup -> startup probe alias of readiness, gives slow-starting pods (EF migrations, JIT warmup) more time
    /// </summary>
    public static WebApplication MapProductionHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // liveness never touches dependencies
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }
}
