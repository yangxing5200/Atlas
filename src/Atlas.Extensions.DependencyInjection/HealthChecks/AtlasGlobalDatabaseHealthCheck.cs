using Atlas.Data.Global;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasGlobalDatabaseHealthCheck : IHealthCheck
{
    private readonly AtlasGlobalDbContext _dbContext;

    public AtlasGlobalDatabaseHealthCheck(AtlasGlobalDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Global database is reachable.")
                : HealthCheckResult.Unhealthy("Global database is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Global database health check failed.", ex);
        }
    }
}
