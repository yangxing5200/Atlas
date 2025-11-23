using Atlas.Core.Entities.Tenant;
using Atlas.Models.DTOs;
using Atlas.Services.Abstractions.Base;

namespace Atlas.Services.Abstractions
{
    public interface IProductService: IServiceBase<Product, ProductDto>
    {
    }
}
