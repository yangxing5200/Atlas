using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Enums;

namespace Atlas.Core.Entities.Global;

public sealed class ExportJob : BaseEntity, ISnowflakeId
{
    public long? BackgroundJobId { get; set; }
    public long TenantId { get; set; }
    public long? StoreId { get; set; }
    public long UserId { get; set; }
    public string ExportTaskType { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string QueryJson { get; set; } = string.Empty;
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Pending;
    public int Progress { get; set; }
    public long ProcessedRows { get; set; }
    public long? TotalRows { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? StorageProvider { get; set; }
    public string? StorageKey { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string QueryHash { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? LastError { get; set; }
}
