using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsNoticeService
{
    Task UpdateAsync(long id, UpdateNoticeRequest request, CancellationToken ct = default);
}
