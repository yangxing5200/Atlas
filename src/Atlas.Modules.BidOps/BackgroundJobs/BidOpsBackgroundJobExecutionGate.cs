using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsBackgroundJobExecutionGate : IBackgroundJobExecutionGate
{
    private static readonly TimeSpan PauseRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly ILogger<BidOpsBackgroundJobExecutionGate> _logger;

    public BidOpsBackgroundJobExecutionGate(
        IBidOpsRuntimeControlService runtimeControl,
        ILogger<BidOpsBackgroundJobExecutionGate> logger)
    {
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackgroundJobExecutionGateDecision> EvaluateAsync(
        BackgroundJob job,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!IsBidOpsJob(job) || job.TenantId is not > 0)
            return BackgroundJobExecutionGateDecision.Allow();

        try
        {
            if (!await _runtimeControl.IsTaskPausedAsync(job.TenantId.Value, ct))
                return BackgroundJobExecutionGateDecision.Allow();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "BidOps runtime task pause setting could not be read for tenant {TenantId}; deferring job {JobId}.",
                job.TenantId,
                job.Id);
            return BackgroundJobExecutionGateDecision.Defer(
                "BidOps runtime pause setting could not be read; job deferred.",
                DateTime.Now.Add(PauseRetryDelay));
        }

        return BackgroundJobExecutionGateDecision.Defer(
            "BidOps 全局任务暂停中，任务已延后等待恢复。",
            DateTime.Now.Add(PauseRetryDelay));
    }

    private static bool IsBidOpsJob(BackgroundJob job)
    {
        return job.Queue.Equals(BidOpsBackgroundJobQueues.BidOps, StringComparison.OrdinalIgnoreCase) ||
               job.JobType.StartsWith("bidops.", StringComparison.OrdinalIgnoreCase);
    }
}
