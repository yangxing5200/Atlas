using Atlas.Models.Tenant.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Models.Tenant.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services.Contract
{
    public interface IProductService
    {
        Task<ProductDto?> GetByIdAsync(long id);
        Task<PagedResult<ProductDto>> PageQueryAsync(int pageIndex, int pageSize);

        Task<ProductDto> CreateAsync(CreateProductRequest request);
        Task UpdateAsync(long id, UpdateProductRequest request);
        Task DeleteAsync(long id);
    }
}
