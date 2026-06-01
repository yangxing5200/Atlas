using Atlas.Core.Authorization;
using Atlas.Infrastructure.Security.Permissions;

namespace Atlas.Core.Tests;

public sealed class AtlasDataScopePredicateBuilderTests
{
    [Fact]
    public void BuildPredicate_UsesDeclaredStoreField_ForSharedStores()
    {
        var catalog = new AtlasAuthorizationCatalogBuilder("Test")
            .AddDataResource(
                "invoice",
                "Invoice",
                tenantField: nameof(TestResource.TenantKey),
                storeField: nameof(TestResource.ShopId),
                supportedScopes: new[] { AtlasDataScopeType.SharedStores })
            .Build();
        var builder = new AtlasDataScopePredicateBuilder(catalog);
        var context = new AtlasDataAccessContext(
            TenantId: 1,
            UserId: 10,
            StoreId: null,
            ResourceCode: "invoice",
            ScopeType: AtlasDataScopeType.SharedStores,
            SharedStoreIds: new[] { 3L, 5L },
            AssignedStoreIds: Array.Empty<long>());
        var predicate = builder.BuildPredicate<TestResource>(context).Compile();

        Assert.True(predicate(new TestResource { TenantKey = 1, ShopId = 5 }));
        Assert.False(predicate(new TestResource { TenantKey = 1, ShopId = 6 }));
        Assert.False(predicate(new TestResource { TenantKey = 2, ShopId = 5 }));
    }

    [Fact]
    public void BuildPredicate_ReturnsFalse_WhenScopeIsUnsupported()
    {
        var catalog = new AtlasAuthorizationCatalogBuilder("Test")
            .AddDataResource(
                "invoice",
                "Invoice",
                tenantField: nameof(TestResource.TenantKey),
                storeField: nameof(TestResource.ShopId),
                supportedScopes: new[] { AtlasDataScopeType.CurrentStore })
            .Build();
        var builder = new AtlasDataScopePredicateBuilder(catalog);
        var context = new AtlasDataAccessContext(
            TenantId: 1,
            UserId: 10,
            StoreId: null,
            ResourceCode: "invoice",
            ScopeType: AtlasDataScopeType.SharedStores,
            SharedStoreIds: new[] { 3L },
            AssignedStoreIds: Array.Empty<long>());
        var predicate = builder.BuildPredicate<TestResource>(context).Compile();

        Assert.False(predicate(new TestResource { TenantKey = 1, ShopId = 3 }));
    }

    private sealed class TestResource
    {
        public long TenantKey { get; init; }

        public long ShopId { get; init; }
    }
}
