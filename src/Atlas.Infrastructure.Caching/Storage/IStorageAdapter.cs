namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// 存储适配器接口
/// </summary>
public interface IStorageAdapter
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default) where T : class;
    
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;
    
    Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan expiration, IEnumerable<string> tags, CancellationToken cancellationToken = default) where T : class;

    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    Task<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    
    Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    Task<long> RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}