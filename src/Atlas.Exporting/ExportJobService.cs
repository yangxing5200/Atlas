using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting;

public sealed class ExportJobService : IExportJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AtlasGlobalDbContext _db;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IExportProviderRegistry _registry;
    private readonly IExportFileStore _fileStore;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ICurrentIdentity _identity;
    private readonly IIdGenerator _idGenerator;
    private readonly ExportingOptions _options;

    public ExportJobService(
        AtlasGlobalDbContext db,
        IBackgroundJobClient backgroundJobs,
        IExportProviderRegistry registry,
        IExportFileStore fileStore,
        IPermissionChecker permissionChecker,
        ICurrentIdentity identity,
        IIdGenerator idGenerator,
        IOptions<ExportingOptions> options)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _options = options?.Value ?? new ExportingOptions();
    }

    public async Task<ExportEnqueueResult> EnqueueAsync<TQuery>(ExportEnqueueRequest<TQuery> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled)
            throw new InvalidOperationException("Exporting is disabled.");
        if (!_identity.IsAuthenticated || _identity.TenantId is not > 0 || _identity.UserId is not > 0)
            throw new InvalidOperationException("An authenticated tenant user is required to enqueue exports.");

        var provider = _registry.GetProvider(request.ExportTaskType);
        var format = NormalizeFormat(request.Format);
        var writer = _registry.GetWriter(format);
        EnsureFormatAllowed(format);

        var query = SerializeQuery(request.Query, provider.QueryType);
        var permissionContext = new PermissionCheckContext(_identity.TenantId.Value, _identity.UserId.Value, _identity.StoreId, provider.PermissionCode);
        if (!await _permissionChecker.HasPermissionAsync(permissionContext, ct))
            throw new UnauthorizedAccessException($"Current user does not have export permission '{provider.PermissionCode}'.");

        var now = DateTime.UtcNow;
        var exportJob = new ExportJob
        {
            Id = _idGenerator.NextId(),
            TenantId = _identity.TenantId.Value,
            StoreId = _identity.StoreId,
            UserId = _identity.UserId.Value,
            ExportTaskType = provider.ExportTaskType,
            ResourceCode = provider.ResourceCode,
            PermissionCode = provider.PermissionCode,
            Format = format,
            QueryJson = query.Json,
            QueryHash = query.Hash,
            Status = ExportJobStatus.Pending,
            Progress = 0,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddDays(Math.Max(1, _options.RetentionDays)),
            CreatedAt = now
        };

        _db.ExportJobs.Add(exportJob);
        await _db.SaveChangesAsync(ct);

        var payload = new ExportJobPayload(
            exportJob.Id,
            exportJob.TenantId,
            exportJob.StoreId,
            exportJob.UserId,
            _identity.UserName,
            _identity.SessionId,
            exportJob.ExportTaskType,
            exportJob.ResourceCode,
            exportJob.PermissionCode,
            writer.Format,
            query.Json,
            query.Hash,
            Culture: "zh-CN",
            TimeZone: "UTC",
            PageSize: Math.Min(_options.DefaultPageSize, _options.MaxPageSize),
            MaxRows: _options.DefaultMaxRows,
            SchemaVersion: query.SchemaVersion);

        var dedupKey = string.IsNullOrWhiteSpace(request.ClientRequestId)
            ? null
            : $"export:manual:{exportJob.TenantId}:{exportJob.UserId}:{request.ClientRequestId.Trim()}";

        var background = await _backgroundJobs.EnqueueAsync(new EnqueueBackgroundJobRequest<ExportJobPayload>
        {
            JobType = ExportBackgroundJobTypes.Generate,
            Queue = ExportBackgroundJobQueues.Export,
            JobName = $"Export {provider.ExportTaskType}",
            Payload = payload,
            DeduplicationKey = dedupKey,
            TenantId = exportJob.TenantId,
            StoreId = exportJob.StoreId,
            MaxAttempts = 3
        }, ct);

        exportJob.BackgroundJobId = background.JobId;
        exportJob.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ExportEnqueueResult(exportJob.Id, background.JobId, $"/api/exports/{exportJob.Id}");
    }

    public async Task<ExportJobStatusDto?> GetAsync(long exportJobId, CancellationToken ct = default)
    {
        var job = await GetAuthorizedJobQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == exportJobId, ct);
        return job == null ? null : ToDto(job);
    }

    public async Task<ExportDownloadResult> OpenDownloadAsync(long exportJobId, CancellationToken ct = default)
    {
        var job = await GetAuthorizedJobQuery().FirstOrDefaultAsync(x => x.Id == exportJobId, ct)
            ?? throw new InvalidOperationException($"Export job {exportJobId} was not found.");
        if (job.Status != ExportJobStatus.Ready)
            throw new InvalidOperationException($"Export job {exportJobId} is not ready.");
        if (job.ExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException($"Export job {exportJobId} has expired.");
        if (string.IsNullOrWhiteSpace(job.StorageKey) || string.IsNullOrWhiteSpace(job.FileName) || string.IsNullOrWhiteSpace(job.ContentType))
            throw new InvalidOperationException($"Export job {exportJobId} has no downloadable file.");

        var allowed = await _permissionChecker.HasPermissionAsync(
            new PermissionCheckContext(job.TenantId, _identity.UserId ?? 0, _identity.StoreId, job.PermissionCode), ct);
        if (!allowed)
            throw new UnauthorizedAccessException($"Current user does not have export permission '{job.PermissionCode}'.");

        var stream = await _fileStore.OpenReadAsync(job.StorageKey, ct);
        return new ExportDownloadResult(stream, job.ContentType, job.FileName, job.FileSizeBytes);
    }

    private IQueryable<ExportJob> GetAuthorizedJobQuery()
    {
        if (!_identity.IsAuthenticated || _identity.TenantId is not > 0 || _identity.UserId is not > 0)
            throw new InvalidOperationException("An authenticated tenant user is required to access exports.");

        return _db.ExportJobs.Where(x => x.TenantId == _identity.TenantId.Value && x.UserId == _identity.UserId.Value);
    }

    private string NormalizeFormat(string? requested)
    {
        var format = string.IsNullOrWhiteSpace(requested) ? _options.DefaultFormat : requested;
        return string.IsNullOrWhiteSpace(format) ? "csv" : format.Trim().ToLowerInvariant();
    }

    private void EnsureFormatAllowed(string format)
    {
        var allowed = _options.AllowedFormats.Length == 0 ? ["csv"] : _options.AllowedFormats;
        if (!allowed.Contains(format, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Export format '{format}' is not allowed.");
    }

    private static ExportSerializedQuery SerializeQuery<TQuery>(TQuery query, Type providerQueryType)
    {
        var json = JsonSerializer.Serialize(query, providerQueryType, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return new ExportSerializedQuery(json, Convert.ToHexString(hashBytes).ToLowerInvariant(), providerQueryType.FullName ?? providerQueryType.Name, "1");
    }

    private static ExportJobStatusDto ToDto(ExportJob job)
    {
        return new ExportJobStatusDto(
            job.Id,
            job.BackgroundJobId,
            job.ExportTaskType,
            job.ResourceCode,
            job.Format,
            job.Status.ToString(),
            job.Progress,
            job.ProcessedRows,
            job.TotalRows,
            job.FileName,
            job.ExpiresAtUtc,
            job.LastError);
    }
}
