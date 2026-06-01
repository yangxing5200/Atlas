using Atlas.Core.Entities.Tenant;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Services.Abstractions;
using AutoMapper;

namespace Atlas.Services
{
    public class StoreService : ServiceBase<Store, StoreDto>, IStoreService
    {
        private readonly ICacheService _cacheService;
        private readonly ICurrentIdentity _currentIdentity;

        public StoreService(
            IRepository<Store> repository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICacheService cacheService,
            ICurrentIdentity currentIdentity) : base(repository, unitOfWork, mapper)
        {
            _cacheService = cacheService;
            _currentIdentity = currentIdentity;
        }

        public override async Task<StoreDto> AddAsync(StoreDto dto, CancellationToken ct = default)
        {
            var result = await base.AddAsync(dto, ct);
            await InvalidateTenantStoreScopeAsync(result.TenantId, ct);
            return result;
        }

        public override async Task UpdateAsync(long id, StoreDto dto, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            var tenantId = entity?.TenantId;

            await base.UpdateAsync(id, dto, ct);
            await InvalidateTenantStoreScopeAsync(tenantId, ct);
        }

        public override async Task RemoveAsync(long id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            var tenantId = entity?.TenantId;

            await base.RemoveAsync(id, ct);
            await InvalidateTenantStoreScopeAsync(tenantId, ct);
        }

        private async Task InvalidateTenantStoreScopeAsync(long? fallbackTenantId, CancellationToken ct)
        {
            var tenantId = _currentIdentity.TenantId ?? fallbackTenantId;
            if (!tenantId.HasValue || tenantId.Value <= 0)
                return;

            await _cacheService.InvalidateTenantAsync(tenantId.Value.ToString(), ct);
        }
    }
}
