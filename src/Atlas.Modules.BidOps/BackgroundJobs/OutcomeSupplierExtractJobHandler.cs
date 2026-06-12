using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class OutcomeSupplierExtractJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsOutcomeSupplierExtractionService _extraction;
    private readonly ILogger<OutcomeSupplierExtractJobHandler> _logger;

    public OutcomeSupplierExtractJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsOutcomeSupplierExtractionService extraction,
        ILogger<OutcomeSupplierExtractJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.OutcomeSupplierExtract;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<OutcomeSupplierExtractJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _extraction.ExtractRawNoticeAsync(payload.RawNoticeId, ct);
        _logger.LogInformation(
            "BidOps outcome supplier extract job saved {SavedCount} records for raw notice {RawNoticeId}.",
            result.SavedCount,
            payload.RawNoticeId);

        return BackgroundJobExecutionResult.Success(
            $"rawNoticeId={payload.RawNoticeId}; outcomeSupplierRecords={result.SavedCount}");
    }
}
