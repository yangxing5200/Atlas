using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks;

public sealed class RecurringTaskRunnerOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 10;
    public int LockSeconds { get; set; } = 300;
}

public sealed class RecurringTaskContext
{
    public RecurringTaskContext(DateTimeOffset startedAt)
    {
        StartedAt = startedAt;
    }

    public DateTimeOffset StartedAt { get; }
}

public interface IRecurringTask
{
    string Name { get; }
    TimeSpan Interval { get; }
    bool RunOnStartup { get; }
    Task ExecuteAsync(RecurringTaskContext context, CancellationToken ct = default);
}

public sealed class RecurringTaskRunner : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringTaskRunner> _logger;
    private readonly RecurringTaskRunnerOptions _options;
    private readonly Dictionary<string, DateTimeOffset> _nextRuns = new(StringComparer.OrdinalIgnoreCase);

    public RecurringTaskRunner(
        IServiceScopeFactory scopeFactory,
        IOptions<RecurringTaskRunnerOptions> options,
        ILogger<RecurringTaskRunner> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new RecurringTaskRunnerOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Recurring task runner is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteDueTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring task runner cycle failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task ExecuteDueTasksAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var lockProvider = scope.ServiceProvider.GetRequiredService<IDistributedLockProvider>();
        var tasks = scope.ServiceProvider.GetServices<IRecurringTask>().ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var task in tasks)
        {
            if (!_nextRuns.TryGetValue(task.Name, out var nextRun))
            {
                nextRun = task.RunOnStartup ? now : now.Add(NormalizeInterval(task.Interval));
                _nextRuns[task.Name] = nextRun;
            }

            if (nextRun > now)
                continue;

            var lockResource = $"atlas:recurring-task:{task.Name}";
            await using var taskLock = await lockProvider.TryAcquireAsync(
                lockResource,
                TimeSpan.FromSeconds(Math.Max(30, _options.LockSeconds)),
                wait: TimeSpan.Zero,
                ct);

            if (taskLock == null)
            {
                _logger.LogDebug("Recurring task {TaskName} skipped because another worker holds the lock.", task.Name);
                _nextRuns[task.Name] = now.Add(NormalizeInterval(task.Interval));
                continue;
            }

            try
            {
                _logger.LogInformation("Recurring task {TaskName} started.", task.Name);
                await task.ExecuteAsync(new RecurringTaskContext(now), ct);
                _logger.LogInformation("Recurring task {TaskName} completed.", task.Name);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring task {TaskName} failed.", task.Name);
            }
            finally
            {
                _nextRuns[task.Name] = DateTimeOffset.UtcNow.Add(NormalizeInterval(task.Interval));
            }
        }
    }

    private static TimeSpan NormalizeInterval(TimeSpan interval)
    {
        return interval <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : interval;
    }
}
