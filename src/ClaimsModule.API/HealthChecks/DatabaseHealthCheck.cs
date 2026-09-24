using ClaimsModule.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClaimsModule.API.HealthChecks;

public sealed class DatabaseHealthCheck(ClaimsDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the database.");
    }
}
