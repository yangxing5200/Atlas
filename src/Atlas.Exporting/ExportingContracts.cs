using Atlas.Core.Enums;

namespace Atlas.Exporting;

public static class ExportBackgroundJobQueues
{
    public const string Export = "export";
}

public static class ExportBackgroundJobTypes
{
    public const string Generate = "export.generate";
}

public sealed class ExportEnqueueRequest<TQuery>
{
    public required string ExportTaskType { get; init; }
    public string? Format { get; init; }
    public required TQuery Query { get; init; }
    public string? ClientRequestId { get; init; }
}

public sealed record ExportEnqueueResult(
    long ExportJobId,
    long? BackgroundJobId,
    string ExportTaskType,
    string ResourceCode,
    string Format,
    ExportJobStatus Status,
    bool AlreadyExists);

public sealed record ExportJobStatusDto(
    long ExportJobId,
    long? BackgroundJobId,
    string ExportTaskType,
    string ResourceCode,
    string Format,
    ExportJobStatus Status,
    int Progress,
    long ProcessedRows,
    long? TotalRows,
    string? FileName,
    DateTime ExpiresAtUtc,
    string? LastError);

public sealed record ExportDownloadResult(
    Stream Content,
    string FileName,
    string ContentType,
    long? FileSizeBytes);

public interface IExportJobService
{
    Task<ExportEnqueueResult> EnqueueAsync<TQuery>(
        ExportEnqueueRequest<TQuery> request,
        CancellationToken ct = default);

    Task<ExportJobStatusDto?> GetAsync(
        long exportJobId,
        CancellationToken ct = default);

    Task<ExportDownloadResult> OpenDownloadAsync(
        long exportJobId,
        CancellationToken ct = default);
}

public enum ExportValueKind
{
    Auto = 0,
    String = 1,
    Number = 2,
    Boolean = 3,
    Date = 4,
    DateTime = 5,
    DateTimeOffset = 6
}

public sealed record ExportColumn(
    string Field,
    string Title)
{
    public ExportValueKind ValueKind { get; init; } = ExportValueKind.Auto;
    public string? Format { get; init; }
    public string? Group { get; init; }
    public bool Hidden { get; init; }
    public bool IsSensitive { get; init; }
    public int Order { get; init; }
}

