using Atlas.Exporting;

namespace Atlas.ModuleTemplate.Models;

public sealed class ExportTenantRecordsRequest : ExportListRequest<TenantRecordExportCriteria>
{
}

public sealed class TenantRecordExportCriteria
{
    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }
}
