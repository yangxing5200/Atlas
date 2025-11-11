// Configuration/CachingOptions.cs
using System;

namespace Atlas.Infrastructure.Caching.Configuration
{
    public class CachingOptions
    {
        public string InstanceName { get; set; } = "atlas";
        public TimeSpan DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan? DefaultSlidingExpiration { get; set; }
        public bool CompressData { get; set; } = false;
        public int CompressionThreshold { get; set; } = 1024; // bytes
        public bool EnableLogging { get; set; } = true;
    }
}