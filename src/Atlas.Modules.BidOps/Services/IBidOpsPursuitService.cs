using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsPursuitService
{
    Task<PagedResult<PursuitDto>> SearchAsync(PursuitSearchQuery query, CancellationToken ct = default);

    Task<PursuitDetailDto?> GetAsync(long id, CancellationToken ct = default);

    Task<PursuitDto> CreateAsync(CreatePursuitRequest request, CancellationToken ct = default);

    Task<PursuitDto> UpdateAsync(long id, UpdatePursuitRequest request, CancellationToken ct = default);

    Task<PursuitDto> ChangeStatusAsync(long id, ChangePursuitStatusRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PursuitTaskDto>> ListTasksAsync(long pursuitId, CancellationToken ct = default);

    Task<PursuitTaskDto> CreateTaskAsync(long pursuitId, CreatePursuitTaskRequest request, CancellationToken ct = default);

    Task<PursuitTaskDto> UpdateTaskAsync(long pursuitId, long taskId, UpdatePursuitTaskRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PursuitFollowRecordDto>> ListFollowRecordsAsync(long pursuitId, CancellationToken ct = default);

    Task<PursuitFollowRecordDto> CreateFollowRecordAsync(long pursuitId, CreatePursuitFollowRecordRequest request, CancellationToken ct = default);
}
