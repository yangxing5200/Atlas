using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class StructuredParseJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsAiParsingService _parsing;
    private readonly IBidOpsOutcomeSupplierExtractionService _outcomeSupplierExtraction;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<StructuredParseJobHandler> _logger;
    private readonly IBackgroundJobProgressReporter? _progress;

    public StructuredParseJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsAiParsingService parsing,
        IBidOpsOutcomeSupplierExtractionService outcomeSupplierExtraction,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<StructuredParseJobHandler> logger,
        IBackgroundJobProgressReporter? progress = null)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _parsing = parsing ?? throw new ArgumentNullException(nameof(parsing));
        _outcomeSupplierExtraction = outcomeSupplierExtraction ?? throw new ArgumentNullException(nameof(outcomeSupplierExtraction));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress;
    }

    public string JobType => BidOpsBackgroundJobTypes.StructuredParse;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<StructuredParseJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        long reviewTaskId;
        try
        {
            reviewTaskId = await RunWithProgressAsync(
                context,
                "notice-structured-parse",
                "AI 正在结构化解析公告",
                token => _parsing.ParseRawNoticeAsync(payload.RawNoticeId, payload.ReviewerPrompt, token),
                payload,
                ct);
        }
        catch
        {
            await TryExtractOutcomeSuppliersAsync(context, payload.RawNoticeId, ct);
            throw;
        }

        _logger.LogInformation(
            "BidOps structured parse generated review task {ReviewTaskId} for raw notice {RawNoticeId}.",
            reviewTaskId,
            payload.RawNoticeId);

        var outcome = await TryExtractOutcomeSuppliersAsync(context, payload.RawNoticeId, ct);
        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(new
        {
            rawNoticeId = payload.RawNoticeId,
            projectCode = payload.ProjectCode,
            reviewTaskId,
            reviewerPrompt = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
            outcomeSupplierExtraction = outcome,
            aiResponses = _diagnostics.Entries,
            deepSeekResponses = _diagnostics.Entries
        }, JsonOptions), BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }

    private async Task<OutcomeSupplierExtractionResultDto?> TryExtractOutcomeSuppliersAsync(
        BackgroundJobExecutionContext context,
        long rawNoticeId,
        CancellationToken ct)
    {
        try
        {
            return await RunWithProgressAsync(
                context,
                "outcome-supplier-extract",
                "AI 正在抽取中标/候选厂家线索",
                token => _outcomeSupplierExtraction.ExtractRawNoticeAsync(rawNoticeId, token),
                rawNoticeId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "BidOps structured parse could not extract outcome supplier records for raw notice {RawNoticeId}.",
                rawNoticeId);
            return null;
        }
    }

    private Task<TResult> RunWithProgressAsync<TResult>(
        BackgroundJobExecutionContext context,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        StructuredParseJobPayload payload,
        CancellationToken ct)
    {
        return RunWithProgressAsync(
            context,
            stage,
            message,
            operation,
            payload.RawNoticeId,
            ct,
            new Dictionary<string, object?>
            {
                ["rawNoticeId"] = payload.RawNoticeId,
                ["projectCode"] = payload.ProjectCode,
                ["reviewerPrompt"] = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt)
            });
    }

    private Task<TResult> RunWithProgressAsync<TResult>(
        BackgroundJobExecutionContext context,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        long rawNoticeId,
        CancellationToken ct,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (_progress == null)
            return operation(ct);

        return _progress.RunWithHeartbeatAsync(
            context.Job.Id,
            stage,
            message,
            operation,
            data ?? new Dictionary<string, object?> { ["rawNoticeId"] = rawNoticeId },
            ct);
    }
}
