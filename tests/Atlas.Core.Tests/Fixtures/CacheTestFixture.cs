using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Testing;

namespace Atlas.Core.Tests.Fixtures;

public class CacheTestFixture
{
    public ICacheKeyBuilder KeyBuilder { get; }
    
    public CacheTestFixture()
    {
        var context = new ScopeContext { TenantId = 1, StoreId = 100, UserId = 1000 };
        KeyBuilder = new TestCacheKeyBuilder(context);
    }
}