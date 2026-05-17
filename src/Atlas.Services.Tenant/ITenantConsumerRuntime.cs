using Atlas.Messaging.Abstractions;

namespace Atlas.Services.Tenant;

public interface ITenantConsumerRuntime
{
    Task ConsumeAsync<TEvent>(
        TEvent message,
        Guid messageId,
        string consumerName,
        Func<long, Guid, CancellationToken, Task> consume,
        CancellationToken ct)
        where TEvent : class, IDomainEvent;
}
