using Atlas.BackgroundTasks;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting;

public sealed class ExportArtifactCleanupTask : IRecurringTask
{
    private readonly AtlasGlobalDbContext _db;
    private readonly IExportFileStore _fileStore;
    private readonly ExportingOptions _options;
    private readonly ILogger<ExportArtifactCleanupTask> _logger;

    public ExportArtifactCleanupTask(
        AtlasGlobalDbContext db,
        IExportFileStore fileStore,
        IOptions<ExportingOptions> options,
        ILogger<ExportArtifactCleanupTask> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _options = options?.Value ?? new ExportingOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "export-artifact-cleanup";
    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _options.Cleanup.IntervalMinutes));
    public bool RunOnStartup => false;

    public async Task ExecuteAsync(RecurringTaskContext context, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Cleanup.Enabled)
            return;

        var now = DateTime.UtcNow;
        var batchSize = Math.Max(1, _options.Cleanup.BatchSize);
        while (!ct.IsCancellationRequested)
        {
            var jobs = await _db.ExportJobs
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
                    job.UpdatedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    job.LastError = ex.Message;
                    _logger.LogWarning(ex, "Failed to delete export artifact for job {ExportJobId}.", job.Id);
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