public sealed record ExportCellValue(
    object? Value,
    ExportValueKind? ValueKind = null,
    string? Format = null)
{
    public static ExportCellValue Empty { get; } = new ExportCellValue((object?)null);

    public static ExportCellValue From(
        object? value,
        ExportValueKind? valueKind = null,
        string? format = null)
    {
        return new ExportCellValue(value, valueKind, format);
    }

    public static implicit operator ExportCellValue(string? value) => new((object?)value);
    public static implicit operator ExportCellValue(bool value) => new(value, ExportValueKind.Boolean);
    public static implicit operator ExportCellValue(bool? value) => new(value, ExportValueKind.Boolean);
    public static implicit operator ExportCellValue(short value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(short? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(int value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(int? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(long value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(long? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(decimal value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(decimal? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(double value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(double? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(float value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(float? value) => new(value, ExportValueKind.Number);
    public static implicit operator ExportCellValue(DateOnly value) => new(value, ExportValueKind.Date);
    public static implicit operator ExportCellValue(DateOnly? value) => new(value, ExportValueKind.Date);
    public static implicit operator ExportCellValue(DateTime value) => new(value, ExportValueKind.DateTime);
    public static implicit operator ExportCellValue(DateTime? value) => new(value, ExportValueKind.DateTime);
    public static implicit operator ExportCellValue(DateTimeOffset value) => new(value, ExportValueKind.DateTimeOffset);
    public static implicit operator ExportCellValue(DateTimeOffset? value) => new(value, ExportValueKind.DateTimeOffset);
}

public interface IExportColumnSelection
{
    IReadOnlyList<string>? SelectedFields { get; }
}

public interface IExportFormatSelection
{
    string? Format { get; }
}

public interface IExportRequestOptions : IExportFormatSelection, IExportColumnSelection
{
}

public interface IExportSearchRequest<out TCriteria> : IExportRequestOptions
    where TCriteria : class, new()
{
    TCriteria Criteria { get; }
}

public abstract class ExportListRequest<TCriteria> : IExportSearchRequest<TCriteria>
    where TCriteria : class, new()
{
    public string? Format { get; init; }

    public IReadOnlyList<string>? SelectedFields { get; init; }

    public TCriteria Criteria { get; init; } = new();
}

public static class ExportSearchRequest
{
    public static TCriteria GetCriteria<TCriteria>(
        IExportSearchRequest<TCriteria> request)
        where TCriteria : class, new()
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Criteria ?? new TCriteria();
    }
}

public static class ExportColumnSelection
{
    public static IReadOnlyList<ExportColumn> Resolve(
        IReadOnlyList<ExportColumn> columns,
        IReadOnlyList<string>? selectedFields)
    {
        ArgumentNullException.ThrowIfNull(columns);

        if (selectedFields == null || selectedFields.Count == 0)
            return columns;

        var availableColumns = columns
            .Where(column => !column.Hidden && !string.IsNullOrWhiteSpace(column.Title))
            .ToDictionary(column => column.Field.Trim(), StringComparer.OrdinalIgnoreCase);

        var selectedColumns = new List<ExportColumn>(selectedFields.Count);
        var selectedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requestedField in selectedFields)
        {
            if (string.IsNullOrWhiteSpace(requestedField))
                throw new InvalidOperationException("Export selected fields cannot contain blank values.");

            var field = requestedField.Trim();
            if (!selectedSet.Add(field))
                throw new InvalidOperationException($"Export field '{field}' is selected more than once.");

            if (!availableColumns.TryGetValue(field, out var column))
                throw new InvalidOperationException($"Export field '{field}' is not available for this export task.");

            selectedColumns.Add(column with { Order = selectedColumns.Count });
        }

        return selectedColumns;
    }
}

public sealed record ExportPage(
    IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> Rows,
    long? TotalRows = null);

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

    Task<IReadOnlyList<ExportColumn>> ResolveColumnsAsync(
        ExportTaskContext context,
        CancellationToken ct = default);

    Task<ExportPage> ReadPageAsync(
        ExportTaskContext context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
}

public abstract class ExportTaskProvider<TQuery> : IExportTaskProvider
{
    public abstract string ExportTaskType { get; }
    public abstract string ResourceCode { get; }
    public abstract string PermissionCode { get; }
    public Type QueryType => typeof(TQuery);
    public abstract IReadOnlyList<ExportColumn> Columns { get; }

    public Task<IReadOnlyList<ExportColumn>> ResolveColumnsAsync(
        ExportTaskContext context,
        CancellationToken ct = default)
    {
        return ResolveColumnsAsync(
            ToTypedContext(context),
            ct);
    }

    public virtual Task<IReadOnlyList<ExportColumn>> ResolveColumnsAsync(
        ExportTaskContext<TQuery> context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var selectedFields = context.Query is IExportColumnSelection selection
            ? selection.SelectedFields
            : null;

        return Task.FromResult(ExportColumnSelection.Resolve(Columns, selectedFields));
    }

    public Task<ExportPage> ReadPageAsync(
        ExportTaskContext context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        return ReadPageAsync(
            ToTypedContext(context),
            pageIndex,
            pageSize,
            ct);
    }

    public abstract Task<ExportPage> ReadPageAsync(
        ExportTaskContext<TQuery> context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);

    private static ExportTaskContext<TQuery> ToTypedContext(
        ExportTaskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Query is not TQuery query)
            throw new InvalidOperationException($"Export query is not assignable to {typeof(TQuery).FullName}.");

        return new ExportTaskContext<TQuery>
        {
            ExportJobId = context.ExportJobId,
            TenantId = context.TenantId,
            StoreId = context.StoreId,
            UserId = context.UserId,
            ExportTaskType = context.ExportTaskType,
            ResourceCode = context.ResourceCode,
            Query = query
        };
    }
}

public sealed record ExportSerializedQuery(
    string Json,
    string Hash,
    string TypeName,
    string SchemaVersion);

public sealed record ExportJobPayload(
    long ExportJobId,
    long TenantId,
    long? StoreId,
    long UserId,
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

public interface IExportFileStore
{
    Task<Stream> CreateAsync(
        string temporaryKey,
        CancellationToken ct = default);

    Task<ExportStoredFile> CommitAsync(
        string temporaryKey,
        string finalKey,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken ct = default);
}

public sealed record ExportStoredFile(
    string StorageProvider,
    string StorageKey,
    long FileSizeBytes,
    string Sha256);

public interface IExportFormatWriter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }

    Task<ExportWriteResult> WriteAsync(
        ExportWriteContext context,
        CancellationToken ct = default);
}

public sealed class ExportWriteContext
{
    public required ExportJobPayload Payload { get; init; }
    public required IExportTaskProvider Provider { get; init; }
    public required ExportTaskContext TaskContext { get; init; }
    public required Stream Output { get; init; }
    public required int PageSize { get; init; }
    public int? MaxRows { get; init; }
    public required string Culture { get; init; }
    public required string TimeZone { get; init; }
}

public sealed record ExportWriteResult(
    long ProcessedRows,
    long? TotalRows);
