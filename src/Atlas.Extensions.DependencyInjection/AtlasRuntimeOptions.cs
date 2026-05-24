namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasCacheSettingsOptions
{
    public const string SectionName = "CacheSettings";

    public string Provider { get; set; } = "Memory";
    public AtlasRedisCacheOptions Redis { get; set; } = new();
    public AtlasHybridCacheOptions Hybrid { get; set; } = new();
}

public sealed class AtlasRedisCacheOptions
{
    public string? ConnectionString { get; set; }
    public string? InstanceName { get; set; }
}

public sealed class AtlasHybridCacheOptions
{
    public string? RedisConnectionString { get; set; }
    public int? L1ExpirationMinutes { get; set; }
}

public sealed class AtlasMessagingOptions
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "None";
}
