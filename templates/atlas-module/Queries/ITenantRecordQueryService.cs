using Atlas.Models.Tenant.Responses;
using Atlas.ModuleTemplate.Models;

namespace Atlas.ModuleTemplate.Queries;

public interface ITenantRecordQueryService
{
    Task<PagedResult<TenantRecordDto>> SearchAsync(TenantRecordSearchQuery query, CancellationToken ct = default);
}
