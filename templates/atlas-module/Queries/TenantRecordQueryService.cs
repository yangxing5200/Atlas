using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.ModuleTemplate.Entities;
using Atlas.ModuleTemplate.Models;
using Atlas.Core.Authorization;

namespace Atlas.ModuleTemplate.Queries;

public sealed class TenantRecordQueryService : ITenantRecordQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private readonly IRepository<TenantRecord> _records;

    public TenantRecordQueryService(IRepository<TenantRecord> records)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
    }

    public async Task<PagedResult<TenantRecordDto>> SearchAsync(TenantRecordSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        var builder = await _records.QueryDataScopeAsync(
            "module-template.tenant-record",
            AtlasDataScopeType.AllTenant,
            ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(record => record.Name.Contains(keyword));
        }

        if (query.IsActive.HasValue)
            builder = builder.Where(record => record.IsActive == query.IsActive.Value);

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderBy(record => record.Name)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(record => new TenantRecordDto
            {
                Id = record.Id,
                Name = record.Name,
                IsActive = record.IsActive
            }, ct);

        return new PagedResult<TenantRecordDto>(total, items, pageIndex, pageSize);
    }
}
