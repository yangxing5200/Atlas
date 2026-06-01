using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Sql;

public sealed class TenantSqlExecutor : ITenantSqlExecutor
{
    public Task<int> ClaimOutboxMessageAsync(
        DbContext dbContext,
        long tenantId,
        long messageId,
        string workerId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (messageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(messageId), messageId, "Message id must be greater than zero.");

        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be greater than zero.");

        return ExecuteTenantCommandAsync(
            dbContext,
            tenantId,
            $"""
             UPDATE TenantOutboxMessages
                SET ProcessingAtUtc = {now}, ProcessingBy = {workerId}, UpdatedAt = {now}
              WHERE Id = {messageId}
                AND TenantId = {tenantId}
                AND ProcessedAtUtc IS NULL
                AND AttemptCount < {maxAttempts}
                AND (AvailableAtUtc IS NULL OR AvailableAtUtc <= {now})
                AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {now})
                AND (ProcessingAtUtc IS NULL OR ProcessingAtUtc < {staleProcessingBefore})
             """,
            ct);
    }

    public Task<int> DeleteProcessedOutboxMessagesAsync(
        DbContext dbContext,
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default)
    {
        EnsureBatchSize(batchSize);
        return ExecuteTenantCommandAsync(
            dbContext,
            tenantId,
            $"""
             DELETE FROM TenantOutboxMessages
              WHERE TenantId = {tenantId}
                AND ProcessedAtUtc IS NOT NULL
                AND ProcessedAtUtc < {cutoffUtc}
                AND Id IN (
                    SELECT Id
                      FROM (
                          SELECT Id
                            FROM TenantOutboxMessages
                           WHERE TenantId = {tenantId}
                             AND ProcessedAtUtc IS NOT NULL
                             AND ProcessedAtUtc < {cutoffUtc}
                           ORDER BY Id
                           LIMIT {batchSize}
                      ) AS TenantScopedBatch
                )
             """,
            ct);
    }

    public Task<int> DeleteReceivedInboxMessagesAsync(
        DbContext dbContext,
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default)
    {
        EnsureBatchSize(batchSize);
        return ExecuteTenantCommandAsync(
            dbContext,
            tenantId,
            $"""
             DELETE FROM TenantInboxMessages
              WHERE TenantId = {tenantId}
                AND ReceivedAtUtc < {cutoffUtc}
                AND Id IN (
                    SELECT Id
                      FROM (
                          SELECT Id
                            FROM TenantInboxMessages
                           WHERE TenantId = {tenantId}
                             AND ReceivedAtUtc < {cutoffUtc}
                           ORDER BY Id
                           LIMIT {batchSize}
                      ) AS TenantScopedBatch
                )
             """,
            ct);
    }

    private static Task<int> ExecuteTenantCommandAsync(
        DbContext dbContext,
        long tenantId,
        FormattableString command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(command);

        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId), tenantId, "Tenant id must be greater than zero.");

        EnsureTenantPredicate(command.Format);
        return dbContext.Database.ExecuteSqlInterpolatedAsync(command, ct);
    }

    private static void EnsureBatchSize(int batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
    }

    private static void EnsureTenantPredicate(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("SQL command cannot be empty.");

        if (!commandText.Contains("TenantId", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Tenant SQL commands must contain an explicit TenantId predicate. " +
                "Use a dedicated infrastructure method for approved cross-tenant operations.");
        }
    }
}
