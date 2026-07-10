using Atlas.Core.Entities.Global;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.BackgroundTasks;

public static class BackgroundJobBusinessLinkBackfillJobTypes
{
    public const string Backfill = "atlas.background-jobs.business-link-backfill";
}

public sealed record BackgroundJobBusinessLinkBackfillJobPayload(
    int BatchSize = 200,
    int MaxRows = 100_000,
    bool IncludeResult = true);

public sealed class BackgroundJobBusinessLinkBackfillEnqueueTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<BackgroundJobBusinessLinkBackfillEnqueueTask> _logger;

    public BackgroundJobBusinessLinkBackfillEnqueueTask(
        IConfiguration configuration,
        IBackgroundJobClient jobs,
        ILogger<BackgroundJobBusinessLinkBackfillEnqueueTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "background-jobs.business-link-backfill.enqueue";
    public TimeSpan Interval => TimeSpan.FromDays(3650);
    public bool RunOnStartup => _configuration.GetValue<bool?>("BackgroundTasks:BusinessLinkBackfill:RunOnStartup") ?? false;

    public async Task ExecuteAsync(RecurringTaskContext context, CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool?>("BackgroundTasks:BusinessLinkBackfill:Enabled").GetValueOrDefault(false))
            return;

        var runId = _configuration["BackgroundTasks:BusinessLinkBackfill:RunId"];
        if (string.IsNullOrWhiteSpace(runId))
            runId = "v1";

        var batchSize = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundTasks:BusinessLinkBackfill:BatchSize") ?? 200,
            1,
            1_000);
        var maxRows = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundTasks:BusinessLinkBackfill:MaxRows") ?? 100_000,
            1,
            5_000_000);
        var includeResult = _configuration.GetValue<bool?>("BackgroundTasks:BusinessLinkBackfill:IncludeResult") ?? true;

        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<BackgroundJobBusinessLinkBackfillJobPayload>
            {
                JobType = BackgroundJobBusinessLinkBackfillJobTypes.Backfill,
                Queue = _configuration["BackgroundTasks:BusinessLinkBackfill:Queue"] ?? BackgroundJobQueues.Default,
                JobName = "Backfill background job business links",
                TenantId = 0,
                DeduplicationKey = $"atlas:background-job-business-link-backfill:{runId.Trim()}",
                Priority = 1,
                MaxAttempts = 1,
                SourceModule = "Atlas",
                BusinessType = "Maintenance",
                CorrelationId = runId.Trim(),
                Payload = new BackgroundJobBusinessLinkBackfillJobPayload(batchSize, maxRows, includeResult)
            },
            ct);

        _logger.LogInformation(
            "Background job business-link backfill enqueue checked. jobId={JobId}, alreadyExists={AlreadyExists}.",
            result.JobId,
            result.AlreadyExists);
    }
}

public sealed class BackgroundJobBusinessLinkBackfillJobHandler : IBackgroundJobHandler
{
    private readonly AtlasGlobalDbContext _dbContext;
    private readonly ILogger<BackgroundJobBusinessLinkBackfillJobHandler> _logger;

    public BackgroundJobBusinessLinkBackfillJobHandler(
        AtlasGlobalDbContext dbContext,
        ILogger<BackgroundJobBusinessLinkBackfillJobHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BackgroundJobBusinessLinkBackfillJobTypes.Backfill;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<BackgroundJobBusinessLinkBackfillJobPayload>();
        var batchSize = Math.Clamp(payload.BatchSize <= 0 ? 200 : payload.BatchSize, 1, 1_000);
        var maxRows = Math.Clamp(payload.MaxRows <= 0 ? 100_000 : payload.MaxRows, 1, 5_000_000);
        var lastId = 0L;
        var scanned = 0;
        var updated = 0;
        var skipped = 0;

        while (scanned < maxRows)
        {
            var take = Math.Min(batchSize, maxRows - scanned);
            var candidates = await LoadCandidatesAsync(lastId, take, ct);
            if (candidates.Count == 0)
                break;

            lastId = candidates[^1].Id;
            scanned += candidates.Count;

            var updates = BuildUpdates(candidates, includeResult: false);
            if (payload.IncludeResult)
            {
                var unresolvedIds = candidates
                    .Where(candidate =>
                        !updates.TryGetValue(candidate.Id, out var update) ||
                        !update.BusinessId.HasValue)
                    .Select(candidate => candidate.Id)
                    .ToArray();
                if (unresolvedIds.Length > 0)
                {
                    var resultRows = await _dbContext.BackgroundJobs
                        .AsNoTracking()
                        .Where(x => unresolvedIds.Contains(x.Id))
                        .Select(x => new ResultCandidate(x.Id, x.Result))
                        .ToListAsync(ct);
                    var resultsById = resultRows.ToDictionary(x => x.Id);
                    var resultCandidates = candidates
                        .Where(candidate => resultsById.ContainsKey(candidate.Id))
                        .Select(candidate => candidate with { Result = resultsById[candidate.Id].Result })
                        .ToList();
                    foreach (var update in BuildUpdates(resultCandidates, includeResult: true))
                        updates[update.Key] = update.Value;
                }
            }

            if (updates.Count > 0)
                updated += await ApplyUpdatesAsync(candidates, updates.Values, ct);

            skipped += candidates.Count - updates.Count;
        }

        _logger.LogInformation(
            "Background job business-link backfill completed. scanned={Scanned}, updated={Updated}, skipped={Skipped}, lastId={LastId}.",
            scanned,
            updated,
            skipped,
            lastId);

        return BackgroundJobExecutionResult.Success(
            $"scanned={scanned};updated={updated};skipped={skipped};lastId={lastId}");
    }

