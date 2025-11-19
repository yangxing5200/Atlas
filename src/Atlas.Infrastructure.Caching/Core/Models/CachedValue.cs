using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存值包装器（内部使用，包含 Tag 版本信息）
    /// </summary>
    public class CachedValue<T>
    {
        public T Value { get; set; } = default!;
        public Dictionary<string, long> TagVersions { get; set; } = new();
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public bool IsNull { get; set; }
    }
}