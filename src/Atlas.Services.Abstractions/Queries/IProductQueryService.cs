using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Models.Tenant.Responses;

namespace Atlas.Services.Abstractions.Queries;

public interface IProductQueryService : IQueryService
{
    Task<PagedResult<ProductDto>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default);
}
