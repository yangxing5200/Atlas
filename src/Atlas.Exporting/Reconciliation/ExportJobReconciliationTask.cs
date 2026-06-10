using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Atlas.Exporting.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting.Reconciliation;

public sealed class ExportJobReconciliationTask : IRecurringTask
{
    private const int MaxErrorLength = 4000;
    private const string DefaultCulture = "zh-CN";
    private const string DefaultTimeZone = "China Standard Time";

    private readonly AtlasGlobalDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ExportJobOptions _options;
    private readonly ILogger<ExportJobReconciliationTask> _logger;
    private readonly IReadOnlyDictionary<string, IExportTaskProvider> _providers;
    private readonly IReadOnlyDictionary<string, IExportFormatWriter> _writers;

    public ExportJobReconciliationTask(
        AtlasGlobalDbContext dbContext,
        IBackgroundJobClient backgroundJobs,
        IOptions<ExportJobOptions> options,
        IEnumerable<IExportTaskProvider> providers,
        IEnumerable<IExportFormatWriter> writers,
        ILogger<ExportJobReconciliationTask> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
        _options = options?.Value ?? new ExportJobOptions();
        _providers = BuildProviderMap(providers);
        _writers = BuildWriterMap(writers);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "atlas.export.reconciliation";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _options.Reconciliation.IntervalMinutes));

    public bool RunOnStartup => _options.Reconciliation.RunOnStartup;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled || !_options.Reconciliation.Enabled)
            return;

        var now = DateTime.UtcNow;
        var stalePendingBefore = now.AddMinutes(-Math.Max(1, _options.Reconciliation.StalePendingMinutes));
        var staleRunningBefore = now.AddMinutes(-Math.Max(1, _options.Reconciliation.StaleRunningMinutes));
        var batchSize = Math.Max(1, _options.Reconciliation.BatchSize);

        var jobs = await _dbContext.ExportJobs
            .Where(x =>
                (x.Status == ExportJobStatus.Pending && x.RequestedAtUtc <= stalePendingBefore) ||
                (x.Status == ExportJobStatus.Running && (x.StartedAtUtc ?? x.RequestedAtUtc) <= staleRunningBefore))
            .OrderBy(x => x.RequestedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        var requeued = 0;
        var linked = 0;
        var failed = 0;

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();

            var result = await ReconcileAsync(job, ct);
            if (result == ReconciliationResult.Requeued)
                requeued++;
            else if (result == ReconciliationResult.Linked)
                linked++;
            else if (result == ReconciliationResult.Failed)
                failed++;
        }

        if (requeued > 0 || linked > 0 || failed > 0)
        {
            _logger.LogInformation(
                "Export reconciliation completed; scanned={Scanned}, linked={Linked}, requeued={Requeued}, failed={Failed}.",
                jobs.Count,
                linked,
                requeued,
                failed);
        }
    }

    private async Task<ReconciliationResult> ReconcileAsync(
        ExportJob job,
        CancellationToken ct)
    {
        var backgroundJob = await FindBackgroundJobAsync(job, ct);
        var linkedExistingJob = false;

        if (backgroundJob != null && job.BackgroundJobId != backgroundJob.Id)
        {
            job.BackgroundJobId = backgroundJob.Id;
            await _dbContext.SaveChangesAsync(ct);
            linkedExistingJob = true;

            _logger.LogWarning(
                "Export job {ExportJobId} was linked to existing background job {BackgroundJobId} by reconciliation.",
                job.Id,
                backgroundJob.Id);
        }

        if (backgroundJob is { Status: BackgroundJobStatus.Dead or BackgroundJobStatus.Canceled })
        {
            await MarkFailedAsync(
                job,
                BuildTerminalBackgroundJobMessage(backgroundJob),
                ct);
            return ReconciliationResult.Failed;
        }

        if (backgroundJob == null)
        {
            try
            {
                await RequeueAsync(
                    job,
                    job.BackgroundJobId.HasValue
                        ? $"Background job {job.BackgroundJobId.Value} does not exist."
                        : "Background job id is missing.",
                    ct);
                return ReconciliationResult.Requeued;
            }
            catch (InvalidOperationException ex)
            {
                await MarkFailedAsync(job, ex.Message, ct);
                return ReconciliationResult.Failed;
            }
        }

        return linkedExistingJob
            ? ReconciliationResult.Linked
            : ReconciliationResult.Unchanged;
    }

    private async Task<BackgroundJob?> FindBackgroundJobAsync(
        ExportJob job,
        CancellationToken ct)
    {
        if (job.BackgroundJobId.HasValue)
        {
            var backgroundJob = await _dbContext.BackgroundJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == job.BackgroundJobId.Value, ct);

            if (backgroundJob != null)
                return backgroundJob;
        }

        var payloadMarker = BuildPayloadMarker(job.Id);
        return await _dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x =>
                x.TenantId == job.TenantId &&
                x.JobType == ExportBackgroundJobTypes.Generate &&
                x.Payload.Contains(payloadMarker))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task RequeueAsync(
        ExportJob job,
        string reason,
        CancellationToken ct)
    {
        var provider = GetProvider(job);
        var writer = GetWriter(job);
        var payload = new ExportJobPayload(
            job.Id,
            job.TenantId,
            job.StoreId,
            job.UserId,
            provider.ExportTaskType.Trim(),
            provider.ResourceCode.Trim(),
            provider.PermissionCode.Trim(),
            writer.Format,
            job.QueryJson,
            job.QueryHash,
            DefaultCulture,
            DefaultTimeZone,
            NormalizePageSize(_options.DefaultPageSize),
            _options.DefaultMaxRows,
            ExportQuerySerializer.SchemaVersion);

        var result = await _backgroundJobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<ExportJobPayload>
            {
                JobType = ExportBackgroundJobTypes.Generate,
                Queue = ExportBackgroundJobQueues.Export,
                JobName = $"Export {job.ExportTaskType}",
                Payload = payload,
                DeduplicationKey = BuildReconciliationDeduplicationKey(job.Id),
                TenantId = job.TenantId,
                StoreId = job.StoreId
            },
            ct);

        job.BackgroundJobId = result.JobId;
        job.Status = ExportJobStatus.Pending;
        job.Progress = 0;
        job.LastError = null;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Export job {ExportJobId} was requeued by reconciliation. BackgroundJobId={BackgroundJobId}, Reason={Reason}.",
            job.Id,
            result.JobId,
            reason);
    }

    private async Task MarkFailedAsync(
        ExportJob job,
        string reason,
        CancellationToken ct)
    {
        job.Status = ExportJobStatus.Failed;
        job.LastError = Truncate(reason, MaxErrorLength);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Export job {ExportJobId} was marked failed by reconciliation. Reason={Reason}.",
            job.Id,
            reason);
    }

    private IExportTaskProvider GetProvider(ExportJob job)
    {
        if (!_providers.TryGetValue(job.ExportTaskType.Trim(), out var provider))
            throw new InvalidOperationException($"No export provider registered for task type '{job.ExportTaskType}'.");

        if (!string.Equals(job.ResourceCode, provider.ResourceCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export job resource code does not match provider declaration.");

        if (!string.Equals(job.PermissionCode, provider.PermissionCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export job permission code does not match provider declaration.");

        return provider;
    }

    private IExportFormatWriter GetWriter(ExportJob job)
    {
        var format = string.IsNullOrWhiteSpace(job.Format)
            ? _options.DefaultFormat
            : job.Format.Trim().ToLowerInvariant();

        if (!_options.AllowedFormats.Any(x => string.Equals(x, format, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Export format '{format}' is not allowed.");

        if (!_writers.TryGetValue(format, out var writer))
            throw new InvalidOperationException($"No export writer registered for format '{format}'.");

        return writer;
    }

    private int NormalizePageSize(int pageSize)
    {
        var value = pageSize <= 0 ? 500 : pageSize;
        return Math.Min(value, Math.Max(1, _options.MaxPageSize));
    }

    private static string BuildReconciliationDeduplicationKey(long exportJobId)
    {
        return $"export:generate:{exportJobId}";
    }

    private static string BuildPayloadMarker(long exportJobId)
    {
        return $"\"exportJobId\":{exportJobId}";
    }

    private static string BuildTerminalBackgroundJobMessage(BackgroundJob backgroundJob)
    {
        var detail = string.IsNullOrWhiteSpace(backgroundJob.LastError)
            ? backgroundJob.Result
            : backgroundJob.LastError;

        return string.IsNullOrWhiteSpace(detail)
            ? $"Background job {backgroundJob.Id} is {backgroundJob.Status}."
            : $"Background job {backgroundJob.Id} is {backgroundJob.Status}: {detail}";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static IReadOnlyDictionary<string, IExportTaskProvider> BuildProviderMap(
        IEnumerable<IExportTaskProvider> providers)
    {
        return providers
            .Where(x => !string.IsNullOrWhiteSpace(x.ExportTaskType))
            .GroupBy(x => x.ExportTaskType.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IExportFormatWriter> BuildWriterMap(
        IEnumerable<IExportFormatWriter> writers)
    {
        return writers
            .Where(x => !string.IsNullOrWhiteSpace(x.Format))
            .GroupBy(x => x.Format.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private enum ReconciliationResult
    {
        Unchanged,
        Linked,
        Requeued,
        Failed
    }
}
