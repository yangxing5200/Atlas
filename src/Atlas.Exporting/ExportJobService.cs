using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Exporting.Internal;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting;

public sealed class ExportJobService : IExportJobService
{
    private const string DefaultCulture = "zh-CN";
    private const string DefaultTimeZone = "China Standard Time";

    private readonly AtlasGlobalDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IIdGenerator _idGenerator;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ExportJobOptions _options;
    private readonly IReadOnlyDictionary<string, IExportTaskProvider> _providers;
    private readonly IReadOnlyDictionary<string, IExportFormatWriter> _writers;
    private readonly IExportFileStore _fileStore;

    public ExportJobService(
        AtlasGlobalDbContext dbContext,
        IBackgroundJobClient backgroundJobs,
        ICurrentIdentity currentIdentity,
        IIdGenerator idGenerator,
        IPermissionChecker permissionChecker,
        IOptions<ExportJobOptions> options,
        IEnumerable<IExportTaskProvider> providers,
        IEnumerable<IExportFormatWriter> writers,
        IExportFileStore fileStore)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _options = options?.Value ?? new ExportJobOptions();
        _providers = BuildProviderMap(providers);
        _writers = BuildWriterMap(writers);
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    public async Task<ExportEnqueueResult> EnqueueAsync<TQuery>(
        ExportEnqueueRequest<TQuery> request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
            throw new InvalidOperationException("Exporting is disabled.");

        var tenantId = RequirePositive(_currentIdentity.TenantId, "Current tenant id is required.");
        var userId = RequirePositive(_currentIdentity.UserId, "Current user id is required.");
        var storeId = _currentIdentity.StoreId;
        var provider = GetProvider(request.ExportTaskType);
        var writer = GetWriter(NormalizeFormat(ResolveFormat(request)));
        var serializedQuery = ExportQuerySerializer.Serialize(request.Query, provider.QueryType);

        await EnsurePermissionAsync(tenantId, userId, storeId, provider.PermissionCode, ct);

        var deduplicationKey = BuildManualDeduplicationKey(tenantId, userId, request.ClientRequestId);
        if (deduplicationKey != null)
        {
            var existing = await FindExistingByDeduplicationKeyAsync(tenantId, deduplicationKey, ct);
            if (existing != null)
                return ToEnqueueResult(existing, alreadyExists: true);
        }

        var now = DateTime.UtcNow;
        var exportJob = new ExportJob
        {
            Id = _idGenerator.NextId(),
            TenantId = tenantId,
            StoreId = storeId,
            UserId = userId,
            ExportTaskType = provider.ExportTaskType.Trim(),
            ResourceCode = provider.ResourceCode.Trim(),
            PermissionCode = provider.PermissionCode.Trim(),
            Format = writer.Format,
            QueryJson = serializedQuery.Json,
            QueryHash = serializedQuery.Hash,
            Status = ExportJobStatus.Pending,
            Progress = 0,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddDays(Math.Max(1, _options.RetentionDays))
        };

        await _dbContext.ExportJobs.AddAsync(exportJob, ct);
        await _dbContext.SaveChangesAsync(ct);

        var payload = new ExportJobPayload(
            exportJob.Id,
            exportJob.TenantId,
            exportJob.StoreId,
            exportJob.UserId,
            exportJob.ExportTaskType,
            exportJob.ResourceCode,
            exportJob.PermissionCode,
            exportJob.Format,
            exportJob.QueryJson,
            exportJob.QueryHash,
            DefaultCulture,
            DefaultTimeZone,
            NormalizePageSize(_options.DefaultPageSize),
            _options.DefaultMaxRows,
            serializedQuery.SchemaVersion);

        var backgroundResult = await _backgroundJobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<ExportJobPayload>
            {
                JobType = ExportBackgroundJobTypes.Generate,
                Queue = ExportBackgroundJobQueues.Export,
                JobName = $"Export {exportJob.ExportTaskType}",
                Payload = payload,
                DeduplicationKey = deduplicationKey,
                TenantId = tenantId,
                StoreId = storeId
            },
            ct);

        if (backgroundResult.AlreadyExists)
        {
            _dbContext.ExportJobs.Remove(exportJob);
            await _dbContext.SaveChangesAsync(ct);

            var existing = await FindByBackgroundJobIdAsync(backgroundResult.JobId, ct);
            if (existing != null)
                return ToEnqueueResult(existing, alreadyExists: true);
        }

        exportJob.BackgroundJobId = backgroundResult.JobId;
        await _dbContext.SaveChangesAsync(ct);

