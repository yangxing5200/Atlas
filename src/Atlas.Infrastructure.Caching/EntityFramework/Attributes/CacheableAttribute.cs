// EntityFramework/Attributes/CacheableAttribute.cs
using System;

namespace Atlas.Infrastructure.Caching.EntityFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CacheableAttribute : Attribute
    {
        public string[]? Tags { get; set; }
        public int ExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// 指定实体的缓存Key格式
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CacheKeyAttribute : Attribute
    {
        public string KeyFormat { get; }

        public CacheKeyAttribute(string keyFormat)
        {
            KeyFormat = keyFormat ?? throw new ArgumentNullException(nameof(keyFormat));
        }
    }

    /// <summary>
    /// 指定实体的缓存Tag
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CacheTagAttribute : Attribute
    {
        public string Tag { get; }

        public CacheTagAttribute(string tag)
        {
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        }
    }

    /// <summary>
    /// 忽略缓存
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class IgnoreCacheAttribute : Attribute
    {
    }
}