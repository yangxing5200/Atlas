using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Atlas.Data.Global.Repositories;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GlobalTenant = Atlas.Core.Entities.Global.Tenant;

namespace Atlas.Services.Tenant.Runtime.Migrations;

public sealed class TenantSchemaMigrationService : ITenantSchemaMigrationService
{
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly ITenantDbConnProvider _tenantConnectionProvider;
    private readonly ITenantSchemaMigrationStateRepository _stateRepository;
    private readonly IAtlasTenantEntityConfigurationAssemblyProvider _entityConfigurationAssemblies;
    private readonly ILogger<TenantSchemaMigrationService> _logger;

    public TenantSchemaMigrationService(
        AtlasGlobalDbContext globalDbContext,
        ITenantDbConnProvider tenantConnectionProvider,
        ITenantSchemaMigrationStateRepository stateRepository,
        IAtlasTenantEntityConfigurationAssemblyProvider entityConfigurationAssemblies,
        ILogger<TenantSchemaMigrationService> logger)
    {
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _tenantConnectionProvider = tenantConnectionProvider ?? throw new ArgumentNullException(nameof(tenantConnectionProvider));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _entityConfigurationAssemblies = entityConfigurationAssemblies ?? throw new ArgumentNullException(nameof(entityConfigurationAssemblies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TenantSchemaMigrationPlanItem>> BuildPlanAsync(CancellationToken ct = default)
    {
        var plans = new List<TenantSchemaMigrationPlanItem>();

        foreach (var tenant in await ListMigratableTenantsAsync(0, int.MaxValue, ct))
        {
            ct.ThrowIfCancellationRequested();
            await using var tenantDbContext = await CreateTenantDbContextAsync(tenant.Id, ct);
            var applied = (await tenantDbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            var pending = (await tenantDbContext.Database.GetPendingMigrationsAsync(ct)).ToArray();
            plans.Add(new TenantSchemaMigrationPlanItem(
                tenant.Id,
                tenant.Name,
                applied.LastOrDefault(),
                pending.LastOrDefault() ?? applied.LastOrDefault(),
                pending));
        }

        return plans;
    }

    public async Task<TenantSchemaMigrationBatchResult> ExecuteAsync(
        TenantSchemaMigrationOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DryRun)
        {
            var plan = await BuildPlanAsync(ct);
            return new TenantSchemaMigrationBatchResult(
                true,
                plan.Select(item => new TenantSchemaMigrationResult(
                    item.TenantId,
                    item.PendingMigrations.Count == 0
                        ? TenantSchemaMigrationStatus.Skipped
                        : TenantSchemaMigrationStatus.Pending,
                    item.CurrentVersion,
                    item.TargetVersion,
                    null)).ToArray());
        }

        var results = new List<TenantSchemaMigrationResult>();
        var lastTenantId = 0L;
        var batchSize = Math.Max(1, options.TenantBatchSize);

        while (!ct.IsCancellationRequested)
        {
            var tenants = await ListMigratableTenantsAsync(lastTenantId, batchSize, ct);
            if (tenants.Count == 0)
                break;

            foreach (var tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                results.Add(await ExecuteTenantAsync(tenant, ct));
                lastTenantId = tenant.Id;
            }
        }

        return new TenantSchemaMigrationBatchResult(false, results);
    }

    private async Task<TenantSchemaMigrationResult> ExecuteTenantAsync(GlobalTenant tenant, CancellationToken ct)
    {
        var state = await GetOrCreateStateAsync(tenant.Id, ct);
        var now = DateTime.UtcNow;

        try
        {
            await using var tenantDbContext = await CreateTenantDbContextAsync(tenant.Id, ct);
            var appliedBefore = (await tenantDbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            var pending = (await tenantDbContext.Database.GetPendingMigrationsAsync(ct)).ToArray();
            var targetVersion = pending.LastOrDefault() ?? appliedBefore.LastOrDefault();

            state.CurrentVersion = appliedBefore.LastOrDefault();
            state.TargetVersion = targetVersion;
            state.LastAttemptedAtUtc = now;
            state.UpdatedAtUtc = now;
            state.UpdatedAt = now;

            if (pending.Length == 0)
            {
                state.Status = TenantSchemaMigrationStatus.Skipped;
                state.LastError = null;
                await _globalDbContext.SaveChangesAsync(ct);
                return new TenantSchemaMigrationResult(
                    tenant.Id,
                    state.Status,
                    state.CurrentVersion,
                    state.TargetVersion,
                    null);
            }

            state.Status = TenantSchemaMigrationStatus.Running;
            await _globalDbContext.SaveChangesAsync(ct);

            await tenantDbContext.Database.MigrateAsync(ct);

            var appliedAfter = (await tenantDbContext.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            state.CurrentVersion = appliedAfter.LastOrDefault();
            state.TargetVersion = targetVersion;
            state.Status = TenantSchemaMigrationStatus.Succeeded;
            state.LastError = null;
            state.UpdatedAtUtc = DateTime.UtcNow;
            state.UpdatedAt = state.UpdatedAtUtc;
            await _globalDbContext.SaveChangesAsync(ct);

            return new TenantSchemaMigrationResult(
                tenant.Id,
                state.Status,
                state.CurrentVersion,
                state.TargetVersion,
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            state.Status = TenantSchemaMigrationStatus.Failed;
            state.RetryCount++;
            state.LastError = Truncate(ex.ToString(), 2000);
            state.UpdatedAtUtc = DateTime.UtcNow;
            state.UpdatedAt = state.UpdatedAtUtc;
            await _globalDbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex, "Tenant schema migration failed for tenant {TenantId}.", tenant.Id);
            return new TenantSchemaMigrationResult(
                tenant.Id,
                state.Status,
                state.CurrentVersion,
                state.TargetVersion,
                state.LastError);
        }
    }

    private async Task<TenantSchemaMigrationState> GetOrCreateStateAsync(long tenantId, CancellationToken ct)
    {
        var state = await _stateRepository.GetByTenantIdAsync(tenantId, ct);
        if (state != null)
            return state;

        var now = DateTime.UtcNow;
        state = new TenantSchemaMigrationState
        {
            Id = tenantId,
            TenantId = tenantId,
            Status = TenantSchemaMigrationStatus.Pending,
            CreatedAt = now,
            UpdatedAtUtc = now
        };

        await _stateRepository.AddAsync(state, ct);
        return state;
    }

    private async Task<List<GlobalTenant>> ListMigratableTenantsAsync(
        long lastTenantId,
        int batchSize,
        CancellationToken ct)
    {
        return await _globalDbContext.Tenants
            .AsNoTracking()
            .Where(x =>
                x.Id > lastTenantId &&
                !x.IsDeleted &&
                (x.Status == TenantStatus.Active || x.Status == TenantStatus.Trial))
            .OrderBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    private async Task<AtlasTenantDbContext> CreateTenantDbContextAsync(
        long tenantId,
        CancellationToken ct)
    {
        var connectionString = await _tenantConnectionProvider.GetConnStringAsync(tenantId, ct);
        var options = new DbContextOptionsBuilder<AtlasTenantDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly("Atlas.Data.Tenant.Migrations"))
            .Options;

        return new AtlasTenantDbContext(options, _entityConfigurationAssemblies.Assemblies);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
