using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasCacheHealthCheck : IHealthCheck
{
    private readonly ICacheService _cacheService;

    public AtlasCacheHealthCheck(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _cacheService.GetStatisticsAsync(cancellationToken);
            return HealthCheckResult.Healthy("Cache service is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache service health check failed.", ex);
        }
    }
}
