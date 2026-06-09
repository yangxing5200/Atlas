namespace Atlas.Core.Context;

/// <summary>
/// Provides the tenant-scoped execution identity without tying callers to HTTP.
/// </summary>
public interface ITenantExecutionContext
{
    long? TenantId { get; }

    long? StoreId { get; }

    long? UserId { get; }

    bool IsAuthenticated { get; }

    string? SessionId { get; }
}
