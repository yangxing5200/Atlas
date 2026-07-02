using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class ReviewBulkApproveJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsReviewService _review;
    private readonly ILogger<ReviewBulkApproveJobHandler> _logger;

    public ReviewBulkApproveJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsReviewService review,
        ILogger<ReviewBulkApproveJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _review = review ?? throw new ArgumentNullException(nameof(review));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.ReviewBulkApprove;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<ReviewBulkApproveJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        // 批量审核仍复用逐项审核规则，后台任务只负责把长耗时从浏览器请求中移出。
        var result = await _review.BulkApproveAsync(
            new BulkApproveReviewTasksRequest
            {
                ReviewTaskIds = payload.ReviewTaskIds,
                Remark = payload.Remark,
                ExpectedRiskLevel = payload.ExpectedRiskLevel,
                MaxHighRiskIssueCount = payload.MaxHighRiskIssueCount
            },
            ct);

        _logger.LogInformation(
            "BidOps review bulk approve completed. requested={RequestedCount}; succeeded={SucceededCount}; failed={FailedCount}; skipped={SkippedCount}.",
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.SkippedCount);

        return BackgroundJobExecutionResult.Success(JsonSerializer.Serialize(result, JsonOptions));
    }
}
