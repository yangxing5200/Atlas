using System.Diagnostics;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.Telemetry;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks;

/// <summary>
/// 轮询全局任务表并执行一次性后台任务的托管服务。
/// </summary>
/// <remarks>
/// Worker 通过数据库原子 UPDATE 领取任务，支持多实例部署；任务处理器必须自行保证业务幂等。
/// </remarks>
public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundWorkerHeartbeatState _heartbeatState;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private readonly BackgroundJobWorkerOptions _options;

    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        BackgroundWorkerHeartbeatState heartbeatState,
        IOptions<BackgroundJobWorkerOptions> options,
        ILogger<BackgroundJobWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _heartbeatState = heartbeatState ?? throw new ArgumentNullException(nameof(heartbeatState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new BackgroundJobWorkerOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Background job worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOnceAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("Processed {Count} background jobs.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job worker cycle failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)),
                stoppingToken);
        }
    }

    internal async Task<int> ProcessOnceAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
        var handlers = scope.ServiceProvider
            .GetServices<IBackgroundJobHandler>()
            .GroupBy(x => x.JobType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var queues = GetEnabledQueues();
        var now = DateTime.UtcNow;
        var staleLockedBefore = now.AddSeconds(-Math.Max(30, _options.ProcessingTimeoutSeconds));
        var batchSize = Math.Max(1, _options.BatchSize);

        // 查询候选任务时包含超时 Running 任务，用于回收崩溃或长时间失联 Worker 留下的锁。
        var jobs = await db.BackgroundJobs
            .Where(x =>
                queues.Contains(x.Queue) &&
                x.CompletedAtUtc == null &&
                x.AttemptCount < x.MaxAttempts &&
                x.AvailableAtUtc <= now &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.Status == BackgroundJobStatus.Pending ||
                 x.Status == BackgroundJobStatus.Failed ||
                 (x.Status == BackgroundJobStatus.Running &&
                  x.LockedAtUtc != null &&
                  x.LockedAtUtc < staleLockedBefore)))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var job in jobs)
        {
            if (!await TryClaimAsync(db, job.Id, job.Queue, now, staleLockedBefore, ct))
                continue;

            // 领取成功后重新加载，确保当前 DbContext 中的实体状态与数据库锁定结果一致。
            await db.Entry(job).ReloadAsync(ct);
            _heartbeatState.SetCurrentJob(job.Id, job.JobType, job.Queue);

            using var activity = AtlasTelemetry.ActivitySource.StartActivity(
                "atlas.background_job.execute",
                ActivityKind.Consumer);
            activity?.SetTag("atlas.background_job.id", job.Id);
            activity?.SetTag("atlas.background_job.type", job.JobType);
            activity?.SetTag("atlas.background_job.queue", job.Queue);
            activity?.SetTag("atlas.background_job.attempt", job.AttemptCount);

            try
            {
                if (!handlers.TryGetValue(job.JobType, out var handler))
                    throw new InvalidOperationException($"No background job handler registered for job type '{job.JobType}'.");

                var result = await handler.HandleAsync(new BackgroundJobExecutionContext(job), ct);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Result ?? $"Background job {job.Id} returned a failed result.");

                job.Status = BackgroundJobStatus.Succeeded;
                job.CompletedAtUtc = DateTime.UtcNow;
                job.LockedAtUtc = null;
                job.LockedBy = null;
                job.LastError = null;
                job.NextAttemptAtUtc = null;
                job.Result = Truncate(result.Result ?? "Succeeded", 4000);
                await db.SaveChangesAsync(ct);
                activity?.SetStatus(ActivityStatusCode.Ok);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                await MarkFailedAsync(db, job, ex, ct);
                processed++;
            }
            finally
            {
                _heartbeatState.ClearCurrentJob(job.Id);
            }
        }

        return processed;
    }

    private async Task<bool> TryClaimAsync(
        AtlasGlobalDbContext db,
        long jobId,
        string queue,
        DateTime now,
        DateTime staleLockedBefore,
        CancellationToken ct)
    {
        var pending = (int)BackgroundJobStatus.Pending;
        var running = (int)BackgroundJobStatus.Running;
        var failed = (int)BackgroundJobStatus.Failed;

        // 用单条条件 UPDATE 完成抢占，避免多个 Worker 同时执行同一个任务。
        var updated = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE BackgroundJobs
             SET Status = {running},
                 LockedAtUtc = {now},
                 LockedBy = {_heartbeatState.WorkerId},
                 StartedAtUtc = COALESCE(StartedAtUtc, {now}),
                 AttemptCount = AttemptCount + 1,
                 NextAttemptAtUtc = NULL,
                 UpdatedAt = {now}
             WHERE Id = {jobId}
               AND Queue = {queue}
               AND CompletedAtUtc IS NULL
               AND AttemptCount < MaxAttempts
               AND AvailableAtUtc <= {now}
               AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {now})
               AND (
                    Status IN ({pending}, {failed})
                    OR (Status = {running} AND LockedAtUtc IS NOT NULL AND LockedAtUtc < {staleLockedBefore})
               )
             """,
            ct);

        return updated == 1;
    }

    private async Task MarkFailedAsync(
        AtlasGlobalDbContext db,
        BackgroundJob job,
        Exception exception,
        CancellationToken ct)
    {
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.LastError = Truncate(exception.ToString(), 4000);

        if (job.AttemptCount >= job.MaxAttempts)
        {
            // 达到最大重试次数后进入 Dead 状态，后续需要人工或维护任务介入。
            job.Status = BackgroundJobStatus.Dead;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.NextAttemptAtUtc = null;
            job.Result = "Failed permanently.";
        }
        else
        {
            job.Status = BackgroundJobStatus.Failed;
            // 指数退避减少故障依赖持续异常时的数据库和外部服务压力。
            job.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(job.AttemptCount));
        }

        await db.SaveChangesAsync(ct);

        _logger.LogError(
            exception,
            "Background job {JobId} ({JobType}) failed at attempt {AttemptCount}/{MaxAttempts}.",
            job.Id,
            job.JobType,
            job.AttemptCount,
            job.MaxAttempts);
    }

    private string[] GetEnabledQueues()
    {
        var queues = _options.Queues
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return queues.Length == 0 ? [BackgroundJobQueues.Default] : queues;
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var initial = Math.Max(1, _options.InitialRetryDelaySeconds);
        var max = Math.Max(initial, _options.MaxRetryDelaySeconds);
        var seconds = Math.Min(max, initial * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
