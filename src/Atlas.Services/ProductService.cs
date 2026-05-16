using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Services.Abstractions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services
{
    public class ProductService : ServiceBase<Product, ProductDto>, IProductService
    {
        public ProductService(IRepository<Product> repository, IUnitOfWork unitOfWork, IMapper mapper) : base(repository, unitOfWork, mapper)
        {
        }

        public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
        {
            var entity = _mapper.Map<Product>(request);
            entity.Id = 0;
            await _repository.AddAsync(entity, ct);
            await UnitOfWork.SaveChangesAsync(ct);
            return _mapper.Map<ProductDto>(entity);
        }

        public async Task UpdateAsync(long id, UpdateProductRequest request, CancellationToken ct = default)
        {
            var builder = await _repository.QueryTrackingAsync(ct);
            var entity = await builder.Where(product => product.Id == id).FirstOrDefaultAsync(ct);
            if (entity == null)
            {
                throw new Atlas.Core.Exceptions.AtlasException($"实体不存在: {id}");
            }

            _mapper.Map(request, entity);
            await UnitOfWork.SaveChangesAsync(ct);
        }
    }
}
