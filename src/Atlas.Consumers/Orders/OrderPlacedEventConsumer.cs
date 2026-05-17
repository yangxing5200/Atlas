using System.Text.Json;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Consumers.Orders;

public sealed class OrderPlacedEventConsumer : IConsumer<OrderPlacedEvent>
{
    private const string ConsumerName = nameof(OrderPlacedEventConsumer);

    private readonly ITenantDbContextFactory _dbContextFactory;
    private readonly ILogger<OrderPlacedEventConsumer> _logger;

    public OrderPlacedEventConsumer(
        ITenantDbContextFactory dbContextFactory,
        ILogger<OrderPlacedEventConsumer> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;

        if (!message.TenantId.HasValue)
            throw new InvalidOperationException("Order placed event must include tenant id.");

        var tenantId = message.TenantId.Value;
        var messageId = context.MessageId ?? message.EventId;
        var db = await _dbContextFactory.GetDbContextAsync(tenantId, context.CancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(context.CancellationToken);

        var alreadyConsumed = await db.Set<TenantInboxMessage>()
            .AnyAsync(
                x => x.MessageId == messageId && x.ConsumerName == ConsumerName,
                context.CancellationToken);

        if (alreadyConsumed)
        {
            _logger.LogInformation(
                "Skipped duplicate order placed event {EventId} for consumer {ConsumerName}.",
                messageId,
                ConsumerName);
            return;
        }

        db.Set<TenantInboxMessage>().Add(new TenantInboxMessage
        {
            TenantId = tenantId,
            MessageId = messageId,
            ConsumerName = ConsumerName,
            ReceivedAtUtc = DateTime.UtcNow
        });

        var operationLog = new OperationLog
        {
            TenantId = tenantId,
            StoreId = message.StoreId,
            Module = "Order",
            OperationType = "Placed",
            Description = $"Order {message.OrderNo} was placed.",
            EntityId = message.OrderId,
            Changes = JsonSerializer.Serialize(message, DomainEventJson.Options),
            IsSuccess = true
        };

        db.Set<OperationLog>().Add(operationLog);
        await db.SaveChangesAsync(context.CancellationToken);
        await transaction.CommitAsync(context.CancellationToken);

        _logger.LogInformation(
            "Consumed order placed event {EventId} and wrote operation log {OperationLogId}: tenant={TenantId}, store={StoreId}, order={OrderId}",
            message.EventId,
            operationLog.Id,
            tenantId,
            message.StoreId,
            message.OrderId);
    }
}
