namespace Atlas.Infrastructure.Caching.Loading;

/// <summary>
/// 缓存条目
/// </summary>
public class CacheEntry<T> where T : class
{
    public T? Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}