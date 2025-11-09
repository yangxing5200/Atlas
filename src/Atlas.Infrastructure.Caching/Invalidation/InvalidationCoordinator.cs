using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 失效协调器
/// </summary>
public class InvalidationCoordinator: IInvalidationCoordinator
{
    private readonly IStorageAdapter _storage;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<InvalidationCoordinator> _logger;

    public InvalidationCoordinator(
        IStorageAdapter storage,
        IMessageBroker messageBroker,
        ILogger<InvalidationCoordinator> logger)
    {
        _storage = storage;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    /// <summary>
    /// 失效指定的键
    /// </summary>
    public async Task InvalidateAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return;

        try
        {
            // 从存储中删除
            await _storage.RemoveManyAsync(keyList, cancellationToken);

            // 发布失效消息给其他服务器
            await _messageBroker.PublishAsync(new InvalidationMessage
            {
                Keys = keyList
            }, cancellationToken);

            _logger.LogInformation("Invalidated {Count} cache keys", keyList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache keys");
            throw;
        }
    }

    /// <summary>
    /// 按模式失效
    /// </summary>
    public async Task InvalidateByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 从存储中删除
            await _storage.RemoveByPatternAsync(pattern, cancellationToken);

            // 发布失效消息
            await _messageBroker.PublishAsync(new InvalidationMessage
            {
                Keys = new List<string> { pattern }
            }, cancellationToken);

            _logger.LogInformation("Invalidated cache by pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache by pattern: {Pattern}", pattern);
            throw;
        }
    }

    /// <summary>
    /// 处理接收到的失效消息
    /// </summary>
    public async Task HandleInvalidationMessageAsync(InvalidationMessage message)
    {
        try
        {
            foreach (var key in message.Keys)
            {
                if (key.Contains('*'))
                {
                    // 模式匹配删除
                    await _storage.RemoveByPatternAsync(key);
                }
                else
                {
                    // 精确删除
                    await _storage.RemoveAsync(key);
                }
            }

            _logger.LogDebug("Handled invalidation message from {SenderId}", message.SenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle invalidation message");
        }
    }
}