using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class OutcomeSupplierExtractJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsOutcomeSupplierExtractionService _extraction;
    private readonly IBidOpsReverseLifecycleClosureService _closure;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<OutcomeSupplierExtractJobHandler> _logger;
    private readonly IBackgroundJobProgressReporter? _progress;

    public OutcomeSupplierExtractJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOutcomeSupplierExtractionService extraction,
        IBidOpsReverseLifecycleClosureService closure,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<OutcomeSupplierExtractJobHandler> logger,
        IBackgroundJobProgressReporter? progress = null)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress;
    }

    public string JobType => BidOpsBackgroundJobTypes.OutcomeSupplierExtract;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<OutcomeSupplierExtractJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await RunWithProgressAsync(
            context,
            "outcome-supplier-extract",
            "AI 正在重新抽取中标/候选厂家线索",
            token => _extraction.ExtractRawNoticeAsync(payload.RawNoticeId, payload.ReviewerPrompt, token),
            payload,
            ct);

        BidOpsReverseClosureDebugResult? lifecycleRefresh = null;
        if (payload.RefreshLifecycleLinks && result.IsOutcomeNotice)
        {
            lifecycleRefresh = await _closure.ReverseCloseRawNoticeAndPersistAsync(payload.RawNoticeId, ct);
        }

        _logger.LogInformation(
            "BidOps outcome supplier extract job saved {SavedCount} records for raw notice {RawNoticeId}; reviewerPrompt={HasReviewerPrompt}; lifecycleRefresh={RefreshLifecycleLinks}.",
            result.SavedCount,
            payload.RawNoticeId,
            !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
            payload.RefreshLifecycleLinks);

        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(new
        {
            rawNoticeId = payload.RawNoticeId,
            projectCode = payload.ProjectCode,
            result.IsOutcomeNotice,
            result.ExtractedCount,
            result.SavedCount,
            result.BuyerCreatedCount,
            result.BuyerUpdatedCount,
            result.SupplierCreatedCount,
            result.SupplierUpdatedCount,
            result.Message,
            reviewerPrompt = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
            refreshLifecycleLinks = payload.RefreshLifecycleLinks,
            lifecycleRefresh = lifecycleRefresh == null
                ? null
                : new
                {
                    closureCount = lifecycleRefresh.Closures.Count,
                    persistedLifecycleLinkCount = lifecycleRefresh.PersistedLifecycleLinks.Count,
                    failureCount = lifecycleRefresh.Failures.Count,
                    warningCount = lifecycleRefresh.Warnings.Count
                },
            aiResponses = _diagnostics.Entries,
            deepSeekResponses = _diagnostics.Entries
        }, JsonOptions), BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }

    private Task<TResult> RunWithProgressAsync<TResult>(
        BackgroundJobExecutionContext context,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        OutcomeSupplierExtractJobPayload payload,
        CancellationToken ct)
    {
        if (_progress == null)
            return operation(ct);

        return _progress.RunWithHeartbeatAsync(
            context.Job.Id,
            stage,
            message,
            operation,
            new Dictionary<string, object?>
            {
                ["rawNoticeId"] = payload.RawNoticeId,
                ["projectCode"] = payload.ProjectCode,
                ["reviewerPrompt"] = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
                ["refreshLifecycleLinks"] = payload.RefreshLifecycleLinks
            },
            ct);
    }
}
