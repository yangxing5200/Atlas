using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Exporting;

public sealed class ExportJobHandler : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AtlasGlobalDbContext _db;
    private readonly IExportProviderRegistry _registry;
    private readonly IExportFileStore _fileStore;
    private readonly IExecutionIdentityAccessor _executionIdentityAccessor;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ILogger<ExportJobHandler> _logger;

    public ExportJobHandler(
        AtlasGlobalDbContext db,
        IExportProviderRegistry registry,
        IExportFileStore fileStore,
        IExecutionIdentityAccessor executionIdentityAccessor,
        IPermissionChecker permissionChecker,
        ILogger<ExportJobHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _executionIdentityAccessor = executionIdentityAccessor ?? throw new ArgumentNullException(nameof(executionIdentityAccessor));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => ExportBackgroundJobTypes.Generate;

    public async Task<BackgroundJobExecutionResult> HandleAsync(BackgroundJobExecutionContext context, CancellationToken ct = default)
    {
        var payload = context.GetPayload<ExportJobPayload>();
        var job = await _db.ExportJobs.FirstOrDefaultAsync(x => x.Id == payload.ExportJobId, ct)
            ?? throw new InvalidOperationException($"Export job {payload.ExportJobId} does not exist.");

        try
        {
            job.Status = ExportJobStatus.Running;
            job.StartedAtUtc ??= DateTime.UtcNow;
            job.LastError = null;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            using var identityScope = _executionIdentityAccessor.Begin(new ExecutionIdentitySnapshot(
                payload.TenantId,
                payload.StoreId,
                payload.UserId,
                payload.UserName,
                payload.SessionId,
                IsAuthenticated: true));

            var allowed = await _permissionChecker.HasPermissionAsync(
                new PermissionCheckContext(payload.TenantId, payload.UserId, payload.StoreId, payload.PermissionCode), ct);
            if (!allowed)
                throw new UnauthorizedAccessException($"Export user {payload.UserId} no longer has permission '{payload.PermissionCode}'.");

            var provider = _registry.GetProvider(payload.ExportTaskType);
            if (!string.Equals(provider.ResourceCode, payload.ResourceCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(provider.PermissionCode, payload.PermissionCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Export provider metadata changed for task type '{payload.ExportTaskType}'.");
            }

            var writer = _registry.GetWriter(payload.Format);
            var query = JsonSerializer.Deserialize(payload.QueryJson, provider.QueryType, JsonOptions)
                ?? throw new InvalidOperationException($"Cannot deserialize export query for task type '{payload.ExportTaskType}'.");

            var tempKey = $"exports/tmp/{job.Id}/{context.Job.AttemptCount}/data{writer.FileExtension}";
            var finalKey = $"exports/{job.TenantId}/{DateTime.UtcNow:yyyyMMdd}/{job.Id}/data{writer.FileExtension}";
            await using (var output = await _fileStore.CreateAsync(tempKey, ct))
            {
                var result = await writer.WriteAsync(new ExportWriteContext
                {
                    Payload = payload,
                    Provider = provider,
                    Query = query,
                    Output = output,
                    ReportProgressAsync = async (processed, total, progress, token) =>
                    {
                        job.ProcessedRows = processed;
                        job.TotalRows = total;
                        job.Progress = progress;
                        job.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(token);
                    }
                }, ct);

                job.ProcessedRows = result.RowsWritten;
            }

            var stored = await _fileStore.CommitAsync(tempKey, finalKey, ct);
            job.Status = ExportJobStatus.Ready;
            job.Progress = 100;
            job.FileName = BuildFileName(payload.ExportTaskType, writer.FileExtension);
            job.ContentType = writer.ContentType;
            job.StorageProvider = stored.StorageProvider;
            job.StorageKey = stored.StorageKey;
            job.FileSizeBytes = stored.FileSizeBytes;
            job.Sha256 = stored.Sha256;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Export job {ExportJobId} completed; rows={Rows}; bytes={Bytes}.",
                job.Id,
                job.ProcessedRows,
                job.FileSizeBytes);

            return BackgroundJobExecutionResult.Success($"Export {job.Id} completed; rows={job.ProcessedRows}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            job.Status = ExportJobStatus.Failed;
            job.LastError = ex.Message;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string BuildFileName(string exportTaskType, string extension)
    {
        var safeName = new string(exportTaskType.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "export";
        return $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
    }
}
