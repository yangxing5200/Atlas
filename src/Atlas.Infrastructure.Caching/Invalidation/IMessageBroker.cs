namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 消息代理接口
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// 发布失效消息
    /// </summary>
    Task PublishAsync(InvalidationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅失效消息
    /// </summary>
    Task SubscribeAsync(Func<InvalidationMessage, Task> handler, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅
    /// </summary>
    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}