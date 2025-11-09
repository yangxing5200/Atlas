using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 依赖注册表
/// </summary>
public class DependencyRegistry
{
    private readonly Dictionary<Type, List<CacheKeyDefinition>> _dependencyIndex = new();
    private readonly ILogger<DependencyRegistry> _logger;

    public DependencyRegistry(ILogger<DependencyRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 构建依赖索引
    /// </summary>
    public void BuildIndex(IEnumerable<CacheKeyDefinition> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        _dependencyIndex.Clear();

        foreach (var definition in definitions)
        {
            foreach (var dependency in definition.Dependencies)
            {
                if (!_dependencyIndex.ContainsKey(dependency.EntityType))
                {
                    _dependencyIndex[dependency.EntityType] = new List<CacheKeyDefinition>();
                }

                _dependencyIndex[dependency.EntityType].Add(definition);
            }
        }

        _logger.LogInformation("Built dependency index for {Count} entity types", _dependencyIndex.Count);
    }

    /// <summary>
    /// 获取依赖某个实体类型的所有缓存键定义
    /// </summary>
    public List<CacheKeyDefinition> GetDependentKeys(Type entityType)
    {
        return _dependencyIndex.GetValueOrDefault(entityType, new List<CacheKeyDefinition>());
    }
}