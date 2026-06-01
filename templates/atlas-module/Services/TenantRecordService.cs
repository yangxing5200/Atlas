using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.ModuleTemplate.Entities;
using Atlas.ModuleTemplate.Models;

namespace Atlas.ModuleTemplate.Services;

public sealed class TenantRecordService : ITenantRecordService
{
    private readonly IRepository<TenantRecord> _records;
    private readonly IUnitOfWork _unitOfWork;

    public TenantRecordService(
        IRepository<TenantRecord> records,
        IUnitOfWork unitOfWork)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TenantRecordDto> CreateAsync(CreateTenantRecordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var record = new TenantRecord
        {
            Name = request.Name.Trim(),
            IsActive = true
        };

        await _records.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(record);
    }

    public async Task UpdateAsync(long id, UpdateTenantRecordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = await _records.QueryTrackingAsync(ct);
        var record = await query.Where(item => item.Id == id).FirstOrDefaultAsync(ct);
        if (record == null)
            throw new AtlasException($"Tenant record does not exist: {id}");

        record.Name = request.Name.Trim();
        record.IsActive = request.IsActive;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static TenantRecordDto Map(TenantRecord record)
    {
        return new TenantRecordDto
        {
            Id = record.Id,
            Name = record.Name,
            IsActive = record.IsActive
        };
    }
}
