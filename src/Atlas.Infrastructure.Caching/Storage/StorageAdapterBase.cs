namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// 存储适配器基类
/// </summary>
public abstract class StorageAdapterBase : IStorageAdapter
{
    public abstract Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    public abstract Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default) where T : class;
    
    public abstract Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;
    
    public abstract Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;

    public abstract Task SetAsync<T>(string key, T value, TimeSpan expiration, IEnumerable<string> tags, CancellationToken cancellationToken = default) where T : class;

    public abstract Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    public abstract Task<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    
    public abstract Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    public abstract Task<long> RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    public abstract Task ClearAsync(CancellationToken cancellationToken = default);
    
    public abstract Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    protected static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return key.Trim();
    }
}