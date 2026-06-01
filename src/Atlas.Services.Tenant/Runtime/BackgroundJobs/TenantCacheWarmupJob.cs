using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services.Tenant.Runtime.BackgroundJobs;

public sealed record TenantCacheWarmupJobPayload(
    long TenantId,
    long? StoreId = null,
    string? Reason = null);

public static class TenantBackgroundJobQueues
{
    public const string Tenant = "tenant";
    public const string Maintenance = "maintenance";
}

public static class TenantBackgroundJobTypes
{
    public const string TenantCacheWarmup = "tenant.cache-warmup";
}

public sealed class TenantCacheWarmupJobHandler : IBackgroundJobHandler
{
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly ITenantDbContextFactory _tenantDbContextFactory;
    private readonly ILogger<TenantCacheWarmupJobHandler> _logger;

    public TenantCacheWarmupJobHandler(
        AtlasGlobalDbContext globalDbContext,
        ITenantDbContextFactory tenantDbContextFactory,
        ILogger<TenantCacheWarmupJobHandler> logger)
    {
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _tenantDbContextFactory = tenantDbContextFactory ?? throw new ArgumentNullException(nameof(tenantDbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => TenantBackgroundJobTypes.TenantCacheWarmup;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<TenantCacheWarmupJobPayload>();
        if (payload.TenantId <= 0)
            throw new InvalidOperationException("Tenant id is required for tenant cache warmup.");

        var tenantExists = await _globalDbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == payload.TenantId && !x.IsDeleted, ct);

        if (!tenantExists)
            throw new InvalidOperationException($"Tenant {payload.TenantId} does not exist.");

        var tenantDb = await _tenantDbContextFactory.GetDbContextAsync(payload.TenantId, ct);

        var storeQuery = tenantDb.Set<Store>()
            .AsNoTracking()
            .Where(x => x.TenantId == payload.TenantId && !x.IsDeleted);

        if (payload.StoreId.HasValue)
            storeQuery = storeQuery.Where(x => x.Id == payload.StoreId.Value);

        var warmedStores = await storeQuery
            .OrderBy(x => x.Id)
            .Take(20)
            .Select(x => new { x.Id, x.Name, x.Type })
            .ToListAsync(ct);

        var alreadyLogged = await tenantDb.Set<OperationLog>()
            .AnyAsync(
                x => x.TenantId == payload.TenantId &&
                     x.Module == "BackgroundJob" &&
                     x.OperationType == "TenantCacheWarmup" &&
                     x.EntityId == context.Job.Id,
                ct);

        if (!alreadyLogged)
        {
            tenantDb.Set<OperationLog>().Add(new OperationLog
            {
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                UserId = SystemIdentity.BackgroundJob.UserId,
                Module = "BackgroundJob",
                OperationType = "TenantCacheWarmup",
                Description = $"Warmed tenant cache for {warmedStores.Count} stores.",
                EntityId = context.Job.Id,
                Changes = JsonSerializer.Serialize(new
                {
                    payload.TenantId,
                    payload.StoreId,
                    payload.Reason,
                    Stores = warmedStores
                })
            });

            await tenantDb.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Tenant cache warmup job {JobId} completed for tenant {TenantId}; stores={StoreCount}.",
            context.Job.Id,
            payload.TenantId,
            warmedStores.Count);

        return BackgroundJobExecutionResult.Success(
            $"Warmed tenant {payload.TenantId}; stores={warmedStores.Count}.");
    }
}
