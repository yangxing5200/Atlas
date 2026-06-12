using System.Text.Json;
using Atlas.Core.Entities.Global;
using Atlas.Core.IdGenerators;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks;

public sealed class BackgroundWorkerHeartbeatService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FailureLogInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundWorkerHeartbeatState _state;
    private readonly BackgroundJobWorkerOptions _workerOptions;
    private readonly RecurringTaskRunnerOptions _recurringOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackgroundWorkerHeartbeatService> _logger;
    private DateTime _lastFailureLogAtUtc = DateTime.MinValue;

    public BackgroundWorkerHeartbeatService(
        IServiceScopeFactory scopeFactory,
        BackgroundWorkerHeartbeatState state,
        IOptions<BackgroundJobWorkerOptions> workerOptions,
        IOptions<RecurringTaskRunnerOptions> recurringOptions,
        IConfiguration configuration,
        ILogger<BackgroundWorkerHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _workerOptions = workerOptions?.Value ?? new BackgroundJobWorkerOptions();
        _recurringOptions = recurringOptions?.Value ?? new RecurringTaskRunnerOptions();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_workerOptions.Enabled && !_recurringOptions.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            await BeatAsync(stoppingToken);

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task BeatAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
            var snapshot = _state.Snapshot();
            var now = DateTime.UtcNow;

            var heartbeat = await db.BackgroundWorkerHeartbeats
                .FirstOrDefaultAsync(x => x.WorkerId == snapshot.WorkerId, ct);
            if (heartbeat == null)
            {
                heartbeat = new BackgroundWorkerHeartbeat
                {
                    Id = ids.NextId(),
                    WorkerId = snapshot.WorkerId,
                    CreatedAt = now
                };
                await db.BackgroundWorkerHeartbeats.AddAsync(heartbeat, ct);
            }

            heartbeat.HostName = snapshot.HostName;
            heartbeat.ProcessId = snapshot.ProcessId;
            heartbeat.RuntimeMode = _configuration["Atlas:Runtime:Mode"] ?? string.Empty;
            heartbeat.QueuesJson = JsonSerializer.Serialize(GetEnabledQueues(), JsonOptions);
            heartbeat.OneTimeJobWorkerEnabled = _workerOptions.Enabled;
            heartbeat.RecurringTaskRunnerEnabled = _recurringOptions.Enabled;
            heartbeat.CurrentJobId = snapshot.CurrentJobId;
            heartbeat.CurrentJobType = snapshot.CurrentJobType;
            heartbeat.CurrentQueue = snapshot.CurrentQueue;
            heartbeat.StartedAtUtc = snapshot.StartedAtUtc;
            heartbeat.LastSeenAtUtc = now;
            heartbeat.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - _lastFailureLogAtUtc < FailureLogInterval)
                return;

            _lastFailureLogAtUtc = now;
            _logger.LogWarning(ex, "Background worker heartbeat update failed.");
        }
    }

    private string[] GetEnabledQueues()
    {
        var queues = _workerOptions.Queues?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        return queues.Length == 0 ? [BackgroundJobQueues.Default] : queues;
    }
}
