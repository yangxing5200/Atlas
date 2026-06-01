namespace Atlas.Services.Tenant.Runtime.Messaging;

public interface ITenantInboxStore
{
    Task<bool> ExecuteOnceAsync(
        long tenantId,
        Guid messageId,
        string consumerName,
        Func<CancellationToken, Task> consume,
        CancellationToken ct = default);

    Task<int> DeleteReceivedBeforeAsync(
        long tenantId,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken ct = default);
}
