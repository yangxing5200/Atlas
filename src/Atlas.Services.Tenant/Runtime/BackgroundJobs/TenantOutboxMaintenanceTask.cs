using Atlas.BackgroundTasks;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Atlas.Services.Tenant.Runtime.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Services.Tenant.Runtime.BackgroundJobs;

public sealed class TenantOutboxMaintenanceOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int RetentionDays { get; set; } = 30;
    public int TenantBatchSize { get; set; } = 100;
    public int DeleteBatchSize { get; set; } = 500;
    public bool RunOnStartup { get; set; }
}

public sealed class TenantOutboxMaintenanceTask : IRecurringTask
{
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly ITenantOutboxStore _outboxStore;
    private readonly ITenantInboxStore _inboxStore;
    private readonly TenantOutboxMaintenanceOptions _options;
    private readonly ILogger<TenantOutboxMaintenanceTask> _logger;

    public TenantOutboxMaintenanceTask(
        AtlasGlobalDbContext globalDbContext,
        ITenantOutboxStore outboxStore,
        ITenantInboxStore inboxStore,
        IOptions<TenantOutboxMaintenanceOptions> options,
        ILogger<TenantOutboxMaintenanceTask> logger)
    {
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
        _options = options?.Value ?? new TenantOutboxMaintenanceOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "tenant-outbox-maintenance";
    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
    public bool RunOnStartup => _options.RunOnStartup;

    public async Task ExecuteAsync(RecurringTaskContext context, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Tenant outbox maintenance task is disabled.");
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));
        var tenantBatchSize = Math.Max(1, _options.TenantBatchSize);
        var deleteBatchSize = Math.Max(1, _options.DeleteBatchSize);
        var lastTenantId = 0L;
        var deletedOutbox = 0;
        var deletedInbox = 0;

        while (!ct.IsCancellationRequested)
        {
            var tenantIds = await _globalDbContext.Tenants
                .AsNoTracking()
                .Where(x =>
                    x.Id > lastTenantId &&
                    !x.IsDeleted &&
                    (x.Status == TenantStatus.Active || x.Status == TenantStatus.Trial))
                .OrderBy(x => x.Id)
                .Take(tenantBatchSize)
                .Select(x => x.Id)
                .ToListAsync(ct);

            if (tenantIds.Count == 0)
                break;

            foreach (var tenantId in tenantIds)
            {
                lastTenantId = tenantId;

                deletedOutbox += await _outboxStore.DeleteProcessedBeforeAsync(
                    tenantId,
                    cutoff,
                    deleteBatchSize,
                    ct);

                deletedInbox += await _inboxStore.DeleteReceivedBeforeAsync(
                    tenantId,
                    cutoff,
                    deleteBatchSize,
                    ct);
            }
        }

        _logger.LogInformation(
            "Tenant outbox maintenance completed; deletedOutbox={DeletedOutbox}, deletedInbox={DeletedInbox}, cutoff={Cutoff:O}.",
            deletedOutbox,
            deletedInbox,
            cutoff);
    }
}
