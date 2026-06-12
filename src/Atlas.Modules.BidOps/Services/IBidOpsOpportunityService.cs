using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsOpportunityService
{
    Task<PagedResult<OpportunityDto>> SearchAsync(OpportunitySearchQuery query, CancellationToken ct = default);

    Task<OpportunityDetailDto?> GetAsync(long id, CancellationToken ct = default);

    Task<OpportunityDto> CreateAsync(CreateOpportunityRequest request, CancellationToken ct = default);

    Task<OpportunityDto> UpdateAsync(long id, UpdateOpportunityRequest request, CancellationToken ct = default);

    Task<OpportunityDto> WatchAsync(long id, WatchOpportunityRequest request, CancellationToken ct = default);

    Task<OpportunityDto> AssessAsync(long id, AssessOpportunityRequest request, CancellationToken ct = default);

    Task<OpportunityDto> ChangeStageAsync(long id, ChangeOpportunityStageRequest request, CancellationToken ct = default);
}
