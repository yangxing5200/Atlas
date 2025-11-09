namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 空消息代理 - 用于没有 Redis 的环境（如测试、单机部署）
/// </summary>
public class NullMessageBroker : IMessageBroker
{
    public Task PublishAsync(InvalidationMessage message, CancellationToken cancellationToken = default)
    {
        // 无操作 - 单机环境不需要发布失效消息
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(Func<InvalidationMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        // 无操作 - 单机环境不需要订阅
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        // 无操作
        return Task.CompletedTask;
    }
}