    private async Task<List<JobCandidate>> LoadCandidatesAsync(
        long lastId,
        int take,
        CancellationToken ct)
    {
        return await _dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x =>
                x.Id > lastId &&
                (x.Queue == "bidops" || x.JobType.StartsWith("bidops.")) &&
                (x.SourceModule == null || x.BusinessType == null || x.BusinessId == null || x.CorrelationId == null))
            .OrderBy(x => x.Id)
            .Take(take)
            .Select(x => new JobCandidate(
                x.Id,
                x.JobType,
                x.Queue,
                x.Payload,
                null,
                x.DeduplicationKey,
                x.SourceModule,
                x.BusinessType,
                x.BusinessId,
                x.CorrelationId))
            .ToListAsync(ct);
    }

    private static Dictionary<long, BusinessLinkUpdate> BuildUpdates(
        IReadOnlyCollection<JobCandidate> candidates,
        bool includeResult)
    {
        var updates = new Dictionary<long, BusinessLinkUpdate>();
        foreach (var candidate in candidates)
        {
            var link = BackgroundJobBusinessLinkInference.Infer(
                candidate.JobType,
                candidate.Queue,
                candidate.Payload,
                candidate.Result,
                candidate.DeduplicationKey,
                candidate.SourceModule,
                candidate.BusinessType,
                candidate.BusinessId,
                candidate.CorrelationId,
                includeResult);

            if (!link.HasAnyValue || IsSame(candidate, link))
                continue;

            updates[candidate.Id] = new BusinessLinkUpdate(
                candidate.Id,
                link.SourceModule,
                link.BusinessType,
                link.BusinessId,
                link.CorrelationId);
        }

        return updates;
    }

    private async Task<int> ApplyUpdatesAsync(
        IReadOnlyCollection<JobCandidate> candidates,
        IEnumerable<BusinessLinkUpdate> updates,
        CancellationToken ct)
    {
        var candidatesById = candidates.ToDictionary(x => x.Id);
        var updateList = updates.ToList();
        var updated = 0;
        foreach (var update in updateList)
        {
            if (!candidatesById.TryGetValue(update.Id, out var candidate))
                continue;

            var job = _dbContext.BackgroundJobs.Local.FirstOrDefault(x => x.Id == update.Id);
            if (job == null)
            {
                job = new BackgroundJob { Id = update.Id };
                _dbContext.BackgroundJobs.Attach(job);
            }

            if (!string.Equals(candidate.SourceModule, update.SourceModule, StringComparison.Ordinal))
            {
                job.SourceModule = update.SourceModule;
                _dbContext.Entry(job).Property(x => x.SourceModule).IsModified = true;
            }

            if (!string.Equals(candidate.BusinessType, update.BusinessType, StringComparison.Ordinal))
            {
                job.BusinessType = update.BusinessType;
                _dbContext.Entry(job).Property(x => x.BusinessType).IsModified = true;
            }

            if (candidate.BusinessId != update.BusinessId)
            {
                job.BusinessId = update.BusinessId;
                _dbContext.Entry(job).Property(x => x.BusinessId).IsModified = true;
            }

            if (!string.Equals(candidate.CorrelationId, update.CorrelationId, StringComparison.Ordinal))
            {
                job.CorrelationId = update.CorrelationId;
                _dbContext.Entry(job).Property(x => x.CorrelationId).IsModified = true;
            }

            updated++;
        }

        await _dbContext.SaveChangesAsync(ct);
        foreach (var update in updateList)
        {
            var job = _dbContext.BackgroundJobs.Local.FirstOrDefault(x => x.Id == update.Id);
            if (job != null)
                _dbContext.Entry(job).State = EntityState.Detached;
        }

        return updated;
    }

    private static bool IsSame(JobCandidate candidate, BackgroundJobBusinessLink link)
    {
        return string.Equals(candidate.SourceModule, link.SourceModule, StringComparison.Ordinal) &&
               string.Equals(candidate.BusinessType, link.BusinessType, StringComparison.Ordinal) &&
               candidate.BusinessId == link.BusinessId &&
               string.Equals(candidate.CorrelationId, link.CorrelationId, StringComparison.Ordinal);
    }

    private sealed record JobCandidate(
        long Id,
        string JobType,
        string Queue,
        string Payload,
        string? Result,
        string? DeduplicationKey,
        string? SourceModule,
        string? BusinessType,
        long? BusinessId,
        string? CorrelationId);

    private sealed record ResultCandidate(long Id, string? Result);

    private sealed record BusinessLinkUpdate(
        long Id,
        string? SourceModule,
        string? BusinessType,
        long? BusinessId,
        string? CorrelationId);
}
