using System.Text.Encodings.Web;
using System.Text.Json;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.BackgroundTasks;

public interface IBackgroundJobProgressReporter
{
    Task ReportAsync(
        long jobId,
        string stage,
        string message,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken ct = default);

    Task<TResult> RunWithHeartbeatAsync<TResult>(
        long jobId,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken ct = default,
        TimeSpan? interval = null);
}

public sealed class BackgroundJobProgressReporter : IBackgroundJobProgressReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobProgressReporter> _logger;

    public BackgroundJobProgressReporter(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobProgressReporter> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ReportAsync(
        long jobId,
        string stage,
        string message,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.Now;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            var job = await db.BackgroundJobs
                .FirstOrDefaultAsync(x =>
                    x.Id == jobId &&
                    x.Status == BackgroundJobStatus.Running &&
                    x.CompletedAtUtc == null,
                    ct);
            if (job == null)
                return;

            job.Result = Truncate(JsonSerializer.Serialize(new BackgroundJobProgressSnapshot
            {
                Message = Normalize(message, "后台任务执行中"),
                Stage = Normalize(stage, "running"),
                State = "Running",
                UpdatedAt = now,
                Data = data ?? new Dictionary<string, object?>()
            }, JsonOptions), BackgroundJobResultStorageLimits.DefaultMaxCharacters);
            job.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report progress for background job {JobId}.", jobId);
        }
    }

    public async Task<TResult> RunWithHeartbeatAsync<TResult>(
        long jobId,
        string stage,
        string message,
        Func<CancellationToken, Task<TResult>> operation,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken ct = default,
        TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var startedAt = DateTime.Now;
        await ReportAsync(jobId, stage, message, BuildData(data, startedAt), ct);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = HeartbeatAsync(
            jobId,
            stage,
            message,
            data,
            startedAt,
            interval ?? DefaultHeartbeatInterval,
            heartbeatCts.Token);

        try
        {
            return await operation(ct);
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            await ObserveHeartbeatAsync(heartbeat);
        }
    }

    private async Task HeartbeatAsync(
        long jobId,
        string stage,
        string message,
        IReadOnlyDictionary<string, object?>? data,
        DateTime startedAt,
        TimeSpan interval,
        CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(interval <= TimeSpan.Zero ? DefaultHeartbeatInterval : interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await ReportAsync(jobId, stage, message, BuildData(data, startedAt), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static Task ObserveHeartbeatAsync(Task heartbeat)
    {
        return heartbeat.IsCompletedSuccessfully ? Task.CompletedTask : ObserveHeartbeatSlowAsync(heartbeat);
    }

    private static async Task ObserveHeartbeatSlowAsync(Task heartbeat)
    {
        try
        {
            await heartbeat;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static Dictionary<string, object?> BuildData(
        IReadOnlyDictionary<string, object?>? data,
        DateTime startedAt)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (data != null)
        {
            foreach (var item in data)
                values[item.Key] = item.Value;
        }

        values["elapsedSeconds"] = Math.Max(0, (long)(DateTime.Now - startedAt).TotalSeconds);
        return values;
    }

    private static string Normalize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed class BackgroundJobProgressSnapshot
    {
        public string Message { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string State { get; set; } = "Running";
        public DateTime UpdatedAt { get; set; }
        public IReadOnlyDictionary<string, object?> Data { get; set; } = new Dictionary<string, object?>();
    }
}
