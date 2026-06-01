using Atlas.Messaging.Abstractions;
using MassTransit;

namespace Atlas.Messaging.RabbitMQ;

/// <summary>
/// 基于 MassTransit 的领域事件发布器，面向应用层直接发布。
/// </summary>
/// <remarks>
/// 使用 IPublishEndpoint 可参与当前 DI 作用域和 MassTransit outbox，适合请求内业务流程。
/// </remarks>
public sealed class MassTransitDomainEventPublisher : IDomainEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitDomainEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        return PublishAsync((IDomainEvent)domainEvent, ct);
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        // 将领域事件元数据写入消息头，消费者可用于幂等、租户路由和链路追踪。
        return _publishEndpoint.Publish(
            (object)domainEvent,
            Pipe.Execute<PublishContext>(context =>
            {
                context.MessageId = domainEvent.EventId;
                context.CorrelationId = domainEvent.EventId;
                context.Headers.Set("Atlas-EventName", domainEvent.EventName);
                context.Headers.Set("Atlas-OccurredAt", domainEvent.OccurredAt);

                if (domainEvent.TenantId.HasValue)
                    context.Headers.Set("Atlas-TenantId", domainEvent.TenantId.Value);
            }),
            ct);
    }
}

/// <summary>
/// 基于 MassTransit Bus 的传输实现，供后台分发器等长生命周期组件使用。
/// </summary>
/// <remarks>
/// IBus 是单例安全的，适合从 outbox dispatcher 这类后台服务中发布消息。
/// </remarks>
public sealed class MassTransitDomainEventTransport : IDomainEventTransport
{
    private readonly IBus _bus;

    public MassTransitDomainEventTransport(IBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return _bus.Publish(
            (object)domainEvent,
            Pipe.Execute<PublishContext>(context =>
            {
                context.MessageId = domainEvent.EventId;
                context.CorrelationId = domainEvent.EventId;
                context.Headers.Set("Atlas-EventName", domainEvent.EventName);
                context.Headers.Set("Atlas-OccurredAt", domainEvent.OccurredAt);

                if (domainEvent.TenantId.HasValue)
                    context.Headers.Set("Atlas-TenantId", domainEvent.TenantId.Value);
            }),
            ct);
    }
}
