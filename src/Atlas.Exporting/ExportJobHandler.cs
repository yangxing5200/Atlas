using Atlas.BackgroundTasks;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Exporting.Internal;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Exporting;

public sealed class ExportJobHandler : IBackgroundJobHandler
{
    private const int MaxErrorLength = 4000;
    private readonly AtlasGlobalDbContext _dbContext;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IExportFileStore _fileStore;
    private readonly ILogger<ExportJobHandler> _logger;
    private readonly IReadOnlyDictionary<string, IExportTaskProvider> _providers;
    private readonly IReadOnlyDictionary<string, IExportFormatWriter> _writers;

    public ExportJobHandler(
        AtlasGlobalDbContext dbContext,
        IExecutionIdentityAccessor identityAccessor,
        IPermissionChecker permissionChecker,
        IEnumerable<IExportTaskProvider> providers,
        IEnumerable<IExportFormatWriter> writers,
        IExportFileStore fileStore,
        ILogger<ExportJobHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providers = BuildProviderMap(providers);
        _writers = BuildWriterMap(writers);
    }

    public string JobType => ExportBackgroundJobTypes.Generate;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = context.GetPayload<ExportJobPayload>();
        var exportJob = await _dbContext.ExportJobs
            .FirstOrDefaultAsync(x => x.Id == payload.ExportJobId, ct)
            ?? throw new InvalidOperationException($"Export job {payload.ExportJobId} does not exist.");

        if (exportJob.Status == ExportJobStatus.Ready)
            return BackgroundJobExecutionResult.Success($"Export job {exportJob.Id} is already ready.");

        try
        {
            var provider = GetProvider(payload.ExportTaskType);
            ValidatePayloadMatchesProvider(payload, provider);
            var writer = GetWriter(payload.Format);
            await EnsureTenantActiveAsync(payload.TenantId, ct);
            await EnsurePermissionAsync(payload, ct);

            var query = ExportQuerySerializer.Deserialize(payload.QueryJson, provider.QueryType);
            using var identityScope = _identityAccessor.Begin(
                new ExecutionIdentitySnapshot(
                    payload.TenantId,
                    payload.StoreId,
                    payload.UserId,
                    string.Empty,
                    null,
                    IsAuthenticated: true));

            exportJob.Status = ExportJobStatus.Running;
            exportJob.Progress = 0;
            exportJob.StartedAtUtc ??= DateTime.UtcNow;
            exportJob.LastError = null;
            await _dbContext.SaveChangesAsync(ct);

            var taskContext = new ExportTaskContext
            {
                ExportJobId = payload.ExportJobId,
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                UserId = payload.UserId,
                ExportTaskType = payload.ExportTaskType,
                ResourceCode = payload.ResourceCode,
                Query = query
            };
            var fileName = BuildFileName(payload.ExportTaskType, payload.ExportJobId, writer.FileExtension);
            var temporaryKey = BuildTemporaryKey(payload.ExportJobId, context.Job.AttemptCount, fileName);
            var finalKey = BuildFinalKey(payload.TenantId, payload.ExportJobId, fileName);

            await using (var output = await _fileStore.CreateAsync(temporaryKey, ct))
            {
                var result = await writer.WriteAsync(
                    new ExportWriteContext
                    {
                        Payload = payload,
                        Provider = provider,
                        TaskContext = taskContext,
                        Output = output,
                        PageSize = payload.PageSize,
                        MaxRows = payload.MaxRows,
                        Culture = payload.Culture,
                        TimeZone = payload.TimeZone
                    },
                    ct);

                exportJob.ProcessedRows = result.ProcessedRows;
                exportJob.TotalRows = result.TotalRows;
            }

            var stored = await _fileStore.CommitAsync(temporaryKey, finalKey, ct);
            exportJob.Status = ExportJobStatus.Ready;
            exportJob.Progress = 100;
            exportJob.FileName = fileName;
            exportJob.ContentType = writer.ContentType;
            exportJob.StorageProvider = stored.StorageProvider;
            exportJob.StorageKey = stored.StorageKey;
            exportJob.FileSizeBytes = stored.FileSizeBytes;
            exportJob.Sha256 = stored.Sha256;
            exportJob.CompletedAtUtc = DateTime.UtcNow;
            exportJob.LastError = null;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Export job {ExportJobId} completed. BackgroundJobId={BackgroundJobId}, TenantId={TenantId}, UserId={UserId}, TaskType={ExportTaskType}, Rows={ProcessedRows}.",
                exportJob.Id,
                context.Job.Id,
                exportJob.TenantId,
                exportJob.UserId,
                exportJob.ExportTaskType,
                exportJob.ProcessedRows);

            return BackgroundJobExecutionResult.Success($"Export job {exportJob.Id} completed; rows={exportJob.ProcessedRows}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            exportJob.Status = ExportJobStatus.Failed;
            exportJob.LastError = Truncate(ex.Message, MaxErrorLength);
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(
                ex,
                "Export job {ExportJobId} failed. BackgroundJobId={BackgroundJobId}, TenantId={TenantId}, UserId={UserId}, TaskType={ExportTaskType}.",
                exportJob.Id,
                context.Job.Id,
                exportJob.TenantId,
                exportJob.UserId,
                exportJob.ExportTaskType);

            throw;
        }
    }

    private async Task EnsureTenantActiveAsync(long tenantId, CancellationToken ct)
    {
        var tenantExists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId && !x.IsDeleted && x.Status == TenantStatus.Active, ct);

        if (!tenantExists)
            throw new InvalidOperationException($"Tenant {tenantId} is not active.");
    }

    private async Task EnsurePermissionAsync(ExportJobPayload payload, CancellationToken ct)
    {
        var allowed = await _permissionChecker.HasPermissionAsync(
            new PermissionCheckContext(
                payload.TenantId,
                payload.UserId,
                payload.StoreId,
                payload.PermissionCode),
            ct);

        if (!allowed)
            throw new InvalidOperationException($"Permission '{payload.PermissionCode}' is required for export execution.");
    }

    private IExportTaskProvider GetProvider(string exportTaskType)
    {
        if (!_providers.TryGetValue(exportTaskType, out var provider))
            throw new InvalidOperationException($"No export provider registered for task type '{exportTaskType}'.");

        return provider;
    }

    private IExportFormatWriter GetWriter(string format)
    {
        if (!_writers.TryGetValue(format, out var writer))
            throw new InvalidOperationException($"No export writer registered for format '{format}'.");

        return writer;
    }

    private static void ValidatePayloadMatchesProvider(
        ExportJobPayload payload,
        IExportTaskProvider provider)
    {
        if (!string.Equals(payload.ResourceCode, provider.ResourceCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export payload resource code does not match provider declaration.");

        if (!string.Equals(payload.PermissionCode, provider.PermissionCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export payload permission code does not match provider declaration.");
    }

    private static string BuildFileName(
        string exportTaskType,
        long exportJobId,
        string extension)
    {
        var name = new string(exportTaskType
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(name))
            name = "export";

        var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";

        return $"{name}-{exportJobId}{normalizedExtension}";
    }

    private static string BuildTemporaryKey(
        long exportJobId,
        int attempt,
        string fileName)
    {
        return $"exports/tmp/{exportJobId}/{Math.Max(1, attempt)}/{fileName}";
    }

    private static string BuildFinalKey(
        long tenantId,
        long exportJobId,
        string fileName)
    {
        return $"exports/{tenantId}/{DateTime.UtcNow:yyyyMMdd}/{exportJobId}/{fileName}";
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
}
