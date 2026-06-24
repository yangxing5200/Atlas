using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class ReviewQualityBackfillJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsReviewQualityService _quality;
    private readonly ILogger<ReviewQualityBackfillJobHandler> _logger;

    public ReviewQualityBackfillJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsReviewQualityService quality,
        ILogger<ReviewQualityBackfillJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _quality = quality ?? throw new ArgumentNullException(nameof(quality));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.ReviewQualityBackfill;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<ReviewQualityBackfillJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _quality.BackfillReviewQualityAsync(
            new ReviewQualityBackfillRequest
            {
                MaxItems = payload.MaxItems,
                NoticeType = payload.NoticeType,
                RiskLevel = payload.RiskLevel,
                DryRun = payload.DryRun,
                SourceId = payload.SourceId,
                PauseSourceAware = payload.PauseSourceAware
            },
            ct);

        _logger.LogInformation(
            "BidOps review quality backfill scanned {ScannedCount}, candidates {CandidateCount}, updated {UpdatedCount}, dryRun={DryRun}.",
            result.ScannedCount,
            result.CandidateCount,
            result.UpdatedCount,
            result.DryRun);

        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(result, JsonOptions));
    }
}
