using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Services.Abstractions;
using Atlas.Services.Abstractions.Base;
using AutoMapper;

namespace Atlas.Services
{
    public class ProductService : ServiceBase<Product, ProductDto>, IProductService
    {
        public ProductService(IRepository<Product> repository, IUnitOfWork unitOfWork, IMapper mapper) : base(repository, unitOfWork, mapper)
        {
        }
    }
}
