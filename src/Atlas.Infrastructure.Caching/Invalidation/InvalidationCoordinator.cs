using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 失效协调器
/// </summary>
public class InvalidationCoordinator : IInvalidationCoordinator
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
                Keys = keyList,
                InvalidationType = InvalidationType.ExactKeys
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
                Keys = new List<string> { pattern },
                InvalidationType = InvalidationType.Pattern
            }, cancellationToken);

            _logger.LogInformation("Invalidated cache by pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache by pattern: {Pattern}", pattern);
            throw;
        }
    }

    // 按标签失效
    public async Task InvalidateByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var tagList = tags.ToList();
        if (tagList.Count == 0)
            return;

        try
        {
            var deletedCount = await _storage.RemoveByTagsAsync(tagList, cancellationToken);

            await _messageBroker.PublishAsync(new InvalidationMessage
            {
                Tags = tagList,
                InvalidationType = InvalidationType.Tags
            }, cancellationToken);

            _logger.LogInformation("Invalidated {Count} cache entries by tags: {Tags}",
                deletedCount, string.Join(", ", tagList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache by tags: {Tags}",
                string.Join(", ", tagList));
            throw;
        }
    }

    public async Task HandleInvalidationMessageAsync(InvalidationMessage message)
    {
        try
        {
            switch (message.InvalidationType)
            {
                case InvalidationType.ExactKeys:
                    foreach (var key in message.Keys)
                    {
                        await _storage.RemoveAsync(key);
                    }
                    break;

                case InvalidationType.Pattern:
                    foreach (var pattern in message.Keys)
                    {
                        await _storage.RemoveByPatternAsync(pattern);
                    }
                    break;

                case InvalidationType.Tags:
                    if (message.Tags != null && message.Tags.Any())
                    {
                        await _storage.RemoveByTagsAsync(message.Tags);
                    }
                    break;
            }

            _logger.LogDebug("Handled invalidation message from {SenderId}", message.SenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle invalidation message");
        }
    }
}