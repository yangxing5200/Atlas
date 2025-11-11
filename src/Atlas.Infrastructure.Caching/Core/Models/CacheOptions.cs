// Core/Models/CacheOptions.cs
using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存选项
    /// </summary>
    public class CacheOptions
    {
        public TimeSpan? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public ISet<string> Tags { get; set; } = new HashSet<string>();
        public CacheScope Scope { get; set; } = CacheScope.Global;
        public int Priority { get; set; } = 1;
        public bool CompressData { get; set; } = false;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        public static CacheOptions Default => new();

        public static CacheOptions WithTags(params string[] tags)
        {
            return new CacheOptions { Tags = new HashSet<string>(tags) };
        }

        public static CacheOptions WithExpiration(TimeSpan expiration)
        {
            return new CacheOptions { AbsoluteExpiration = expiration };
        }
    }
}