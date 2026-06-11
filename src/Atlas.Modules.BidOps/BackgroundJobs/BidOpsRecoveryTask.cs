using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsRecoveryTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _attachments;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<BidOpsRecoveryTask> _logger;

    public BidOpsRecoveryTask(
        IConfiguration configuration,
        IExecutionIdentityAccessor identityAccessor,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> attachments,
        IBackgroundJobClient jobs,
        ILogger<BidOpsRecoveryTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "bidops.recovery";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(
        1,
        _configuration.GetValue<int?>("BidOps:Recovery:IntervalMinutes") ?? 5));

    public bool RunOnStartup => _configuration.GetValue<bool?>("BidOps:Recovery:RunOnStartup") ?? true;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool?>("BidOps:Recovery:Enabled").GetValueOrDefault(true))
            return;

        var tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "Recovery");
        if (tenantIds.Count == 0)
            tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "ScheduledScan");

        if (tenantIds.Count == 0)
            return;

        var maxRawPerCycle = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:Recovery:MaxRawPerCycle") ?? 50,
            1,
            500);
        var userId = BidOpsBackgroundTenantConfiguration.GetUserId(_configuration, "Recovery");
        var userName = BidOpsBackgroundTenantConfiguration.GetUserName(_configuration, "Recovery", "BidOps Recovery");

        foreach (var tenantId in tenantIds)
        {
            using var identity = _identityAccessor.Begin(new ExecutionIdentitySnapshot(
                tenantId,
                StoreId: null,
                userId,
                userName,
                SessionId: null,
                IsAuthenticated: true));

            var rawIds = await FindRecoverableRawNoticeIdsAsync(maxRawPerCycle, ct);
            foreach (var rawId in rawIds)
            {
                await _jobs.EnqueueAsync(
                    new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
                    {
                        JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                        Queue = BidOpsBackgroundJobQueues.BidOps,
                        JobName = "BidOps recovery attachment and parse",
                        TenantId = tenantId,
                        DeduplicationKey = $"bidops:recovery:attachment-process:{tenantId}:{rawId}:{DateTime.UtcNow:yyyyMMddHHmm}",
                        Payload = new AttachmentProcessJobPayload(
                            tenantId,
                            StoreId: null,
                            userId,
                            userName,
                            rawId)
                    },
                    ct);
            }

            if (rawIds.Count > 0)
            {
                _logger.LogInformation(
                    "BidOps recovery enqueued {Count} raw notices for tenant {TenantId}.",
                    rawIds.Count,
                    tenantId);
            }
        }
    }

    private async Task<IReadOnlyList<long>> FindRecoverableRawNoticeIdsAsync(
        int maxRawPerCycle,
        CancellationToken ct)
    {
        var rawQuery = await _rawNotices.QueryAsync(ct);
        var rawIds = await rawQuery
            .Where(x => x.Status == RawNoticeStatus.ParseQueued || x.Status == RawNoticeStatus.Failed)
            .OrderBy(x => x.FetchTime)
            .Take(maxRawPerCycle)
            .SelectToListAsync(x => new RawNoticeIdProjection { Id = x.Id }, ct);

        var ids = rawIds.Select(x => x.Id).ToHashSet();

        var attachmentQuery = await _attachments.QueryAsync(ct);
        var attachmentRawIds = await attachmentQuery
            .Where(x => x.DownloadStatus == DownloadStatus.Pending ||
                        x.DownloadStatus == DownloadStatus.Failed ||
                        x.TextExtractStatus == TextExtractStatus.Pending ||
                        x.TextExtractStatus == TextExtractStatus.Failed)
            .OrderBy(x => x.CreatedAt)
            .Take(maxRawPerCycle)
            .SelectToListAsync(x => new RawNoticeIdProjection { Id = x.RawNoticeId }, ct);

        foreach (var projection in attachmentRawIds)
        {
            ids.Add(projection.Id);
            if (ids.Count >= maxRawPerCycle)
                break;
        }

        return ids.ToArray();
    }

    private sealed class RawNoticeIdProjection
    {
        public long Id { get; init; }
    }
}
