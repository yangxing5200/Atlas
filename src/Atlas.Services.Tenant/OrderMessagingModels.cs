using Atlas.Core.Enums;
using Atlas.Messaging.Abstractions;

namespace Atlas.Services.Tenant;

public sealed class PlaceOrderRequest
{
    public string? OrderNo { get; set; }
    public long MemberId { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; } = 1;
}

public sealed class PlaceOrderResult
{
    public long OrderId { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public long TenantId { get; init; }
    public long StoreId { get; init; }
    public long MemberId { get; init; }
    public decimal TotalAmount { get; init; }
    public OrderStatus Status { get; init; }
    public Guid EventId { get; init; }
    public bool EventQueued { get; init; }
    public bool EventPublished { get; init; }
}

public sealed class OrderPlacedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventName => "order.placed";
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public long? TenantId { get; init; }
    public long StoreId { get; init; }
    public long OrderId { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public long MemberId { get; init; }
    public decimal TotalAmount { get; init; }
    public int ItemCount { get; init; }
}

public interface IOrderCommandService
{
    Task<PlaceOrderResult> PlaceAsync(PlaceOrderRequest request, CancellationToken ct = default);
}
