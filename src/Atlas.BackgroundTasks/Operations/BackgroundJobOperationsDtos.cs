using Atlas.Core.Enums;

namespace Atlas.BackgroundTasks.Operations;

public sealed class BackgroundJobSearchQuery
{
    public string? Keyword { get; set; }
    public long? TenantId { get; set; }
    public string? Queue { get; set; }
    public string? JobType { get; set; }
    public BackgroundJobStatus? Status { get; set; }
    public string? SourceModule { get; set; }
    public string? BusinessType { get; set; }
    public long? BusinessId { get; set; }
    public string? CorrelationId { get; set; }
    public bool? DeadOnly { get; set; }
    public bool? StaleRunningOnly { get; set; }
    public bool? WaitingRetryOnly { get; set; }
    public DateTime? CreatedFromUtc { get; set; }
    public DateTime? CreatedToUtc { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BackgroundJobListItemDto
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string? DeduplicationKey { get; set; }
    public long? TenantId { get; set; }
    public long? StoreId { get; set; }
    public BackgroundJobStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string LastErrorPreview { get; set; } = string.Empty;
    public string ResultPreview { get; set; } = string.Empty;
    public string PayloadPreview { get; set; } = string.Empty;
    public bool IsStaleRunning { get; set; }
    public long? WaitSeconds { get; set; }
    public long? RunSeconds { get; set; }
}

public sealed class BackgroundJobDetailDto : BackgroundJobListItemDto
{
    public string Payload { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public sealed class BackgroundJobStatusCountDto
{
    public BackgroundJobStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class BackgroundJobDimensionCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class BackgroundJobSummaryDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Running { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Dead { get; set; }
    public int Canceled { get; set; }
    public int StaleRunning { get; set; }
    public int WaitingRetry { get; set; }
    public DateTime? OldestPendingAtUtc { get; set; }
    public DateTime? RecentErrorAtUtc { get; set; }
    public List<BackgroundJobStatusCountDto> StatusCounts { get; set; } = [];
    public List<BackgroundJobDimensionCountDto> QueueCounts { get; set; } = [];
    public List<BackgroundJobDimensionCountDto> JobTypeFailureCounts { get; set; } = [];
}

public sealed class BackgroundJobRetryResultDto
{
    public long OriginalJobId { get; set; }
    public long NewJobId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class BackgroundJobCancelRequest
{
    public string? Reason { get; set; }
}

public sealed class BackgroundJobCancelResultDto
{
    public long JobId { get; set; }
    public BackgroundJobStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class BackgroundWorkerSearchQuery
{
    public string? Keyword { get; set; }
    public bool? OnlineOnly { get; set; }
    public string? Queue { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class BackgroundWorkerHeartbeatDto
{
    public long Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string RuntimeMode { get; set; } = string.Empty;
    public string[] Queues { get; set; } = [];
    public bool OneTimeJobWorkerEnabled { get; set; }
    public bool RecurringTaskRunnerEnabled { get; set; }
    public long? CurrentJobId { get; set; }
    public string? CurrentJobType { get; set; }
    public string? CurrentQueue { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public bool IsOnline { get; set; }
    public long SecondsSinceLastSeen { get; set; }
}
