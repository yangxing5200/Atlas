using System.Text.Json;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;

namespace Atlas.BackgroundTasks;

public static class BackgroundJobQueues
{
    public const string Default = "default";
}

public sealed class BackgroundJobWorkerOptions
{
    public bool Enabled { get; set; }
    public string[] Queues { get; set; } = [BackgroundJobQueues.Default];
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int ProcessingTimeoutSeconds { get; set; } = 300;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public int DefaultMaxAttempts { get; set; } = 5;
}

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
    bool AlreadyExists);

public sealed record BackgroundJobExecutionResult(
    bool Succeeded,
    string? Result = null)
{
    public static BackgroundJobExecutionResult Success(string? result = null)
    {
        return new BackgroundJobExecutionResult(true, result);
    }
}

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

public interface IBackgroundJobClient
{
    Task<BackgroundJobEnqueueResult> EnqueueAsync<TPayload>(
        EnqueueBackgroundJobRequest<TPayload> request,
        CancellationToken ct = default);

    Task<BackgroundJob?> FindAsync(long jobId, CancellationToken ct = default);
}

public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default);
}
