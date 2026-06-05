using Atlas.BackgroundTasks;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting.Cleanup;

public sealed class ExportArtifactCleanupTask : IRecurringTask
{
    private const int MaxErrorLength = 4000;
    private readonly AtlasGlobalDbContext _dbContext;
    private readonly IExportFileStore _fileStore;
    private readonly ILogger<ExportArtifactCleanupTask> _logger;
    private readonly ExportJobOptions _options;

    public ExportArtifactCleanupTask(
        AtlasGlobalDbContext dbContext,
        IExportFileStore fileStore,
        IOptions<ExportJobOptions> options,
        ILogger<ExportArtifactCleanupTask> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _options = options?.Value ?? new ExportJobOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "atlas.export.artifact-cleanup";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _options.Cleanup.IntervalMinutes));

    public bool RunOnStartup => false;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Cleanup.Enabled)
            return;

        var batchSize = Math.Max(1, _options.Cleanup.BatchSize);
        var now = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var jobs = await _dbContext.ExportJobs
                .Where(x => x.Status == ExportJobStatus.Ready && x.ExpiresAtUtc < now)
                .OrderBy(x => x.ExpiresAtUtc)
                .Take(batchSize)
                .ToListAsync(ct);

            if (jobs.Count == 0)
                return;

            foreach (var job in jobs)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(job.StorageKey))
                        await _fileStore.DeleteAsync(job.StorageKey, ct);

                    job.Status = ExportJobStatus.Expired;
                    job.LastError = null;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    job.LastError = Truncate(ex.Message, MaxErrorLength);
                    _logger.LogWarning(
                        ex,
                        "Failed to delete expired export artifact for export job {ExportJobId}.",
                        job.Id);
                }
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
