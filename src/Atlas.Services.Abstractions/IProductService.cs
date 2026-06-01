using Atlas.Core.Entities.Tenant;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Services.Abstractions.Base;

namespace Atlas.Services.Abstractions
{
    public interface IProductService : IServiceBase<Product, ProductDto>
    {
        Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
        Task UpdateAsync(long id, UpdateProductRequest request, CancellationToken ct = default);
    }
}
