using System.Text.Json;

namespace Atlas.Exporting;

public static class ExportBackgroundJobTypes
{
    public const string Generate = "export.generate";
}

public static class ExportBackgroundJobQueues
{
    public const string Export = "export";
}

public sealed class ExportEnqueueRequest<TQuery>
{
    public required string ExportTaskType { get; init; }
    public string? Format { get; init; }
    public required TQuery Query { get; init; }
    public string? ClientRequestId { get; init; }
}

public sealed record ExportEnqueueResult(long ExportJobId, long BackgroundJobId, string StatusUrl);

public sealed record ExportJobStatusDto(
    long ExportJobId,
    long? BackgroundJobId,
    string ExportTaskType,
    string ResourceCode,
    string Format,
    string Status,
    int Progress,
    long ProcessedRows,
    long? TotalRows,
    string? FileName,
    DateTime ExpiresAtUtc,
    string? LastError);

public sealed record ExportDownloadResult(Stream Content, string ContentType, string FileName, long? FileSizeBytes);

public interface IExportJobService
{
    Task<ExportEnqueueResult> EnqueueAsync<TQuery>(ExportEnqueueRequest<TQuery> request, CancellationToken ct = default);
    Task<ExportJobStatusDto?> GetAsync(long exportJobId, CancellationToken ct = default);
    Task<ExportDownloadResult> OpenDownloadAsync(long exportJobId, CancellationToken ct = default);
}

public sealed record ExportSerializedQuery(string Json, string Hash, string TypeName, string SchemaVersion);

public sealed record ExportJobPayload(
    long ExportJobId,
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    string? SessionId,
    string ExportTaskType,
    string ResourceCode,
    string PermissionCode,
    string Format,
    string QueryJson,
    string QueryHash,
    string Culture,
    string TimeZone,
    int PageSize,
    int? MaxRows,
    string SchemaVersion);

public sealed record ExportColumn(string Key, string Header, Func<object, object?> ValueAccessor);

public sealed record ExportPage(IReadOnlyCollection<object> Rows, bool HasMore, long? TotalRows = null);

public sealed class ExportTaskContext
{
    public required long ExportJobId { get; init; }
    public required long TenantId { get; init; }
    public long? StoreId { get; init; }
    public required long UserId { get; init; }
    public required string ExportTaskType { get; init; }
    public required string ResourceCode { get; init; }
    public required object Query { get; init; }
}

public sealed class ExportTaskContext<TQuery>
{
    public required long ExportJobId { get; init; }
    public required long TenantId { get; init; }
    public long? StoreId { get; init; }
    public required long UserId { get; init; }
    public required string ExportTaskType { get; init; }
    public required string ResourceCode { get; init; }
    public required TQuery Query { get; init; }
}

public interface IExportTaskProvider
{
    string ExportTaskType { get; }
    string ResourceCode { get; }
    string PermissionCode { get; }
    Type QueryType { get; }
    IReadOnlyList<ExportColumn> Columns { get; }
    Task<ExportPage> ReadPageAsync(ExportTaskContext context, int pageIndex, int pageSize, CancellationToken ct = default);
}

public abstract class ExportTaskProvider<TQuery> : IExportTaskProvider
{
    public abstract string ExportTaskType { get; }
    public abstract string ResourceCode { get; }
    public abstract string PermissionCode { get; }
    public Type QueryType => typeof(TQuery);
    public abstract IReadOnlyList<ExportColumn> Columns { get; }

    public Task<ExportPage> ReadPageAsync(ExportTaskContext context, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        if (context.Query is not TQuery query)
            throw new InvalidOperationException($"Export task '{ExportTaskType}' expected query type '{typeof(TQuery).FullName}'.");

        return ReadPageAsync(new ExportTaskContext<TQuery>
        {
            ExportJobId = context.ExportJobId,
            TenantId = context.TenantId,
            StoreId = context.StoreId,
            UserId = context.UserId,
            ExportTaskType = context.ExportTaskType,
            ResourceCode = context.ResourceCode,
            Query = query
        }, pageIndex, pageSize, ct);
    }

    public abstract Task<ExportPage> ReadPageAsync(ExportTaskContext<TQuery> context, int pageIndex, int pageSize, CancellationToken ct = default);
}

public sealed record ExportStoredFile(string StorageProvider, string StorageKey, long FileSizeBytes, string Sha256);

public interface IExportFileStore
{
    Task<Stream> CreateAsync(string temporaryKey, CancellationToken ct = default);
    Task<ExportStoredFile> CommitAsync(string temporaryKey, string finalKey, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public sealed class ExportWriteContext
{
    public required ExportJobPayload Payload { get; init; }
    public required IExportTaskProvider Provider { get; init; }
    public required object Query { get; init; }
    public required Stream Output { get; init; }
    public required Func<long, long?, int, CancellationToken, Task> ReportProgressAsync { get; init; }
}

public sealed record ExportWriteResult(long RowsWritten);

public interface IExportFormatWriter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }
    Task<ExportWriteResult> WriteAsync(ExportWriteContext context, CancellationToken ct = default);
}
