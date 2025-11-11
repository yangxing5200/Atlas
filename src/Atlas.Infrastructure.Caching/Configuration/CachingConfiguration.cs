// Configuration/CachingConfiguration.cs
using System;

namespace Atlas.Infrastructure.Caching.Configuration
{
    public class CachingConfiguration
    {
        public bool EnableCaching { get; set; } = true;
        public bool EnableDistributedCaching { get; set; } = false;
        public bool EnableMultiTenancy { get; set; } = true;
        public bool EnableEntityFrameworkIntegration { get; set; } = true;
        public string? DefaultSerializer { get; set; } = "Json";
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
        public bool EnableStatistics { get; set; } = true;
        public bool EnableDiagnostics { get; set; } = false;
    }
}