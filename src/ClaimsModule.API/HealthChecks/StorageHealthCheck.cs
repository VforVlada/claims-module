using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClaimsModule.API.HealthChecks;

public sealed class StorageHealthCheck(IStorageService storageService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await storageService.CheckHealthAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Storage provider is unreachable.", ex);
        }
    }
}
