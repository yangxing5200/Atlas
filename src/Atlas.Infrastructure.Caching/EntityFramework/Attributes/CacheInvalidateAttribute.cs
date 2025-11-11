// EntityFramework/Attributes/CacheInvalidateAttribute.cs
using System;

namespace Atlas.Infrastructure.Caching.EntityFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CacheInvalidateAttribute : Attribute
    {
        public string[]? Tags { get; set; }
        public bool InvalidateOnAdd { get; set; } = true;
        public bool InvalidateOnUpdate { get; set; } = true;
        public bool InvalidateOnDelete { get; set; } = true;
    }
}