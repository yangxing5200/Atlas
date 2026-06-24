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

        var activeJobs = new List<ActiveJob>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ObserveCompletedJobsAsync(activeJobs, waitAll: false, stoppingToken);
                var capacity = GetMaxConcurrency() - activeJobs.Count;
                if (capacity > 0)
                {
                    var claim = await ClaimRunnableJobsAsync(
                        capacity,
                        BuildActiveJobTypeCounts(activeJobs),
                        stoppingToken);
                    processed += claim.MaintenanceProcessed;
                    foreach (var job in claim.Jobs)
                    {
                        activeJobs.Add(new ActiveJob(
                            job.Id,
                            job.JobType,
                            ProcessClaimedJobAsync(job.Id, stoppingToken)));
                    }
                }

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

            await WaitForNextWorkerTickAsync(activeJobs, stoppingToken);
        }

        if (activeJobs.Count > 0)
        {
            await ObserveCompletedJobsAsync(activeJobs, waitAll: true, stoppingToken);
        }
    }

    internal async Task<int> ProcessOnceAsync(CancellationToken ct = default)
    {
        var claim = await ClaimRunnableJobsAsync(
            GetMaxConcurrency(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ct);
        if (claim.Jobs.Count == 0)
            return claim.MaintenanceProcessed;

        var processingTasks = claim.Jobs
            .Select(job => ProcessClaimedJobAsync(job.Id, ct))
            .ToArray();
        var processingCounts = await Task.WhenAll(processingTasks);

        return claim.MaintenanceProcessed + processingCounts.Sum();
    }

    private async Task<ClaimRunnableJobsResult> ClaimRunnableJobsAsync(
        int availableSlots,
        IReadOnlyDictionary<string, int> activeByType,
        CancellationToken ct)
    {
        var queues = GetEnabledQueues();
        var now = DateTime.Now;
        var staleLockedBefore = now.AddSeconds(-Math.Max(30, _options.ProcessingTimeoutSeconds));
        var maxRunningTime = GetMaxRunningTime();
        var batchSize = Math.Max(1, _options.BatchSize);
        var claimedJobIds = new List<long>();
        var processed = 0;
        if (availableSlots <= 0)
            return new ClaimRunnableJobsResult(processed, []);

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();

            processed = await CancelStaleCancellationRequestedJobsAsync(
                db,
                queues,
                now,
                staleLockedBefore,
                ct);
            processed += await ForceTerminateTimedOutRunningJobsAsync(
                db,
                queues,
                now,
                maxRunningTime,
                ct);

            // 查询候选任务时包含超时 Running 任务，用于回收崩溃或长时间失联 Worker 留下的锁。
            var candidateScanLimit = Math.Clamp(
                Math.Max(batchSize, GetMaxConcurrency() * 50),
                batchSize,
                500);
            var jobs = await ApplyConfiguredJobTypeScope(db.BackgroundJobs
                .AsNoTracking()
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
                      x.LockedAtUtc < staleLockedBefore))))
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.CreatedAt)
                .Take(candidateScanLimit)
                .ToListAsync(ct);

            foreach (var job in SelectRunnableCandidates(jobs, availableSlots, activeByType))
            {
                if (await TryClaimAsync(db, job.Id, job.Queue, now, staleLockedBefore, ct))
                    claimedJobIds.Add(job.Id);
            }
        }

        var jobsById = new Dictionary<long, string>();
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            jobsById = await db.BackgroundJobs
                .AsNoTracking()
                .Where(x => claimedJobIds.Contains(x.Id))
                .Select(x => new { x.Id, x.JobType })
                .ToDictionaryAsync(x => x.Id, x => x.JobType, ct);
        }

        var claimedJobs = claimedJobIds
            .Select(id => new ClaimedJob(id, jobsById.GetValueOrDefault(id, string.Empty)))
            .ToArray();
        return new ClaimRunnableJobsResult(processed, claimedJobs);
    }

    private async Task<int> ProcessClaimedJobAsync(long jobId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
        var handlers = scope.ServiceProvider
            .GetServices<IBackgroundJobHandler>()
            .GroupBy(x => x.JobType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var gates = scope.ServiceProvider
            .GetServices<IBackgroundJobExecutionGate>()
            .ToList();

        var job = await db.BackgroundJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (job == null)
            return 0;

        _heartbeatState.SetCurrentJob(job.Id, job.JobType, job.Queue);
        var maxRunningTime = GetMaxRunningTime();
        using var timeoutCts = new CancellationTokenSource(maxRunningTime);
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var cancellationWatcher = WatchCancellationRequestsAsync(job.Id, jobCts, ct);

        using var activity = AtlasTelemetry.ActivitySource.StartActivity(
            "atlas.background_job.execute",
            ActivityKind.Consumer);
        activity?.SetTag("atlas.background_job.id", job.Id);
        activity?.SetTag("atlas.background_job.type", job.JobType);
        activity?.SetTag("atlas.background_job.queue", job.Queue);
        activity?.SetTag("atlas.background_job.attempt", job.AttemptCount);

        try
        {
            if (job.CancellationRequestedAt.HasValue || job.Status == BackgroundJobStatus.Canceled)
            {
                await MarkCanceledAsync(db, job, ct);
                return 1;
            }

            var gateDecision = await EvaluateExecutionGatesAsync(gates, job, ct);
            if (!gateDecision.IsAllowed)
            {
                await DeferAsync(db, job, gateDecision, ct);
                return 1;
            }

            if (!handlers.TryGetValue(job.JobType, out var handler))
                throw new InvalidOperationException($"No background job handler registered for job type '{job.JobType}'.");

            var result = await handler.HandleAsync(new BackgroundJobExecutionContext(job), jobCts.Token);
            if (!result.Succeeded)
                throw new InvalidOperationException(result.Result ?? $"Background job {job.Id} returned a failed result.");

            if (await IsCancellationRequestedOrCanceledAsync(db, job.Id, ct))
            {
                await MarkCanceledAsync(db, job, ct);
                activity?.SetStatus(ActivityStatusCode.Ok, "Canceled by operator.");
                return 1;
            }

            job.Status = BackgroundJobStatus.Succeeded;
            job.CompletedAtUtc = DateTime.Now;
            job.LockedAtUtc = null;
            job.LockedBy = null;
            job.LastError = null;
            job.NextAttemptAtUtc = null;
            job.Result = Truncate(
                result.Result ?? "Succeeded",
                NormalizeResultMaxCharacters(result.MaxResultCharacters));
            await db.SaveChangesAsync(ct);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return 1;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Timed out.");
            await MarkTimedOutAsync(db, job, maxRunningTime, ct);
            return 1;
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Ok, "Canceled by operator.");
            await MarkCanceledAsync(db, job, ct);
            return 1;
        }
        catch (Exception) when (timeoutCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Timed out.");
            await MarkTimedOutAsync(db, job, maxRunningTime, ct);
            return 1;
        }
        catch (Exception) when (jobCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Ok, "Canceled by operator.");
            await MarkCanceledAsync(db, job, ct);
            return 1;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await MarkFailedAsync(db, job, ex, ct);
            return 1;
        }
        finally
        {
            if (!jobCts.IsCancellationRequested)
                await jobCts.CancelAsync();

            await ObserveCancellationWatcherAsync(cancellationWatcher, ct);
            _heartbeatState.ClearCurrentJob(job.Id);
        }
    }

    private static async Task<BackgroundJobExecutionGateDecision> EvaluateExecutionGatesAsync(
        IEnumerable<IBackgroundJobExecutionGate> gates,
        BackgroundJob job,
        CancellationToken ct)
    {
        foreach (var gate in gates)
        {
            var decision = await gate.EvaluateAsync(job, ct);
            if (!decision.IsAllowed)
                return decision;
        }

        return BackgroundJobExecutionGateDecision.Allow();
    }

    private static async Task<bool> IsCancellationRequestedOrCanceledAsync(
        AtlasGlobalDbContext db,
        long jobId,
        CancellationToken ct)
    {
        return await db.BackgroundJobs
            .AsNoTracking()
            .Where(x => x.Id == jobId)
            .Select(x => x.CancellationRequestedAt.HasValue || x.Status == BackgroundJobStatus.Canceled)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<int> CancelStaleCancellationRequestedJobsAsync(
        AtlasGlobalDbContext db,
        string[] queues,
        DateTime now,
        DateTime staleLockedBefore,
        CancellationToken ct)
    {
        var jobs = await ApplyConfiguredJobTypeScope(db.BackgroundJobs
            .Where(x =>
                queues.Contains(x.Queue) &&
                x.Status == BackgroundJobStatus.Running &&
                x.CompletedAtUtc == null &&
                x.CancellationRequestedAt.HasValue &&
                (x.LockedAtUtc ?? x.StartedAtUtc ?? x.CreatedAt) < staleLockedBefore))
            .ToListAsync(ct);
        if (jobs.Count == 0)
            return 0;

        foreach (var job in jobs)
            MarkCanceled(job, now);

        await db.SaveChangesAsync(ct);

        foreach (var job in jobs)
        {
            _logger.LogInformation(
                "Background job {JobId} ({JobType}) was marked canceled after a stale termination request.",
                job.Id,
                job.JobType);
        }

        return jobs.Count;
    }

    private async Task<int> ForceTerminateTimedOutRunningJobsAsync(
        AtlasGlobalDbContext db,
        string[] queues,
        DateTime now,
        TimeSpan maxRunningTime,
        CancellationToken ct)
    {
        var timeoutBefore = now.Subtract(maxRunningTime);
        var jobs = await ApplyConfiguredJobTypeScope(db.BackgroundJobs
            .Where(x =>
                queues.Contains(x.Queue) &&
                x.Status == BackgroundJobStatus.Running &&
                x.CompletedAtUtc == null &&
                (x.LockedAtUtc ?? x.StartedAtUtc ?? x.CreatedAt) < timeoutBefore))
            .ToListAsync(ct);
        if (jobs.Count == 0)
            return 0;

        foreach (var job in jobs)
            MarkTimedOut(job, now, maxRunningTime);

        await db.SaveChangesAsync(ct);

        foreach (var job in jobs)
        {
            _logger.LogError(
                "Background job {JobId} ({JobType}) force terminated after running longer than {MaxRunningSeconds} seconds.",
                job.Id,
                job.JobType,
                (int)maxRunningTime.TotalSeconds);
        }

        return jobs.Count;
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

    private async Task WatchCancellationRequestsAsync(
        long jobId,
        CancellationTokenSource jobCts,
        CancellationToken workerCt)
    {
        using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(jobCts.Token, workerCt);
        var watchToken = watchCts.Token;
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CancellationCheckIntervalSeconds));

        while (!watchToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, watchToken);
            }
            catch (OperationCanceledException) when (watchToken.IsCancellationRequested)
            {
                break;
            }

            if (watchToken.IsCancellationRequested)
                break;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
                var cancellationRequested = await db.BackgroundJobs
                    .AsNoTracking()
                    .Where(x => x.Id == jobId)
                    .Select(x => x.CancellationRequestedAt.HasValue || x.Status == BackgroundJobStatus.Canceled)
                    .FirstOrDefaultAsync(workerCt);

                if (!cancellationRequested)
                    continue;

                _logger.LogInformation("Cancellation requested for background job {JobId}.", jobId);
                await jobCts.CancelAsync();
                return;
            }
            catch (OperationCanceledException) when (workerCt.IsCancellationRequested || jobCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check cancellation request for background job {JobId}.", jobId);
            }
        }
    }

    private static async Task ObserveCancellationWatcherAsync(Task watcher, CancellationToken workerCt)
    {
        try
        {
            await watcher;
        }
        catch (OperationCanceledException) when (workerCt.IsCancellationRequested)
        {
        }
    }

    private static async Task MarkCanceledAsync(
        AtlasGlobalDbContext db,
        BackgroundJob job,
        CancellationToken ct)
    {
        var cancellation = await db.BackgroundJobs
            .AsNoTracking()
            .Where(x => x.Id == job.Id)
            .Select(x => new
            {
                x.CancellationRequestedAt,
                x.CancellationRequestedBy,
                x.CancellationReason
            })
            .FirstOrDefaultAsync(ct);

        var now = DateTime.Now;
        job.CancellationRequestedAt = cancellation?.CancellationRequestedAt ?? job.CancellationRequestedAt ?? now;
        job.CancellationRequestedBy = cancellation?.CancellationRequestedBy ?? job.CancellationRequestedBy;
        job.CancellationReason = cancellation?.CancellationReason ?? job.CancellationReason;
        MarkCanceled(job, now);

        await db.SaveChangesAsync(ct);
    }

    private static void MarkCanceled(
        BackgroundJob job,
        DateTime now)
    {
        job.Status = BackgroundJobStatus.Canceled;
        job.CompletedAtUtc = now;
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.LastError = null;
        job.NextAttemptAtUtc = null;
        job.CancellationRequestedAt ??= now;
        job.Result = string.IsNullOrWhiteSpace(job.CancellationReason)
            ? "Canceled by operator."
            : $"Canceled by operator. Reason: {job.CancellationReason}";
        job.UpdatedAt = now;
    }

    private async Task MarkTimedOutAsync(
        AtlasGlobalDbContext db,
        BackgroundJob job,
        TimeSpan maxRunningTime,
        CancellationToken ct)
    {
        MarkTimedOut(job, DateTime.Now, maxRunningTime);
        await db.SaveChangesAsync(ct);

        _logger.LogError(
            "Background job {JobId} ({JobType}) timed out after running longer than {MaxRunningSeconds} seconds.",
            job.Id,
            job.JobType,
            (int)maxRunningTime.TotalSeconds);
    }

    private static void MarkTimedOut(
        BackgroundJob job,
        DateTime now,
        TimeSpan maxRunningTime)
    {
        var message = $"Force terminated by timeout watchdog: running longer than {FormatDuration(maxRunningTime)}.";
        job.Status = BackgroundJobStatus.Dead;
        job.CompletedAtUtc = now;
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.NextAttemptAtUtc = null;
        job.LastError = Truncate(
            string.IsNullOrWhiteSpace(job.LastError)
                ? message
                : $"{message} Previous error: {job.LastError}",
            4000);
        job.Result = message;
        job.UpdatedAt = now;
    }

    private async Task DeferAsync(
        AtlasGlobalDbContext db,
        BackgroundJob job,
        BackgroundJobExecutionGateDecision decision,
        CancellationToken ct)
    {
        var now = DateTime.Now;
        var reason = string.IsNullOrWhiteSpace(decision.Reason)
            ? "Background job execution deferred."
            : decision.Reason.Trim();

        job.Status = BackgroundJobStatus.Pending;
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.LastError = null;
        job.Result = Truncate(reason, BackgroundJobResultStorageLimits.DefaultMaxCharacters);
        job.NextAttemptAtUtc = decision.DeferUntil ?? now.AddSeconds(Math.Max(1, _options.PollIntervalSeconds));
        job.UpdatedAt = now;
        if (job.AttemptCount > 0)
            job.AttemptCount--;
        if (job.AttemptCount == 0)
            job.StartedAtUtc = null;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Background job {JobId} ({JobType}) deferred by execution gate: {Reason}",
            job.Id,
            job.JobType,
            reason);
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
            job.CompletedAtUtc = DateTime.Now;
            job.NextAttemptAtUtc = null;
            job.Result = "Failed permanently.";
        }
        else
        {
            job.Status = BackgroundJobStatus.Failed;
            // 指数退避减少故障依赖持续异常时的数据库和外部服务压力。
            job.NextAttemptAtUtc = DateTime.Now.Add(GetRetryDelay(job.AttemptCount));
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

    private IQueryable<BackgroundJob> ApplyConfiguredJobTypeScope(IQueryable<BackgroundJob> query)
    {
        var includedJobTypes = _options.IncludedJobTypes ?? [];
        var excludedJobTypes = _options.ExcludedJobTypes ?? [];

        if (includedJobTypes.Length > 0)
            query = query.Where(x => includedJobTypes.Contains(x.JobType));

        if (excludedJobTypes.Length > 0)
            query = query.Where(x => !excludedJobTypes.Contains(x.JobType));

        return query;
    }

    private async Task WaitForNextWorkerTickAsync(
        IReadOnlyCollection<ActiveJob> activeJobs,
        CancellationToken stoppingToken)
    {
        var delay = Task.Delay(
            TimeSpan.FromSeconds(activeJobs.Count == 0 ? Math.Max(1, _options.PollIntervalSeconds) : 1),
            stoppingToken);
        if (activeJobs.Count == 0)
        {
            await delay;
            return;
        }

        var completion = Task.WhenAny(activeJobs.Select(x => x.Task));
        await await Task.WhenAny(delay, completion);
    }

    private async Task<int> ObserveCompletedJobsAsync(
        List<ActiveJob> activeJobs,
        bool waitAll,
        CancellationToken ct)
    {
        if (activeJobs.Count == 0)
            return 0;

        var processed = 0;
        var completedJobs = waitAll
            ? activeJobs.ToArray()
            : activeJobs.Where(x => x.Task.IsCompleted).ToArray();

        foreach (var activeJob in completedJobs)
        {
            try
            {
                processed += await activeJob.Task;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Background job {JobId} ({JobType}) task failed outside handler boundaries.",
                    activeJob.Id,
                    activeJob.JobType);
            }
            finally
            {
                activeJobs.Remove(activeJob);
            }
        }

        return processed;
    }

    private static IReadOnlyDictionary<string, int> BuildActiveJobTypeCounts(
        IEnumerable<ActiveJob> activeJobs)
    {
        return activeJobs
            .GroupBy(x => x.JobType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<BackgroundJob> SelectRunnableCandidates(
        IReadOnlyList<BackgroundJob> candidates,
        int availableSlots,
        IReadOnlyDictionary<string, int> activeByType)
    {
        var maxConcurrency = GetMaxConcurrency();
        var selected = new List<BackgroundJob>(Math.Min(maxConcurrency, Math.Max(0, availableSlots)));
        var selectedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (selected.Count >= availableSlots)
                break;

            var typeLimit = GetJobTypeConcurrencyLimit(candidate.JobType, maxConcurrency);
            activeByType.TryGetValue(candidate.JobType, out var activeForType);
            selectedByType.TryGetValue(candidate.JobType, out var selectedForType);
            if (activeForType + selectedForType >= typeLimit)
                continue;

            selected.Add(candidate);
            selectedByType[candidate.JobType] = selectedForType + 1;
        }

        return selected;
    }

    private int GetMaxConcurrency()
    {
        return Math.Max(1, Math.Min(Math.Max(1, _options.BatchSize), _options.MaxConcurrency));
    }

    private int GetJobTypeConcurrencyLimit(string jobType, int maxConcurrency)
    {
        if (_options.JobTypeConcurrency != null &&
            _options.JobTypeConcurrency.TryGetValue(jobType, out var configured))
        {
            return Math.Clamp(configured, 1, maxConcurrency);
        }

        return maxConcurrency;
    }

    private TimeSpan GetMaxRunningTime()
    {
        return TimeSpan.FromSeconds(Math.Max(1, _options.MaxRunningSeconds));
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var initial = Math.Max(1, _options.InitialRetryDelaySeconds);
        var max = Math.Max(initial, _options.MaxRetryDelaySeconds);
        var seconds = Math.Min(max, initial * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalHours >= 1 && value.TotalHours % 1 == 0)
            return $"{(int)value.TotalHours} hours";

        if (value.TotalMinutes >= 1 && value.TotalMinutes % 1 == 0)
            return $"{(int)value.TotalMinutes} minutes";

        return $"{(int)value.TotalSeconds} seconds";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int NormalizeResultMaxCharacters(int? maxCharacters)
    {
        return Math.Max(1, maxCharacters ?? BackgroundJobResultStorageLimits.DefaultMaxCharacters);
    }

    private sealed record ClaimedJob(long Id, string JobType);

    private sealed record ClaimRunnableJobsResult(
        int MaintenanceProcessed,
        IReadOnlyList<ClaimedJob> Jobs);

    private sealed record ActiveJob(long Id, string JobType, Task<int> Task);
}
