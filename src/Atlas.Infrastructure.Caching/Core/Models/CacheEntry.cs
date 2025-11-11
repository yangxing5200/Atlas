using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存条目
    /// </summary>
    public class CacheEntry<T>
    {
        public T Value { get; set; } = default!;
        public CacheMetadata Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public ISet<string> Tags { get; set; } = new HashSet<string>();
        public CacheScope Scope { get; set; }
        public IDictionary<string, string> ScopeValues { get; set; } = new Dictionary<string, string>();
    }
}