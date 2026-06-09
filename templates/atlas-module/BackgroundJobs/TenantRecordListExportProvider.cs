using Atlas.Exporting;
using Atlas.ModuleTemplate.Models;
using Atlas.ModuleTemplate.Queries;

namespace Atlas.ModuleTemplate.BackgroundJobs;

public sealed class TenantRecordListExportProvider : ExportTaskProvider<ExportTenantRecordsRequest>
{
    private readonly ITenantRecordQueryService _queries;

    public TenantRecordListExportProvider(ITenantRecordQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public override string ExportTaskType => ModuleTemplateExportTaskTypes.TenantRecordList;

    public override string ResourceCode => "module-template.tenant-record";

    public override string PermissionCode => ModuleTemplatePermissionCodes.TenantRecordsExport;

    public override IReadOnlyList<ExportColumn> Columns { get; } =
    [
        new("id", "Id") { ValueKind = ExportValueKind.Number },
        new("name", "Name"),
        new("isActive", "Is active") { ValueKind = ExportValueKind.Boolean }
    ];

    public override async Task<ExportPage> ReadPageAsync(
        ExportTaskContext<ExportTenantRecordsRequest> context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var criteria = ExportSearchRequest.GetCriteria(context.Query);

        var result = await _queries.SearchAsync(
            new TenantRecordSearchQuery
            {
                Keyword = criteria.Keyword,
                IsActive = criteria.IsActive,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            ct);

        var rows = result.Items
            .Select<TenantRecordDto, IReadOnlyDictionary<string, ExportCellValue>>(record => new Dictionary<string, ExportCellValue>
            {
                ["id"] = record.Id,
                ["name"] = record.Name,
                ["isActive"] = record.IsActive
            })
            .ToArray();

        return new ExportPage(rows, result.Total);
    }
}
