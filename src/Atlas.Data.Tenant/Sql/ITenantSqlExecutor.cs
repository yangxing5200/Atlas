using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Sql;

public interface ITenantSqlExecutor
{
    Task<int> ClaimOutboxMessageAsync(
        DbContext dbContext,
        long tenantId,
        long messageId,
        string workerId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        CancellationToken ct = default);

    Task<int> DeleteProcessedOutboxMessagesAsync(
        DbContext dbContext,
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default);

    Task<int> DeleteReceivedInboxMessagesAsync(
        DbContext dbContext,
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default);
}
