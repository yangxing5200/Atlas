using Atlas.Core.Authorization;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Core.Tests;

public sealed class AtlasDataAccessEvaluatorTests
{
    [Fact]
    public async Task CanAccessAsync_UsesDeclaredStoreField_ForCurrentStoreScope()
    {
        var catalog = new AtlasAuthorizationCatalogBuilder("Test")
            .AddDataResource(
                "order",
                "Order",
                tenantField: nameof(TestResource.TenantKey),
                storeField: nameof(TestResource.ShopId),
                supportedScopes: new[] { AtlasDataScopeType.CurrentStore })
            .Build();
        var evaluator = CreateEvaluator(catalog);
        var resource = new TestResource
        {
            TenantKey = 1,
            ShopId = 7
        };
        var context = new AtlasDataAccessContext(
            TenantId: 1,
            UserId: 10,
            StoreId: 7,
            ResourceCode: "order",
            ScopeType: AtlasDataScopeType.CurrentStore,
            SharedStoreIds: Array.Empty<long>(),
            AssignedStoreIds: Array.Empty<long>());

        var decision = await evaluator.CanAccessAsync(resource, context);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task CanAccessAsync_DeniesUnsupportedScope()
    {
        var catalog = new AtlasAuthorizationCatalogBuilder("Test")
            .AddDataResource(
                "order",
                "Order",
                tenantField: nameof(TestResource.TenantKey),
                storeField: nameof(TestResource.ShopId),
                supportedScopes: new[] { AtlasDataScopeType.CurrentStore })
            .Build();
        var evaluator = CreateEvaluator(catalog);
        var context = new AtlasDataAccessContext(
            TenantId: 1,
            UserId: 10,
            StoreId: null,
            ResourceCode: "order",
            ScopeType: AtlasDataScopeType.Own,
            SharedStoreIds: Array.Empty<long>(),
            AssignedStoreIds: Array.Empty<long>());

        var decision = await evaluator.CanAccessAsync(new TestResource { TenantKey = 1 }, context);

        Assert.False(decision.Allowed);
        Assert.Contains("not supported", decision.Reason);
    }

    [Fact]
    public async Task CanAccessAsync_UsesContributorDecision_ForCustomScope()
    {
        var catalog = new AtlasAuthorizationCatalogBuilder("Test")
            .AddDataResource(
                "record",
                "Record",
                tenantField: nameof(TestResource.TenantKey),
                supportedScopes: new[] { AtlasDataScopeType.Custom })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IAtlasDataScopeContributor<TestResource>>(new AllowingContributor())
            .BuildServiceProvider();
        var evaluator = CreateEvaluator(catalog, services);
        var context = new AtlasDataAccessContext(
            TenantId: 1,
            UserId: 10,
            StoreId: null,
            ResourceCode: "record",
            ScopeType: AtlasDataScopeType.Custom,
            SharedStoreIds: Array.Empty<long>(),
            AssignedStoreIds: Array.Empty<long>());

        var decision = await evaluator.CanAccessAsync(new TestResource { TenantKey = 1 }, context);

        Assert.True(decision.Allowed);
        Assert.Equal("Contributor allowed.", decision.Reason);
    }

    private static AtlasDataAccessEvaluator CreateEvaluator(
        IAtlasAuthorizationCatalog catalog,
        IServiceProvider? serviceProvider = null)
    {
        return new AtlasDataAccessEvaluator(
            catalog,
            serviceProvider ?? new ServiceCollection().BuildServiceProvider());
    }

    private sealed class TestResource
    {
        public long TenantKey { get; init; }

        public long ShopId { get; init; }
    }

    private sealed class AllowingContributor : IAtlasDataScopeContributor<TestResource>
    {
        public ValueTask<AtlasDataAccessDecision> CanAccessAsync(
            TestResource resource,
            AtlasDataAccessContext context,
            CancellationToken ct = default)
        {
            return ValueTask.FromResult(AtlasDataAccessDecision.Allow("Contributor allowed."));
        }
    }
}
