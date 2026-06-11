using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class MockAiParseJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsAiParsingService _parsing;
    private readonly ILogger<MockAiParseJobHandler> _logger;

    public MockAiParseJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsAiParsingService parsing,
        ILogger<MockAiParseJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _parsing = parsing ?? throw new ArgumentNullException(nameof(parsing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.MockAiParse;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<MockAiParseJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var reviewTaskId = await _parsing.ParseRawNoticeAsync(payload.RawNoticeId, ct);
        _logger.LogInformation(
            "BidOps mock AI parse generated review task {ReviewTaskId} for raw notice {RawNoticeId}.",
            reviewTaskId,
            payload.RawNoticeId);

        return BackgroundJobExecutionResult.Success($"reviewTaskId={reviewTaskId}");
    }
}
