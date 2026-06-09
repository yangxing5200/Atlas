using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Sql;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services.Tenant.Runtime.Messaging;

public sealed class TenantInboxStore : ITenantInboxStore
{
    private readonly ITenantDbContextFactory _dbContextFactory;
    private readonly ITenantSqlExecutor _tenantSqlExecutor;

    public TenantInboxStore(
        ITenantDbContextFactory dbContextFactory,
        ITenantSqlExecutor tenantSqlExecutor)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _tenantSqlExecutor = tenantSqlExecutor ?? throw new ArgumentNullException(nameof(tenantSqlExecutor));
    }

    public async Task<bool> ExecuteOnceAsync(
        long tenantId,
        Guid messageId,
        string consumerName,
        Func<CancellationToken, Task> consume,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentNullException.ThrowIfNull(consume);

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var alreadyConsumed = await db.Set<TenantInboxMessage>()
            .AnyAsync(
                x => x.TenantId == tenantId &&
                     x.MessageId == messageId &&
                     x.ConsumerName == consumerName,
                ct);

        if (alreadyConsumed)
            return false;

        db.Set<TenantInboxMessage>().Add(new TenantInboxMessage
        {
            TenantId = tenantId,
            MessageId = messageId,
            ConsumerName = consumerName,
            ReceivedAtUtc = DateTime.UtcNow
        });

        await consume(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<int> DeleteReceivedBeforeAsync(
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);

        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);
        return await _tenantSqlExecutor.DeleteReceivedInboxMessagesAsync(
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