        return ToEnqueueResult(exportJob, alreadyExists: false);
    }

    public async Task<ExportJobStatusDto?> GetAsync(
        long exportJobId,
        CancellationToken ct = default)
    {
        var job = await _dbContext.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == exportJobId, ct);

        if (job == null)
            return null;

        return await CanReadAsync(job, ct)
            ? ToStatusDto(job)
            : null;
    }

    public async Task<ExportDownloadResult> OpenDownloadAsync(
        long exportJobId,
        CancellationToken ct = default)
    {
        var job = await _dbContext.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == exportJobId, ct)
            ?? throw new InvalidOperationException($"Export job {exportJobId} does not exist.");

        await EnsureCanDownloadAsync(job, ct);

        if (string.IsNullOrWhiteSpace(job.StorageKey))
            throw new InvalidOperationException($"Export job {exportJobId} has no stored file.");

        var stream = await _fileStore.OpenReadAsync(job.StorageKey, ct);
        return new ExportDownloadResult(
            stream,
            job.FileName ?? $"export-{job.Id}.csv",
            job.ContentType ?? "application/octet-stream",
            job.FileSizeBytes);
    }

    private async Task EnsureCanDownloadAsync(ExportJob job, CancellationToken ct)
    {
        var tenantId = RequirePositive(_currentIdentity.TenantId, "Current tenant id is required.");
        var userId = RequirePositive(_currentIdentity.UserId, "Current user id is required.");
        var storeId = _currentIdentity.StoreId;

        if (job.TenantId != tenantId)
            throw new InvalidOperationException("Export job belongs to another tenant.");

        if (job.Status != ExportJobStatus.Ready)
            throw new InvalidOperationException("Export job is not ready.");

        if (job.ExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("Export file has expired.");

        if (job.UserId != userId)
            await EnsurePermissionAsync(tenantId, userId, storeId, AtlasPermissionCodes.TenantAdmin, ct);

        await EnsurePermissionAsync(tenantId, userId, storeId, job.PermissionCode, ct);
    }

    private async Task<bool> CanReadAsync(ExportJob job, CancellationToken ct)
    {
        var tenantId = _currentIdentity.TenantId;
        var userId = _currentIdentity.UserId;
        if (!tenantId.HasValue || tenantId.Value <= 0 || !userId.HasValue || userId.Value <= 0)
            return false;

        if (job.TenantId != tenantId.Value)
            return false;

        if (job.UserId == userId.Value)
            return true;

        return await _permissionChecker.HasPermissionAsync(
            new PermissionCheckContext(
                tenantId.Value,
                userId.Value,
                _currentIdentity.StoreId,
                AtlasPermissionCodes.TenantAdmin),
            ct);
    }

    private async Task EnsurePermissionAsync(
        long tenantId,
        long userId,
        long? storeId,
        string permissionCode,
        CancellationToken ct)
    {
        var allowed = await _permissionChecker.HasPermissionAsync(
            new PermissionCheckContext(tenantId, userId, storeId, permissionCode),
            ct);

        if (!allowed)
            throw new InvalidOperationException($"Permission '{permissionCode}' is required for export.");
    }

    private IExportTaskProvider GetProvider(string exportTaskType)
    {
        if (string.IsNullOrWhiteSpace(exportTaskType))
            throw new ArgumentException("Export task type is required.", nameof(exportTaskType));

        if (!_providers.TryGetValue(exportTaskType.Trim(), out var provider))
            throw new InvalidOperationException($"No export provider registered for task type '{exportTaskType}'.");

        if (string.IsNullOrWhiteSpace(provider.ResourceCode))
            throw new InvalidOperationException($"Export provider '{provider.ExportTaskType}' must declare ResourceCode.");

        if (string.IsNullOrWhiteSpace(provider.PermissionCode))
            throw new InvalidOperationException($"Export provider '{provider.ExportTaskType}' must declare PermissionCode.");

        return provider;
    }

    private IExportFormatWriter GetWriter(string format)
    {
        if (!_options.AllowedFormats.Any(x => string.Equals(x, format, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Export format '{format}' is not allowed.");

        if (!_writers.TryGetValue(format, out var writer))
            throw new InvalidOperationException($"No export writer registered for format '{format}'.");

        return writer;
    }

    private string NormalizeFormat(string? format)
    {
        var value = string.IsNullOrWhiteSpace(format) ? _options.DefaultFormat : format;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Export format is required.");

        return value.Trim().ToLowerInvariant();
    }

    private static string? ResolveFormat<TQuery>(
        ExportEnqueueRequest<TQuery> request)
    {
        if (!string.IsNullOrWhiteSpace(request.Format))
            return request.Format;

        return request.Query is IExportFormatSelection selection
            ? selection.Format
            : null;
    }

    private int NormalizePageSize(int pageSize)
    {
        var value = pageSize <= 0 ? 500 : pageSize;
        return Math.Min(value, Math.Max(1, _options.MaxPageSize));
    }

    private async Task<ExportJob?> FindExistingByDeduplicationKeyAsync(
        long tenantId,
        string deduplicationKey,
        CancellationToken ct)
    {
        var backgroundJob = await _dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.DeduplicationKey == deduplicationKey)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return backgroundJob == null
            ? null
            : await FindByBackgroundJobIdAsync(backgroundJob.Id, ct);
    }

    private Task<ExportJob?> FindByBackgroundJobIdAsync(
        long backgroundJobId,
        CancellationToken ct)
    {
        return _dbContext.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BackgroundJobId == backgroundJobId, ct);
    }

    private static ExportEnqueueResult ToEnqueueResult(ExportJob job, bool alreadyExists)
    {
        return new ExportEnqueueResult(
            job.Id,
            job.BackgroundJobId,
            job.ExportTaskType,
            job.ResourceCode,
            job.Format,
            job.Status,
            alreadyExists);
    }

    private static ExportJobStatusDto ToStatusDto(ExportJob job)
    {
        return new ExportJobStatusDto(
            job.Id,
            job.BackgroundJobId,
            job.ExportTaskType,
            job.ResourceCode,
            job.Format,
            job.Status,
            job.Progress,
            job.ProcessedRows,
            job.TotalRows,
            job.FileName,
            job.ExpiresAtUtc,
            job.LastError);
    }

    private static string? BuildManualDeduplicationKey(
        long tenantId,
        long userId,
        string? clientRequestId)
    {
        return string.IsNullOrWhiteSpace(clientRequestId)
            ? null
            : $"export:manual:{tenantId}:{userId}:{clientRequestId.Trim()}";
    }

    private static long RequirePositive(long? value, string message)
    {
        return value.HasValue && value.Value > 0
            ? value.Value
            : throw new InvalidOperationException(message);
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
