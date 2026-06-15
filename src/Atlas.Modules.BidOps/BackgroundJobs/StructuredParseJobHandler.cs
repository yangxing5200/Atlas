using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
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
    private readonly ILogger<StructuredParseJobHandler> _logger;

    public StructuredParseJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsAiParsingService parsing,
        IBidOpsOutcomeSupplierExtractionService outcomeSupplierExtraction,
        ILogger<StructuredParseJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _parsing = parsing ?? throw new ArgumentNullException(nameof(parsing));
        _outcomeSupplierExtraction = outcomeSupplierExtraction ?? throw new ArgumentNullException(nameof(outcomeSupplierExtraction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            reviewTaskId = await _parsing.ParseRawNoticeAsync(payload.RawNoticeId, ct);
        }
        catch
        {
            await TryExtractOutcomeSuppliersAsync(payload.RawNoticeId, ct);
            throw;
        }

        _logger.LogInformation(
            "BidOps structured parse generated review task {ReviewTaskId} for raw notice {RawNoticeId}.",
            reviewTaskId,
            payload.RawNoticeId);

        var outcome = await TryExtractOutcomeSuppliersAsync(payload.RawNoticeId, ct);
        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(new
        {
            rawNoticeId = payload.RawNoticeId,
            reviewTaskId,
            outcomeSupplierExtraction = outcome
        }, JsonOptions));
    }

    private async Task<OutcomeSupplierExtractionResultDto?> TryExtractOutcomeSuppliersAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        try
        {
            return await _outcomeSupplierExtraction.ExtractRawNoticeAsync(rawNoticeId, ct);
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
}
