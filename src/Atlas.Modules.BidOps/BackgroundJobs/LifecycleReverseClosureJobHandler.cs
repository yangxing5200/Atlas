using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Exceptions;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class LifecycleReverseClosureJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsReverseLifecycleClosureService _closure;
    private readonly ILogger<LifecycleReverseClosureJobHandler> _logger;

    public LifecycleReverseClosureJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsReverseLifecycleClosureService closure,
        ILogger<LifecycleReverseClosureJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.LifecycleReverseClosure;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<LifecycleReverseClosureJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        BidOpsReverseClosureDebugResult result;
        if (payload.RawNoticeId.HasValue)
        {
            result = payload.PersistLifecycleLinks
                ? await _closure.ReverseCloseRawNoticeAndPersistAsync(payload.RawNoticeId.Value, ct)
                : await _closure.ReverseCloseRawNoticeAsync(payload.RawNoticeId.Value, ct);
        }
        else if (!string.IsNullOrWhiteSpace(payload.AwardUrl))
        {
            result = await _closure.ReverseCloseUrlAsync(new BidOpsReverseCloseUrlRequest
            {
                Url = payload.AwardUrl,
                PersistEvidence = true,
                PersistLifecycleLinks = payload.PersistLifecycleLinks
            }, ct);
        }
        else
        {
            throw new AtlasException("Lifecycle reverse closure job requires RawNoticeId or AwardUrl.");
        }

        _logger.LogInformation(
            "BidOps lifecycle reverse closure job completed for raw notice {RawNoticeId} / url {AwardUrl}; closures={ClosureCount}; persistedLinks={PersistedCount}.",
            payload.RawNoticeId,
            payload.AwardUrl,
            result.Closures.Count,
            result.PersistedLifecycleLinks.Count);

        return BackgroundJobExecutionResult.Success(
            JsonSerializer.Serialize(result, JsonOptions),
            BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }
}
