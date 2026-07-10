using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class OutcomeSupplierRebuildDryRunJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsOutcomeSupplierExtractionService _extraction;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<OutcomeSupplierRebuildDryRunJobHandler> _logger;
    private readonly IBackgroundJobProgressReporter? _progress;

    public OutcomeSupplierRebuildDryRunJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOutcomeSupplierExtractionService extraction,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<OutcomeSupplierRebuildDryRunJobHandler> logger,
        IBackgroundJobProgressReporter? progress = null)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress;
    }

    public string JobType => BidOpsBackgroundJobTypes.OutcomeSupplierRebuildDryRun;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<OutcomeSupplierRebuildDryRunJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await RunWithProgressAsync(
            context,
            "outcome-supplier-rebuild-dry-run",
            "AI 正在预览重建中标/候选厂家线索",
            token => _extraction.DryRunRawNoticeAsync(payload.RawNoticeId, payload.ReviewerPrompt, token),
            payload,
            ct);

        _logger.LogInformation(
            "BidOps outcome supplier rebuild dry-run completed for raw notice {RawNoticeId}; existing={ExistingCount}; previewSaved={PreviewSavedCount}; delta={DeltaCount}; candidateCount={CandidateCount}; mergeGroups={MergeGroupCount}.",
            payload.RawNoticeId,
            result.ExistingCount,
            result.PreviewSavedCount,
            result.DeltaCount,
            result.CandidateCount,
            result.MergeGroupCount);

        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(new
        {
            rawNoticeId = payload.RawNoticeId,
            projectCode = payload.ProjectCode,
            result.DryRun,
            result.IsOutcomeNotice,
            result.ExistingCount,
            result.PreviewExtractedCount,
            result.PreviewSavedCount,
            result.CandidateCount,
            result.MergeGroupCount,
            result.MergedCandidateCount,
            result.DeltaCount,
            result.SourceCounts,
            result.LotNoValidationCounts,
            result.StrengthCounts,
            result.Message,
            reviewerPrompt = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
            aiResponses = _diagnostics.Entries,
            deepSeekResponses = _diagnostics.Entries
        }, JsonOptions), BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }

    private Task<TResult> RunWithProgressAsync<TResult>(
        BackgroundJobExecutionContext context,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        OutcomeSupplierRebuildDryRunJobPayload payload,
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
                ["dryRun"] = true
            },
            ct);
    }
}
