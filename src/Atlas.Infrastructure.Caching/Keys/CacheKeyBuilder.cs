using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键构建器实现
/// </summary>
public class CacheKeyBuilder : ICacheKeyBuilder
{
    private readonly ICurrentIdentity _currentIdentity;
    private readonly ILogger<CacheKeyBuilder> _logger;

    public CacheKeyBuilder(
        ICurrentIdentity currentIdentity,
        ILogger<CacheKeyBuilder> logger)
    {
        _currentIdentity = currentIdentity;
        _logger = logger;
    }

    public CacheKeyInstance Build(CacheKeyDefinition definition, object? instanceValue = null)
    {
        // 验证实例值
        if (definition.InstanceKeyName != null && instanceValue == null)
        {
            throw new ArgumentException(
                $"Cache key '{definition.Name}' requires instance value for '{definition.InstanceKeyName}'",
                nameof(instanceValue));
        }

        // 获取当前上下文
        var context = GetScopeContext(definition.Scope);

        // 构建实例
        return new CacheKeyInstance(definition, context, instanceValue);
    }

    public IEnumerable<CacheKeyInstance> BuildMany(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues)
    {
        var context = GetScopeContext(definition.Scope);
        return instanceValues.Select(value => new CacheKeyInstance(definition, context, value));
    }

    private ScopeContext GetScopeContext(CacheKeyScope scope)
    {
        return scope switch
        {
            CacheKeyScope.Global => new ScopeContext(),
            CacheKeyScope.Tenant => new ScopeContext
            {
                TenantId = _currentIdentity.TenantId
            },
            CacheKeyScope.Store => new ScopeContext
            {
                TenantId = _currentIdentity.TenantId,
                StoreId = _currentIdentity.StoreId
            },
            CacheKeyScope.User => new ScopeContext
            {
                TenantId = _currentIdentity.TenantId,
                StoreId = _currentIdentity.StoreId,
                UserId = _currentIdentity.UserId
            },
            _ => throw new ArgumentException($"Unknown scope: {scope}")
        };
    }
}