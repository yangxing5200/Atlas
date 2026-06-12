using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsSupplierMaintenanceTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<BidOpsSupplierMaintenanceTask> _logger;

    public BidOpsSupplierMaintenanceTask(
        IConfiguration configuration,
        IExecutionIdentityAccessor identityAccessor,
        IBackgroundJobClient jobs,
        ILogger<BidOpsSupplierMaintenanceTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "bidops.supplier-maintenance";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(
        1,
        _configuration.GetValue<int?>("BidOps:SupplierMaintenance:IntervalMinutes") ?? 60));

    public bool RunOnStartup => _configuration.GetValue<bool?>("BidOps:SupplierMaintenance:RunOnStartup") ?? true;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool?>("BidOps:SupplierMaintenance:Enabled").GetValueOrDefault(true))
            return;

        var tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "SupplierMaintenance");
        if (tenantIds.Count == 0)
            tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "Recovery");
        if (tenantIds.Count == 0)
            return;

        var userId = BidOpsBackgroundTenantConfiguration.GetUserId(_configuration, "SupplierMaintenance");
        var userName = BidOpsBackgroundTenantConfiguration.GetUserName(
            _configuration,
            "SupplierMaintenance",
            "BidOps Supplier Maintenance");
        var maxItems = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:SupplierMaintenance:MaxItemsPerJob") ?? 100,
            1,
            1000);
        var warningDays = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:SupplierMaintenance:EvidenceWarningDays") ?? 30,
            1,
            365);
        var now = DateTime.UtcNow;

        foreach (var tenantId in tenantIds)
        {
            using var identity = _identityAccessor.Begin(new ExecutionIdentitySnapshot(
                tenantId,
                StoreId: null,
                userId,
                userName,
                SessionId: null,
                IsAuthenticated: true));

            var result = await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<SupplierEvidenceExpiryScanJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.SupplierEvidenceExpiryScan,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps supplier evidence expiry scan",
                    TenantId = tenantId,
                    DeduplicationKey = $"bidops:supplier-maintenance:evidence-expiry:{tenantId}:{now:yyyyMMddHHmm}",
                    Payload = new SupplierEvidenceExpiryScanJobPayload(
                        tenantId,
                        StoreId: null,
                        userId,
                        userName,
                        maxItems,
                        warningDays)
                },
                ct);

            if (!result.AlreadyExists)
            {
                _logger.LogInformation(
                    "BidOps supplier maintenance enqueued evidence expiry scan for tenant {TenantId}.",
                    tenantId);
            }
        }
    }
}
