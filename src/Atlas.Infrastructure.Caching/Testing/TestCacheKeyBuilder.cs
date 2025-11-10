using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.Caching.Testing;

/// <summary>
/// 测试用键构建器
/// </summary>
public class TestCacheKeyBuilder : ICacheKeyBuilder
{
    private readonly ScopeContext _context;

    public TestCacheKeyBuilder(ScopeContext? context = null)
    {
        _context = context ?? new ScopeContext
        {
            TenantId = 1,
            StoreId = 1,
            UserId = 1
        };
    }

    public CacheKeyInstance Build(CacheKeyDefinition definition, object? instanceValue = null)
    {
        return new CacheKeyInstance(definition, _context, instanceValue);
    }

    public IEnumerable<CacheKeyInstance> BuildMany(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues)
    {
        return instanceValues.Select(value => Build(definition, value));
    }

    public ScopeContext GetCurrentScopeContext(CacheKeyDefinition definition)
    {
        return _context;
    }
}