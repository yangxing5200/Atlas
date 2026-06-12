using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public abstract class OpportunityMaintenanceJobHandlerBase : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;

    protected OpportunityMaintenanceJobHandlerBase(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOpportunityMaintenanceService maintenance)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        Maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
    }

    protected IBidOpsOpportunityMaintenanceService Maintenance { get; }

    public abstract string JobType { get; }

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<OpportunityMaintenanceJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);
        var result = await ExecuteCoreAsync(payload, ct);
        return BackgroundJobExecutionResult.Success(result.ToJobResult());
    }

    protected abstract Task<BidOpsOpportunityMaintenanceResult> ExecuteCoreAsync(
        OpportunityMaintenanceJobPayload payload,
        CancellationToken ct);
}

public sealed class OpportunityValueAssessmentJobHandler : OpportunityMaintenanceJobHandlerBase
{
    public OpportunityValueAssessmentJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOpportunityMaintenanceService maintenance)
        : base(identityAccessor, maintenance)
    {
    }

    public override string JobType => BidOpsBackgroundJobTypes.OpportunityValueAssessment;

    protected override Task<BidOpsOpportunityMaintenanceResult> ExecuteCoreAsync(
        OpportunityMaintenanceJobPayload payload,
        CancellationToken ct)
    {
        return Maintenance.RunValueAssessmentAsync(payload.MaxItems, ct);
    }
}

public sealed class OpportunityDeadlineReminderJobHandler : OpportunityMaintenanceJobHandlerBase
{
    public OpportunityDeadlineReminderJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOpportunityMaintenanceService maintenance)
        : base(identityAccessor, maintenance)
    {
    }

    public override string JobType => BidOpsBackgroundJobTypes.OpportunityDeadlineReminder;

    protected override Task<BidOpsOpportunityMaintenanceResult> ExecuteCoreAsync(
        OpportunityMaintenanceJobPayload payload,
        CancellationToken ct)
    {
        return Maintenance.RunDeadlineReminderScanAsync(
            payload.MaxItems,
            payload.DeadlineWarningDays,
            ct);
    }
}

public sealed class OpportunityWatchReminderJobHandler : OpportunityMaintenanceJobHandlerBase
{
    public OpportunityWatchReminderJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOpportunityMaintenanceService maintenance)
        : base(identityAccessor, maintenance)
    {
    }

    public override string JobType => BidOpsBackgroundJobTypes.OpportunityWatchReminder;

    protected override Task<BidOpsOpportunityMaintenanceResult> ExecuteCoreAsync(
        OpportunityMaintenanceJobPayload payload,
        CancellationToken ct)
    {
        return Maintenance.RunWatchReminderScanAsync(
            payload.MaxItems,
            payload.DeadlineWarningDays,
            ct);
    }
}

public sealed class OpportunityStaleStateScanJobHandler : OpportunityMaintenanceJobHandlerBase
{
    public OpportunityStaleStateScanJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOpportunityMaintenanceService maintenance)
        : base(identityAccessor, maintenance)
    {
    }

    public override string JobType => BidOpsBackgroundJobTypes.OpportunityStaleStateScan;

    protected override Task<BidOpsOpportunityMaintenanceResult> ExecuteCoreAsync(
        OpportunityMaintenanceJobPayload payload,
        CancellationToken ct)
    {
        return Maintenance.RunStaleStateScanAsync(
            payload.MaxItems,
            payload.StaleDays,
            ct);
    }
}
