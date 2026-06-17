using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai;
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
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<OutcomeSupplierExtractJobHandler> _logger;

    public OutcomeSupplierExtractJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOutcomeSupplierExtractionService extraction,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<OutcomeSupplierExtractJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.OutcomeSupplierExtract;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<OutcomeSupplierExtractJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _extraction.ExtractRawNoticeAsync(payload.RawNoticeId, payload.ReviewerPrompt, ct);
        _logger.LogInformation(
            "BidOps outcome supplier extract job saved {SavedCount} records for raw notice {RawNoticeId}; reviewerPrompt={HasReviewerPrompt}.",
            result.SavedCount,
            payload.RawNoticeId,
            !string.IsNullOrWhiteSpace(payload.ReviewerPrompt));

        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(new
        {
            rawNoticeId = payload.RawNoticeId,
            result.IsOutcomeNotice,
            result.ExtractedCount,
            result.SavedCount,
            result.BuyerCreatedCount,
            result.BuyerUpdatedCount,
            result.SupplierCreatedCount,
            result.SupplierUpdatedCount,
            result.Message,
            reviewerPrompt = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
            deepSeekResponses = _diagnostics.Entries
        }, JsonOptions), BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }
}
