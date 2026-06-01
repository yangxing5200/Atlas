using Atlas.Messaging.Abstractions;

namespace Atlas.Services.Tenant;

public interface ITenantDomainEventOutbox
{
    Task EnqueueAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
