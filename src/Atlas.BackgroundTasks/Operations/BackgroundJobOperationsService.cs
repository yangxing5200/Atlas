using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Models.Tenant.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks.Operations;

public sealed class BackgroundJobOperationsService : IBackgroundJobOperationsService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int DeduplicationKeyMaxLength = 300;
    private const int CancellationRequestedByMaxLength = 200;
    private const string BidOpsQueue = "bidops";
    private const string BidOpsJobTypePrefix = "bidops.";
    private const string BidOpsModuleName = "BidOps";
    private const string BidOpsStructuredParseJobType = "bidops.ai.structured-parse";
    private const string BidOpsOutcomeSupplierExtractJobType = "bidops.outcome.supplier-extract";

    private readonly AtlasGlobalDbContext _dbContext;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IIdGenerator _idGenerator;
    private readonly ISensitiveJsonMasker _masker;
    private readonly BackgroundJobWorkerOptions _workerOptions;

    public BackgroundJobOperationsService(
        AtlasGlobalDbContext dbContext,
        ICurrentIdentity currentIdentity,
        IIdGenerator idGenerator,
        ISensitiveJsonMasker masker,
        IOptions<BackgroundJobWorkerOptions> workerOptions)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
        _workerOptions = workerOptions?.Value ?? new BackgroundJobWorkerOptions();
    }

    public async Task<PagedResult<BackgroundJobListItemDto>> SearchAsync(
        BackgroundJobSearchQuery query,
        bool bidOpsOnly = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (pageIndex, pageSize) = NormalizePaging(query);
        var now = DateTime.Now;
        var builder = BuildQuery(query, bidOpsOnly, now).AsNoTracking();

        var total = await builder.CountAsync(ct);
        var jobs = await builder
            .OrderByDescending(x => x.Status == BackgroundJobStatus.Running)
            .ThenByDescending(x => x.Status == BackgroundJobStatus.Pending)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<BackgroundJobListItemDto>(
            total,
            jobs.Select(job => MapListItem(job, now)).ToList(),
            pageIndex,
            pageSize);
    }

    public async Task<BackgroundJobSummaryDto> GetSummaryAsync(
        BackgroundJobSearchQuery query,
        bool bidOpsOnly = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = DateTime.Now;
        var builder = BuildQuery(query, bidOpsOnly, now).AsNoTracking();
        var staleThresholdUtc = GetStaleThresholdUtc(now);

        var statusCounts = await builder
            .GroupBy(x => x.Status)
            .Select(x => new BackgroundJobStatusCountDto
            {
                Status = x.Key,
                StatusName = x.Key.ToString(),
                Count = x.Count()
            })
            .ToListAsync(ct);

        var queues = await builder
            .GroupBy(x => x.Queue)
            .Select(x => new BackgroundJobDimensionCountDto
            {
                Name = x.Key,
                DisplayName = x.Key,
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(ct);

        var failedJobTypes = await builder
            .Where(x => x.Status == BackgroundJobStatus.Failed || x.Status == BackgroundJobStatus.Dead)
            .GroupBy(x => x.JobType)
            .Select(x => new BackgroundJobDimensionCountDto
            {
                Name = x.Key,
                DisplayName = BackgroundJobDisplayNames.ForJobType(x.Key),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var oldestPending = await builder
            .Where(x => x.Status == BackgroundJobStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var recentError = await builder
            .Where(x => x.Status == BackgroundJobStatus.Failed || x.Status == BackgroundJobStatus.Dead)
            .OrderByDescending(x => x.UpdatedAt ?? x.CompletedAtUtc ?? x.CreatedAt)
            .Select(x => x.UpdatedAt ?? x.CompletedAtUtc ?? x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var staleRunning = await builder
            .Where(x =>
                x.Status == BackgroundJobStatus.Running &&
                x.LockedAtUtc.HasValue &&
                x.LockedAtUtc.Value < staleThresholdUtc)
            .CountAsync(ct);

        var waitingRetry = await builder
            .Where(x =>
                x.Status == BackgroundJobStatus.Failed &&
                x.NextAttemptAtUtc.HasValue &&
                x.NextAttemptAtUtc.Value > now)
            .CountAsync(ct);

        var summary = new BackgroundJobSummaryDto
        {
            StatusCounts = statusCounts,
            QueueCounts = queues,
            JobTypeFailureCounts = failedJobTypes,
            Total = statusCounts.Sum(x => x.Count),
            Pending = CountStatus(statusCounts, BackgroundJobStatus.Pending),
            Running = CountStatus(statusCounts, BackgroundJobStatus.Running),
            Succeeded = CountStatus(statusCounts, BackgroundJobStatus.Succeeded),
            Failed = CountStatus(statusCounts, BackgroundJobStatus.Failed),
            Dead = CountStatus(statusCounts, BackgroundJobStatus.Dead),
            Canceled = CountStatus(statusCounts, BackgroundJobStatus.Canceled),
            StaleRunning = staleRunning,
            WaitingRetry = waitingRetry,
            OldestPendingAt = oldestPending,
            RecentErrorAt = recentError == default ? null : recentError,
            OldestPendingAtUtc = oldestPending,
            RecentErrorAtUtc = recentError == default ? null : recentError
        };

        return summary;
    }

    public async Task<BackgroundJobDetailDto?> GetAsync(
        long id,
        bool bidOpsOnly = false,
        CancellationToken ct = default)
    {
        var query = ApplyTenantScope(_dbContext.BackgroundJobs.AsNoTracking(), null);
        if (bidOpsOnly)
            query = ApplyBidOpsScope(query);

        var job = await query.FirstOrDefaultAsync(x => x.Id == id, ct);
        return job == null ? null : MapDetail(job, DateTime.Now);
    }

    public async Task<BackgroundJobRetryResultDto?> RetryAsync(
        long id,
        bool bidOpsOnly = false,
        CancellationToken ct = default)
    {
        var query = ApplyTenantScope(_dbContext.BackgroundJobs, null);
        if (bidOpsOnly)
            query = ApplyBidOpsScope(query);

        var original = await query.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (original == null)
            return null;

        if (original.Status is BackgroundJobStatus.Pending or BackgroundJobStatus.Running)
            throw new InvalidOperationException("当前任务仍在等待或执行中，不能创建人工重试任务。");

        var now = DateTime.Now;
        var job = new BackgroundJob
        {
            Id = _idGenerator.NextId(),
            JobType = original.JobType,
            Queue = original.Queue,
            JobName = string.IsNullOrWhiteSpace(original.JobName)
                ? original.JobType
                : $"{original.JobName} (manual retry)",
            DeduplicationKey = BuildRetryDeduplicationKey(original, now),
            TenantId = original.TenantId,
            StoreId = original.StoreId,
            Payload = original.Payload,
            Status = BackgroundJobStatus.Pending,
            Priority = original.Priority,
            AvailableAtUtc = now,
            MaxAttempts = Math.Max(1, original.MaxAttempts > 0 ? original.MaxAttempts : _workerOptions.DefaultMaxAttempts),
            CreatedAt = now
        };

        await _dbContext.BackgroundJobs.AddAsync(job, ct);
        await _dbContext.SaveChangesAsync(ct);

        return new BackgroundJobRetryResultDto
        {
            OriginalJobId = original.Id,
            NewJobId = job.Id,
            JobType = job.JobType,
            JobTypeName = BackgroundJobDisplayNames.ForJobType(job.JobType),
            Queue = job.Queue,
            Message = "已创建新的后台任务用于人工重试，原任务历史不会被覆盖。"
        };
    }

    public async Task<BackgroundJobCancelResultDto?> CancelAsync(
        long id,
        BackgroundJobCancelRequest request,
        bool bidOpsOnly = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = ApplyTenantScope(_dbContext.BackgroundJobs, null);
        if (bidOpsOnly)
            query = ApplyBidOpsScope(query);

        var job = await query.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job == null)
            return null;

        if (job.Status == BackgroundJobStatus.Succeeded)
        {
            return new BackgroundJobCancelResultDto
            {
                JobId = job.Id,
                Status = job.Status,
                StatusName = job.Status.ToString(),
                IsCancellationRequested = false,
                Message = "任务已成功完成，不能取消。"
            };
        }

        var now = DateTime.Now;
        var reason = NormalizeCancellationReason(request.Reason);
        var requestedBy = GetCancellationRequester();

        if (job.Status == BackgroundJobStatus.Canceled)
        {
            return new BackgroundJobCancelResultDto
            {
                JobId = job.Id,
                Status = job.Status,
                StatusName = job.Status.ToString(),
                IsCancellationRequested = false,
                Message = "任务已处于取消状态。"
            };
        }

        if (job.Status == BackgroundJobStatus.Running)
        {
            job.CancellationRequestedAt ??= now;
            job.CancellationRequestedBy = requestedBy;
            job.CancellationReason = reason;
            job.Result = BuildCancellationResult(reason, requested: true);
            job.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);

            return new BackgroundJobCancelResultDto
            {
                JobId = job.Id,
                Status = job.Status,
                StatusName = job.Status.ToString(),
                IsCancellationRequested = true,
                Message = "终止请求已提交，Worker 会尽快通知正在执行的任务停止。"
            };
        }

        job.Status = BackgroundJobStatus.Canceled;
        job.CompletedAtUtc = now;
        job.NextAttemptAtUtc = null;
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.CancellationRequestedAt ??= now;
        job.CancellationRequestedBy = requestedBy;
        job.CancellationReason = reason;
        job.Result = BuildCancellationResult(reason, requested: false);
        job.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);

        return new BackgroundJobCancelResultDto
        {
            JobId = job.Id,
            Status = job.Status,
            StatusName = job.Status.ToString(),
            IsCancellationRequested = false,
            Message = "任务已取消。"
        };
    }

    private IQueryable<BackgroundJob> BuildQuery(
        BackgroundJobSearchQuery query,
        bool bidOpsOnly,
        DateTime now)
    {
        var builder = ApplyTenantScope(_dbContext.BackgroundJobs.AsQueryable(), query.TenantId);
        if (bidOpsOnly)
            builder = ApplyBidOpsScope(builder);

        if (query.DeadOnly == true)
            builder = builder.Where(x => x.Status == BackgroundJobStatus.Dead);
        else if (query.Status.HasValue)
            builder = builder.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Queue))
        {
            var queue = query.Queue.Trim();
            builder = builder.Where(x => x.Queue == queue);
        }

        if (!string.IsNullOrWhiteSpace(query.JobType))
        {
            var jobType = query.JobType.Trim();
            builder = builder.Where(x => x.JobType.Contains(jobType));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceModule))
        {
            var sourceModule = query.SourceModule.Trim();
            builder = sourceModule.Equals(BidOpsModuleName, StringComparison.OrdinalIgnoreCase)
                ? ApplyBidOpsScope(builder)
                : builder.Where(x => x.Payload.Contains(sourceModule));
        }

        if (!string.IsNullOrWhiteSpace(query.BusinessType))
        {
            var businessType = query.BusinessType.Trim();
            builder = builder.Where(x => x.Payload.Contains(businessType));
        }

        if (query.BusinessId.HasValue)
        {
            var businessId = query.BusinessId.Value.ToString();
            builder = builder.Where(x => x.Payload.Contains(businessId));
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            var correlationId = query.CorrelationId.Trim();
            builder = builder.Where(x =>
                x.Payload.Contains(correlationId) ||
                (x.DeduplicationKey != null && x.DeduplicationKey.Contains(correlationId)));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            if (long.TryParse(keyword, out var id))
            {
                builder = builder.Where(x => x.Id == id);
            }
            else
            {
                builder = builder.Where(x =>
                    x.JobType.Contains(keyword) ||
                    x.Queue.Contains(keyword) ||
                    x.JobName.Contains(keyword) ||
                    (x.DeduplicationKey != null && x.DeduplicationKey.Contains(keyword)) ||
                    (x.LastError != null && x.LastError.Contains(keyword)) ||
                    (x.Result != null && x.Result.Contains(keyword)));
            }
        }

        var createdFrom = query.CreatedFrom ?? query.CreatedFromUtc;
        var createdTo = query.CreatedTo ?? query.CreatedToUtc;
        if (createdFrom.HasValue)
            builder = builder.Where(x => x.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            builder = builder.Where(x => x.CreatedAt <= createdTo.Value);

        if (query.StaleRunningOnly == true)
        {
            var staleThresholdUtc = GetStaleThresholdUtc(now);
            builder = builder.Where(x =>
                x.Status == BackgroundJobStatus.Running &&
                x.LockedAtUtc.HasValue &&
                x.LockedAtUtc.Value < staleThresholdUtc);
        }

        if (query.WaitingRetryOnly == true)
        {
            builder = builder.Where(x =>
                x.Status == BackgroundJobStatus.Failed &&
                x.NextAttemptAtUtc.HasValue &&
                x.NextAttemptAtUtc.Value > now);
        }

        return builder;
    }

    private IQueryable<BackgroundJob> ApplyTenantScope(
        IQueryable<BackgroundJob> query,
        long? requestedTenantId)
    {
        if (_currentIdentity.TenantId is > 0)
            return query.Where(x => x.TenantId == _currentIdentity.TenantId.Value);

        if (requestedTenantId is > 0)
            return query.Where(x => x.TenantId == requestedTenantId.Value);

        return query.Where(x => x.TenantId == null);
    }

    private static IQueryable<BackgroundJob> ApplyBidOpsScope(IQueryable<BackgroundJob> query)
    {
        return query.Where(x =>
            x.Queue == BidOpsQueue ||
            x.JobType.StartsWith(BidOpsJobTypePrefix));
    }

    private BackgroundJobListItemDto MapListItem(BackgroundJob job, DateTime now)
    {
        var maskedPayload = _masker.MaskJson(job.Payload);
        var lastError = _masker.MaskText(job.LastError, 1_000);
        var result = _masker.MaskText(job.Result, 1_000);
        var cancellationReason = _masker.MaskText(job.CancellationReason, 1_000);
        var waitMilliseconds = CalculateWaitMilliseconds(job, now);
        var runMilliseconds = CalculateRunMilliseconds(job, now);

        return new BackgroundJobListItemDto
        {
            Id = job.Id,
            JobType = job.JobType,
            JobTypeName = BackgroundJobDisplayNames.ForJobType(job.JobType),
            Queue = job.Queue,
            JobName = job.JobName,
            DeduplicationKey = job.DeduplicationKey,
            TenantId = job.TenantId,
            StoreId = job.StoreId,
            Status = job.Status,
            StatusName = job.Status.ToString(),
            Priority = job.Priority,
            CreatedAt = job.CreatedAt,
            AvailableAt = job.AvailableAtUtc,
            StartedAt = job.StartedAtUtc,
            LockedAt = job.LockedAtUtc,
            CompletedAt = job.CompletedAtUtc,
            NextAttemptAt = job.NextAttemptAtUtc,
            AvailableAtUtc = job.AvailableAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            LockedAtUtc = job.LockedAtUtc,
            LockedBy = job.LockedBy,
            CompletedAtUtc = job.CompletedAtUtc,
            AttemptCount = job.AttemptCount,
            MaxAttempts = job.MaxAttempts,
            NextAttemptAtUtc = job.NextAttemptAtUtc,
            IsCancellationRequested = IsCancellationRequested(job),
            CancellationRequestedAt = job.CancellationRequestedAt,
            CancellationRequestedBy = job.CancellationRequestedBy,
            CancellationReason = cancellationReason,
            LastErrorPreview = Truncate(lastError, 300),
            ResultPreview = Truncate(result, 300),
            PayloadPreview = Truncate(maskedPayload, 500),
            IsStaleRunning = IsStaleRunning(job, now),
            WaitMilliseconds = waitMilliseconds,
            RunMilliseconds = runMilliseconds,
            WaitSeconds = ToWholeSeconds(waitMilliseconds),
            RunSeconds = ToWholeSeconds(runMilliseconds)
        };
    }

    private BackgroundJobDetailDto MapDetail(BackgroundJob job, DateTime now)
    {
        var item = MapListItem(job, now);
        return new BackgroundJobDetailDto
        {
            Id = item.Id,
            JobType = item.JobType,
            JobTypeName = item.JobTypeName,
            Queue = item.Queue,
            JobName = item.JobName,
            DeduplicationKey = item.DeduplicationKey,
            TenantId = item.TenantId,
            StoreId = item.StoreId,
            Status = item.Status,
            StatusName = item.StatusName,
            Priority = item.Priority,
            CreatedAt = item.CreatedAt,
            AvailableAt = item.AvailableAt,
            StartedAt = item.StartedAt,
            LockedAt = item.LockedAt,
            CompletedAt = item.CompletedAt,
            NextAttemptAt = item.NextAttemptAt,
            AvailableAtUtc = item.AvailableAtUtc,
            StartedAtUtc = item.StartedAtUtc,
            LockedAtUtc = item.LockedAtUtc,
            LockedBy = item.LockedBy,
            CompletedAtUtc = item.CompletedAtUtc,
            AttemptCount = item.AttemptCount,
            MaxAttempts = item.MaxAttempts,
            NextAttemptAtUtc = item.NextAttemptAtUtc,
            IsCancellationRequested = item.IsCancellationRequested,
            CancellationRequestedAt = item.CancellationRequestedAt,
            CancellationRequestedBy = item.CancellationRequestedBy,
            CancellationReason = item.CancellationReason,
            LastErrorPreview = item.LastErrorPreview,
            ResultPreview = item.ResultPreview,
            PayloadPreview = item.PayloadPreview,
            IsStaleRunning = item.IsStaleRunning,
            WaitMilliseconds = item.WaitMilliseconds,
            RunMilliseconds = item.RunMilliseconds,
            WaitSeconds = item.WaitSeconds,
            RunSeconds = item.RunSeconds,
            Payload = _masker.MaskJson(job.Payload),
            LastError = _masker.MaskText(job.LastError, 20_000),
            Result = _masker.MaskText(job.Result, GetDetailResultMaxCharacters(job))
        };
    }

    private static int GetDetailResultMaxCharacters(BackgroundJob job)
    {
        return IsBidOpsAiDiagnosticJob(job)
            ? BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters
            : BackgroundJobResultStorageLimits.DefaultDetailMaxCharacters;
    }

    private static bool IsBidOpsAiDiagnosticJob(BackgroundJob job)
    {
        return string.Equals(job.JobType, BidOpsStructuredParseJobType, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(job.JobType, BidOpsOutcomeSupplierExtractJobType, StringComparison.OrdinalIgnoreCase);
    }

    private DateTime GetStaleThresholdUtc(DateTime now)
    {
        return now.AddSeconds(-Math.Max(30, _workerOptions.ProcessingTimeoutSeconds));
    }

    private bool IsStaleRunning(BackgroundJob job, DateTime now)
    {
        return job.Status == BackgroundJobStatus.Running &&
               job.LockedAtUtc.HasValue &&
               job.LockedAtUtc.Value < GetStaleThresholdUtc(now);
    }

    private static bool IsCancellationRequested(BackgroundJob job)
    {
        return job.Status == BackgroundJobStatus.Running &&
               job.CancellationRequestedAt.HasValue;
    }

    private string GetCancellationRequester()
    {
        var name = string.IsNullOrWhiteSpace(_currentIdentity.UserName)
            ? "operator"
            : _currentIdentity.UserName.Trim();

        if (_currentIdentity.UserId is > 0)
            name = $"{name} ({_currentIdentity.UserId.Value})";

        return Truncate(name, CancellationRequestedByMaxLength);
    }

    private static string? NormalizeCancellationReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private static string BuildCancellationResult(string? reason, bool requested)
    {
        var prefix = requested
            ? "Cancellation requested by operator."
            : "Canceled by operator.";

        return string.IsNullOrWhiteSpace(reason)
            ? prefix
            : $"{prefix} Reason: {reason}";
    }

    private static long? CalculateWaitMilliseconds(BackgroundJob job, DateTime now)
    {
        if (job.Status != BackgroundJobStatus.Pending)
            return null;

        var startedAt = job.AvailableAtUtc > job.CreatedAt ? job.AvailableAtUtc : job.CreatedAt;
        return Math.Max(0, (long)Math.Ceiling((now - startedAt).TotalMilliseconds));
    }

    private static long? CalculateRunMilliseconds(BackgroundJob job, DateTime now)
    {
        if (!job.StartedAtUtc.HasValue)
            return null;

        var endedAt = job.CompletedAtUtc ?? now;
        return Math.Max(0, (long)Math.Ceiling((endedAt - job.StartedAtUtc.Value).TotalMilliseconds));
    }

    private static long? ToWholeSeconds(long? milliseconds)
    {
        if (!milliseconds.HasValue)
            return null;

        return milliseconds.Value / 1000;
    }

    private static int CountStatus(
        IEnumerable<BackgroundJobStatusCountDto> counts,
        BackgroundJobStatus status)
    {
        return counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BackgroundJobSearchQuery query)
    {
        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static string BuildRetryDeduplicationKey(BackgroundJob original, DateTime now)
    {
        var suffix = $":manual-retry:{now:yyyyMMddHHmmssfff}";
        var baseKey = string.IsNullOrWhiteSpace(original.DeduplicationKey)
            ? $"job:{original.Id}"
            : original.DeduplicationKey.Trim();
        var maxBaseLength = Math.Max(0, DeduplicationKeyMaxLength - suffix.Length);

        return baseKey.Length > maxBaseLength
            ? baseKey[..maxBaseLength] + suffix
            : baseKey + suffix;
    }

    private static string Truncate(string value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            return value;

        return value[..maxCharacters] + "...";
    }
}
