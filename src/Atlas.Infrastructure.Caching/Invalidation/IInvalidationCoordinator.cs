namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 缓存失效协调器接口
/// </summary>
public interface IInvalidationCoordinator
{
    /// <summary>
    /// 失效指定的键
    /// </summary>
    /// <param name="keys">需要失效的缓存键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InvalidateAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按模式失效
    /// </summary>
    /// <param name="pattern">缓存键模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理接收到的失效消息
    /// </summary>
    /// <param name="message">失效消息</param>
    Task HandleInvalidationMessageAsync(InvalidationMessage message);
}