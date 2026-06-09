using Atlas.Core.Entities.Tenant;

namespace Atlas.Services.Tenant.Runtime.Messaging;

public interface ITenantOutboxStore
{
    Task AddAsync(TenantOutboxMessage message, long tenantId, CancellationToken ct = default);

    Task<List<TenantOutboxMessage>> ListDueAsync(
        long tenantId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        int batchSize,
        CancellationToken ct = default);

    Task<bool> TryClaimAsync(
        long tenantId,
        long messageId,
        string workerId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        CancellationToken ct = default);

    Task ReloadAsync(TenantOutboxMessage message, CancellationToken ct = default);

    Task MarkProcessedAsync(TenantOutboxMessage message, CancellationToken ct = default);

    Task MarkFailedAsync(
        TenantOutboxMessage message,
        string lastError,
        DateTime? nextAttemptAtUtc,
        CancellationToken ct = default);

    Task<int> DeleteProcessedBeforeAsync(
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default);
}
