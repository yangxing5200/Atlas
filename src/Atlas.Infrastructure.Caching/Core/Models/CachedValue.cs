namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 带 Tag 版本的缓存值包装器
    /// </summary>
    internal class CachedValue<T>
    {
        public T Value { get; set; } = default!;
        public Dictionary<string, long> TagVersions { get; set; } = new();
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}