using Atlas.Core.Services;

namespace Atlas.Services;

internal sealed class UserTokenIdentity : ICurrentIdentity
{
    public long? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public long? TenantId { get; set; }
    public long? StoreId { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public string? SessionId { get; set; }
}
