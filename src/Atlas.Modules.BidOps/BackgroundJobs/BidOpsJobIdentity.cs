using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.BackgroundJobs;

internal static class BidOpsJobIdentity
{
    public static IDisposable Begin(
        IExecutionIdentityAccessor accessor,
        BidOpsTenantJobPayload payload)
    {
        return accessor.Begin(new ExecutionIdentitySnapshot(
            payload.TenantId,
            payload.StoreId,
            payload.UserId,
            string.IsNullOrWhiteSpace(payload.UserName) ? "BidOps Worker" : payload.UserName,
            SessionId: null,
            IsAuthenticated: true));
    }
}
