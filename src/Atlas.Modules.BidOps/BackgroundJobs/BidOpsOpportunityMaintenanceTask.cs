using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsOpportunityMaintenanceTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBackgroundJobClient _jobs;
    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly ILogger<BidOpsOpportunityMaintenanceTask> _logger;

    public BidOpsOpportunityMaintenanceTask(
        IConfiguration configuration,
        IExecutionIdentityAccessor identityAccessor,
        IBackgroundJobClient jobs,
        IBidOpsRuntimeControlService runtimeControl,
        ILogger<BidOpsOpportunityMaintenanceTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "bidops.opportunity-maintenance";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(
        1,
        _configuration.GetValue<int?>("BidOps:OpportunityMaintenance:IntervalMinutes") ?? 30));

    public bool RunOnStartup => _configuration.GetValue<bool?>("BidOps:OpportunityMaintenance:RunOnStartup") ?? true;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool?>("BidOps:OpportunityMaintenance:Enabled").GetValueOrDefault(true))
            return;

        var tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "OpportunityMaintenance");
        if (tenantIds.Count == 0)
            tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "Recovery");
        if (tenantIds.Count == 0)
            tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "ScheduledScan");
        if (tenantIds.Count == 0)
            return;

        var userId = BidOpsBackgroundTenantConfiguration.GetUserId(_configuration, "OpportunityMaintenance");
        var userName = BidOpsBackgroundTenantConfiguration.GetUserName(
            _configuration,
            "OpportunityMaintenance",
            "BidOps Opportunity Maintenance");
        var maxItems = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:OpportunityMaintenance:MaxItemsPerJob") ?? 100,
            1,
            1000);
        var deadlineWarningDays = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:OpportunityMaintenance:DeadlineWarningDays") ?? 7,
            1,
            60);
        var staleDays = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:OpportunityMaintenance:StaleDays") ?? 14,
            1,
            365);
        var now = DateTime.UtcNow;

        foreach (var tenantId in tenantIds)
        {
            if (await _runtimeControl.IsTaskPausedAsync(tenantId, ct))
            {
                _logger.LogInformation("BidOps opportunity maintenance skipped for tenant {TenantId} because global task pause is enabled.", tenantId);
                continue;
            }

            using var identity = _identityAccessor.Begin(new ExecutionIdentitySnapshot(
                tenantId,
                StoreId: null,
                userId,
                userName,
                SessionId: null,
                IsAuthenticated: true));

            var payload = new OpportunityMaintenanceJobPayload(
                tenantId,
                StoreId: null,
                userId,
                userName,
                maxItems,
                deadlineWarningDays,
                staleDays);

            var enqueued = 0;
            if (await EnqueueAsync(
                BidOpsBackgroundJobTypes.OpportunityValueAssessment,
                "BidOps opportunity value assessment",
                payload,
                tenantId,
                now,
                ct))
            {
                enqueued++;
            }

            if (await EnqueueAsync(
                BidOpsBackgroundJobTypes.OpportunityDeadlineReminder,
                "BidOps opportunity deadline reminder scan",
                payload,
                tenantId,
                now,
                ct))
            {
                enqueued++;
            }

            if (await EnqueueAsync(
                BidOpsBackgroundJobTypes.OpportunityWatchReminder,
                "BidOps opportunity watch reminder scan",
                payload,
                tenantId,
                now,
                ct))
            {
                enqueued++;
            }

            if (await EnqueueAsync(
                BidOpsBackgroundJobTypes.OpportunityStaleStateScan,
                "BidOps opportunity stale state scan",
                payload,
                tenantId,
                now,
                ct))
            {
                enqueued++;
            }

            if (enqueued > 0)
            {
                _logger.LogInformation(
                    "BidOps opportunity maintenance enqueued {Count} jobs for tenant {TenantId}.",
                    enqueued,
                    tenantId);
            }
        }
    }

    private async Task<bool> EnqueueAsync(
        string jobType,
        string jobName,
        OpportunityMaintenanceJobPayload payload,
        long tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<OpportunityMaintenanceJobPayload>
            {
                JobType = jobType,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = jobName,
                TenantId = tenantId,
                StoreId = payload.StoreId,
                DeduplicationKey = $"bidops:opportunity-maintenance:{jobType}:{tenantId}:{now:yyyyMMddHHmm}",
                Payload = payload
            },
            ct);
        return !result.AlreadyExists;
    }
}
