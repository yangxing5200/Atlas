using Atlas.ModuleTemplate.Models;

namespace Atlas.ModuleTemplate.Services;

public interface ITenantRecordService
{
    Task<TenantRecordDto> CreateAsync(CreateTenantRecordRequest request, CancellationToken ct = default);

    Task UpdateAsync(long id, UpdateTenantRecordRequest request, CancellationToken ct = default);
}
