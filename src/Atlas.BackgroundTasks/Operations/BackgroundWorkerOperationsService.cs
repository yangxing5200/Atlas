using System.Text.Json;
using Atlas.Core.Entities.Global;
using Atlas.Data.Global;
using Atlas.Models.Tenant.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks.Operations;

public sealed class BackgroundWorkerOperationsService : IBackgroundWorkerOperationsService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AtlasGlobalDbContext _dbContext;
    private readonly BackgroundJobWorkerOptions _workerOptions;

    public BackgroundWorkerOperationsService(
        AtlasGlobalDbContext dbContext,
        IOptions<BackgroundJobWorkerOptions> workerOptions)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _workerOptions = workerOptions?.Value ?? new BackgroundJobWorkerOptions();
    }

    public async Task<PagedResult<BackgroundWorkerHeartbeatDto>> SearchAsync(
        BackgroundWorkerSearchQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (pageIndex, pageSize) = NormalizePaging(query);
        var now = DateTime.UtcNow;
        var onlineThresholdUtc = GetOnlineThresholdUtc(now);
        var builder = _dbContext.BackgroundWorkerHeartbeats.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.WorkerId.Contains(keyword) ||
                x.HostName.Contains(keyword) ||
                x.RuntimeMode.Contains(keyword) ||
                (x.CurrentJobType != null && x.CurrentJobType.Contains(keyword)) ||
                (x.CurrentQueue != null && x.CurrentQueue.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Queue))
        {
            var queue = query.Queue.Trim();
            builder = builder.Where(x => x.QueuesJson.Contains(queue));
        }

        if (query.OnlineOnly == true)
            builder = builder.Where(x => x.LastSeenAtUtc >= onlineThresholdUtc);

        var total = await builder.CountAsync(ct);
        var workers = await builder
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<BackgroundWorkerHeartbeatDto>(
            total,
            workers.Select(worker => Map(worker, now, onlineThresholdUtc)).ToList(),
            pageIndex,
            pageSize);
    }

    private BackgroundWorkerHeartbeatDto Map(
        BackgroundWorkerHeartbeat worker,
        DateTime now,
        DateTime onlineThresholdUtc)
    {
        return new BackgroundWorkerHeartbeatDto
        {
            Id = worker.Id,
            WorkerId = worker.WorkerId,
            HostName = worker.HostName,
            ProcessId = worker.ProcessId,
            RuntimeMode = worker.RuntimeMode,
            Queues = ParseQueues(worker.QueuesJson),
            OneTimeJobWorkerEnabled = worker.OneTimeJobWorkerEnabled,
            RecurringTaskRunnerEnabled = worker.RecurringTaskRunnerEnabled,
            CurrentJobId = worker.CurrentJobId,
            CurrentJobType = worker.CurrentJobType,
            CurrentQueue = worker.CurrentQueue,
            StartedAtUtc = worker.StartedAtUtc,
            LastSeenAtUtc = worker.LastSeenAtUtc,
            IsOnline = worker.LastSeenAtUtc >= onlineThresholdUtc,
            SecondsSinceLastSeen = Math.Max(0, (long)(now - worker.LastSeenAtUtc).TotalSeconds)
        };
    }

    private DateTime GetOnlineThresholdUtc(DateTime now)
    {
        var thresholdSeconds = Math.Max(60, Math.Max(1, _workerOptions.PollIntervalSeconds) * 3);
        return now.AddSeconds(-thresholdSeconds);
    }

    private static string[] ParseQueues(string queuesJson)
    {
        if (string.IsNullOrWhiteSpace(queuesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(queuesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BackgroundWorkerSearchQuery query)
    {
        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }
}
