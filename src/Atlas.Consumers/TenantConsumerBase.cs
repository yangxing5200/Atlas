using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;
using MassTransit;

namespace Atlas.Consumers;

public abstract class TenantConsumerBase<TEvent> : IConsumer<TEvent>
    where TEvent : class, IDomainEvent
{
    private readonly ITenantConsumerRuntime _runtime;

    protected TenantConsumerBase(ITenantConsumerRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    protected abstract string ConsumerName { get; }

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? message.EventId;

        await _runtime.ConsumeAsync(
            message,
            messageId,
            ConsumerName,
            (tenantId, consumedMessageId, ct) => ConsumeTenantMessageAsync(context, tenantId, consumedMessageId, ct),
            context.CancellationToken);
    }

    protected abstract Task ConsumeTenantMessageAsync(
        ConsumeContext<TEvent> context,
        long tenantId,
        Guid messageId,
        CancellationToken ct);
}
