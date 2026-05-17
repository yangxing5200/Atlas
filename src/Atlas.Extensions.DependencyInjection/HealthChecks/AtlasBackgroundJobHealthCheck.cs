using Atlas.Core.Enums;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasBackgroundJobHealthCheck : IHealthCheck
{
    private readonly AtlasGlobalDbContext _dbContext;

    public AtlasBackgroundJobHealthCheck(AtlasGlobalDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deadJobs = await _dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x => x.Status == BackgroundJobStatus.Dead)
            .CountAsync(cancellationToken);

        return deadJobs == 0
            ? HealthCheckResult.Healthy("No dead background jobs.")
            : HealthCheckResult.Degraded($"{deadJobs} background jobs are dead.");
    }
}
