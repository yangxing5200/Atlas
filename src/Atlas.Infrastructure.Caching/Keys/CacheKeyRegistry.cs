using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键注册表
/// </summary>
public class CacheKeyRegistry
{
    private readonly Dictionary<string, CacheKeyDefinition> _definitions = new();
    private readonly ILogger<CacheKeyRegistry> _logger;

    public CacheKeyRegistry(ILogger<CacheKeyRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册键定义
    /// </summary>
    public void Register(CacheKeyDefinition definition)
    {
        if (_definitions.ContainsKey(definition.Name))
        {
            throw new InvalidOperationException($"Cache key '{definition.Name}' is already registered");
        }

        _definitions[definition.Name] = definition;
        _logger.LogDebug("Registered cache key: {KeyName}", definition.Name);
    }

    /// <summary>
    /// 获取键定义
    /// </summary>
    public CacheKeyDefinition? Get(string name)
    {
        return _definitions.GetValueOrDefault(name);
    }

    /// <summary>
    /// 获取所有定义
    /// </summary>
    public IEnumerable<CacheKeyDefinition> GetAll()
    {
        return _definitions.Values;
    }
}