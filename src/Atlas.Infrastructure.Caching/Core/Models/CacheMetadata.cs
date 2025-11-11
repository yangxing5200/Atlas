// Core/Models/CacheMetadata.cs
using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存元数据
    /// </summary>
    public class CacheMetadata
    {
        public string Key { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastAccessedAt { get; set; }
        public long AccessCount { get; set; }
        public long Size { get; set; }
        public string? CreatedBy { get; set; }
        public IDictionary<string, string> CustomData { get; set; } = new Dictionary<string, string>();
    }
}