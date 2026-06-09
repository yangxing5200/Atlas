using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Sql;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services.Tenant.Runtime.Messaging;

public sealed class TenantOutboxStore : ITenantOutboxStore
{
    private readonly ITenantDbContextFactory _dbContextFactory;
    private readonly ITenantSqlExecutor _tenantSqlExecutor;

    public TenantOutboxStore(
        ITenantDbContextFactory dbContextFactory,
        ITenantSqlExecutor tenantSqlExecutor)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _tenantSqlExecutor = tenantSqlExecutor ?? throw new ArgumentNullException(nameof(tenantSqlExecutor));
    }

    public async Task AddAsync(TenantOutboxMessage message, long tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureTenantId(tenantId);

        if (message.TenantId != tenantId)
            throw new InvalidOperationException("Outbox message TenantId must match the target tenant.");

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        await db.Set<TenantOutboxMessage>().AddAsync(message, ct);
    }

    public async Task<List<TenantOutboxMessage>> ListDueAsync(
        long tenantId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        int batchSize,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        return await db.Set<TenantOutboxMessage>()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProcessedAtUtc == null &&
                x.AttemptCount < maxAttempts &&
                (x.AvailableAtUtc == null || x.AvailableAtUtc <= now) &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.ProcessingAtUtc == null || x.ProcessingAtUtc < staleProcessingBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<bool> TryClaimAsync(
        long tenantId,
        long messageId,
        string workerId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        var updated = await _tenantSqlExecutor.ClaimOutboxMessageAsync(
            db,
            tenantId,
            messageId,
            workerId,
            now,
            staleProcessingBefore,
            maxAttempts,
            ct);

        return updated == 1;
    }

    public async Task ReloadAsync(TenantOutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureTenantId(message.TenantId);

        var db = await _dbContextFactory.GetDbContextAsync(message.TenantId, ct);
        await db.Entry(message).ReloadAsync(ct);
    }

    public async Task MarkProcessedAsync(TenantOutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureTenantId(message.TenantId);

        message.ProcessedAtUtc = DateTime.UtcNow;
        message.ProcessingAtUtc = null;
        message.ProcessingBy = null;
        message.LastError = null;

        var db = await _dbContextFactory.GetDbContextAsync(message.TenantId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        TenantOutboxMessage message,
        string lastError,
        DateTime? nextAttemptAtUtc,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureTenantId(message.TenantId);

        message.AttemptCount++;
        message.ProcessingAtUtc = null;
        message.ProcessingBy = null;
        message.LastError = lastError;
        message.NextAttemptAtUtc = nextAttemptAtUtc;

        var db = await _dbContextFactory.GetDbContextAsync(message.TenantId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteProcessedBeforeAsync(
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        return await _tenantSqlExecutor.DeleteProcessedOutboxMessagesAsync(
            db,
            tenantId,
            cutoffUtc,
            batchSize,
            ct);
    }

    private static void EnsureTenantId(long tenantId)
    {
        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId), "Tenant id must be greater than zero.");
    }
}
