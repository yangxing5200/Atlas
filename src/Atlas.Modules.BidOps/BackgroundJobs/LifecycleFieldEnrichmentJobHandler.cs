using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class LifecycleFieldEnrichmentJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsReverseLifecycleClosureService _closure;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<LifecycleFieldEnrichmentJobHandler> _logger;

    public LifecycleFieldEnrichmentJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsReverseLifecycleClosureService closure,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<LifecycleFieldEnrichmentJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.LifecycleFieldEnrichment;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<LifecycleFieldEnrichmentJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _closure.EnrichLifecycleLinkFieldsAsync(payload.LinkId, payload.ReviewerPrompt, ct);
        _logger.LogInformation(
            "BidOps lifecycle field enrichment job completed for link {LinkId}; reviewerPrompt={HasReviewerPrompt}.",
            payload.LinkId,
            !string.IsNullOrWhiteSpace(payload.ReviewerPrompt));

        return BackgroundJobExecutionResult.Success(
            JsonSerializer.Serialize(new
            {
                linkId = payload.LinkId,
                reviewerPrompt = !string.IsNullOrWhiteSpace(payload.ReviewerPrompt),
                result,
                aiResponses = _diagnostics.Entries,
                deepSeekResponses = _diagnostics.Entries
            }, JsonOptions),
            BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters);
    }
}
