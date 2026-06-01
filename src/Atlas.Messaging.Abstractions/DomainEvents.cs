using System.Text.Json;

namespace Atlas.Messaging.Abstractions;

/// <summary>
/// 领域事件的最小公共契约。
/// </summary>
/// <remarks>
/// EventId 用作消息幂等、链路追踪和消息系统 CorrelationId；TenantId 为空表示全局事件。
/// </remarks>
public interface IDomainEvent
{
    Guid EventId { get; }
    string EventName { get; }
    DateTimeOffset OccurredAt { get; }
    long? TenantId { get; }
}

/// <summary>
/// 领域事件传输信封，保留事件元数据并承载原始事件体。
/// </summary>
/// <remarks>
/// 该类型适合用于持久化或跨边界传输；运行时发布通常直接发布具体事件类型，方便消费者按类型订阅。
/// </remarks>
public sealed record DomainEventEnvelope<TEvent>
    where TEvent : IDomainEvent
{
    public DomainEventEnvelope(TEvent data)
    {
        EventId = data.EventId;
        EventName = data.EventName;
        TenantId = data.TenantId;
        OccurredAt = data.OccurredAt;
        Data = data;
    }

    public Guid EventId { get; init; }
    public string EventName { get; init; }
    public long? TenantId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public TEvent Data { get; init; }
}

/// <summary>
/// 应用层发布领域事件的入口。
/// </summary>
/// <remarks>
/// 调用方不应感知底层传输实现；本地开发或禁用消息时可替换为 NoOp 实现。
/// </remarks>
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;

    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>
/// 底层消息传输抽象，供 outbox 分发器等基础设施使用。
/// </summary>
public interface IDomainEventTransport
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>
/// 空实现发布器，用于未启用消息中间件的环境。
/// </summary>
public sealed class NoOpDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Task.CompletedTask;
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 空实现传输层，保持业务代码在无消息服务时仍可启动。
/// </summary>
public sealed class NoOpDomainEventTransport : IDomainEventTransport
{
    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 领域事件序列化的统一选项。
/// </summary>
public static class DomainEventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
