using System.Text.Json;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;

namespace Atlas.BackgroundTasks;

/// <summary>
/// 后台任务队列名称约定。
/// </summary>
public static class BackgroundJobQueues
{
    public const string Default = "default";
}

/// <summary>
/// 一次性后台任务 Worker 配置。
/// </summary>
/// <remarks>
/// ProcessingTimeoutSeconds 用于回收超时的 Running 任务，避免进程崩溃后任务永久锁死。
/// </remarks>
public sealed class BackgroundJobWorkerOptions
{
    public bool Enabled { get; set; }
    public string[] Queues { get; set; } = [BackgroundJobQueues.Default];
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int MaxConcurrency { get; set; } = 1;
    public Dictionary<string, int> JobTypeConcurrency { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] IncludedJobTypes { get; set; } = [];
    public string[] ExcludedJobTypes { get; set; } = [];
    public int ProcessingTimeoutSeconds { get; set; } = 300;
    public int MaxRunningSeconds { get; set; } = 7200;
    public int CancellationCheckIntervalSeconds { get; set; } = 2;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public int DefaultMaxAttempts { get; set; } = 5;
}

/// <summary>
/// 入队后台任务请求。
/// </summary>
/// <remarks>
/// DeduplicationKey 用于业务幂等，通常应按“租户 + 业务对象 + 动作”构造。
/// Payload 会被序列化为 JSON，建议只放可版本化的简单 DTO。
/// </remarks>
public sealed class EnqueueBackgroundJobRequest<TPayload>
{
    public required string JobType { get; init; }
    public string? Queue { get; init; }
    public string? JobName { get; init; }
    public TPayload? Payload { get; init; }
    public string? DeduplicationKey { get; init; }
    public long? TenantId { get; init; }
    public long? StoreId { get; init; }
    public int Priority { get; init; }
    public DateTime? AvailableAtUtc { get; init; }
    public int? MaxAttempts { get; init; }
}

public sealed record BackgroundJobEnqueueResult(
    long JobId,
    string JobType,
    string Queue,
    BackgroundJobStatus Status,
    bool AlreadyExists)
{
    public string JobTypeName => Operations.BackgroundJobDisplayNames.ForJobType(JobType);
}

/// <summary>
/// 后台任务处理结果。
/// </summary>
public sealed record BackgroundJobExecutionResult(
    bool Succeeded,
    string? Result = null,
    int? MaxResultCharacters = null)
{
    public static BackgroundJobExecutionResult Success(
        string? result = null,
        int? maxResultCharacters = null)
    {
        return new BackgroundJobExecutionResult(true, result, maxResultCharacters);
    }
}

public static class BackgroundJobResultStorageLimits
{
    public const int DefaultMaxCharacters = 4_000;
    public const int DefaultDetailMaxCharacters = 20_000;
    public const int AiDiagnosticsMaxCharacters = 1_000_000;
}

/// <summary>
/// 后台任务执行上下文，向 Handler 暴露任务记录和强类型 Payload 反序列化入口。
/// </summary>
public sealed class BackgroundJobExecutionContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BackgroundJobExecutionContext(BackgroundJob job)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
    }

    public BackgroundJob Job { get; }

    public TPayload GetPayload<TPayload>()
    {
        if (string.IsNullOrWhiteSpace(Job.Payload))
            throw new InvalidOperationException($"Background job {Job.Id} has no payload.");

        return JsonSerializer.Deserialize<TPayload>(Job.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Cannot deserialize payload for background job {Job.Id}.");
    }
}

/// <summary>
/// 后台任务入队与查询客户端。
/// </summary>
public interface IBackgroundJobClient
{
    Task<BackgroundJobEnqueueResult> EnqueueAsync<TPayload>(
        EnqueueBackgroundJobRequest<TPayload> request,
        CancellationToken ct = default);

    Task<BackgroundJob?> FindAsync(long jobId, CancellationToken ct = default);
}

public sealed record BackgroundJobExecutionGateDecision(
    bool IsAllowed,
    string Reason = "",
    DateTime? DeferUntil = null)
{
    public static BackgroundJobExecutionGateDecision Allow()
    {
        return new BackgroundJobExecutionGateDecision(true);
    }

    public static BackgroundJobExecutionGateDecision Defer(
        string reason,
        DateTime? deferUntil = null)
    {
        return new BackgroundJobExecutionGateDecision(false, reason, deferUntil);
    }
}

/// <summary>
/// Allows modules to temporarily defer background jobs before handler execution.
/// </summary>
public interface IBackgroundJobExecutionGate
{
    Task<BackgroundJobExecutionGateDecision> EvaluateAsync(
        BackgroundJob job,
        CancellationToken ct = default);
}

/// <summary>
/// 后台任务处理器契约。
/// </summary>
/// <remarks>
/// JobType 必须与入队请求一致；Handler 应保持幂等，因为任务可能因重试或锁回收被再次执行。
/// </remarks>
public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default);
}
