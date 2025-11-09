using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Atlas.Data.Tests.Caching;

/// <summary>
/// 依赖注册表完整测试套件（支持表达式树）
/// </summary>
public class DependencyRegistryTests
{
    #region Test Entities

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DiscountPrice { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; }
    }

    private class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ParentId { get; set; }
    }

    private class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    #endregion

    #region Test Helpers

    private DependencyRegistry CreateRegistry()
    {
        var logger = Mock.Of<ILogger<DependencyRegistry>>();
        return new DependencyRegistry(logger);
    }

    private CacheKeyDefinition CreateKeyDefinition(
        string name,
        params CacheDependency[] dependencies)
    {
        var definition = new CacheKeyDefinition(name, CacheKeyScope.Global);
        foreach (var dep in dependencies)
        {
            definition.Dependencies.Add(dep);
        }
        return definition;
    }

    #endregion

    #region 基础索引构建测试

    [Fact]
    public void BuildIndex_EmptyDefinitions_ShouldCreateEmptyIndex()
    {
        // Arrange
        var registry = CreateRegistry();
        var definitions = Array.Empty<CacheKeyDefinition>();

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().BeEmpty();
    }

    [Fact]
    public void BuildIndex_SingleDefinitionWithTypeLevelDependency_ShouldIndexCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductList", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(1);
        keys.Should().Contain(definition);
    }

    [Fact]
    public void BuildIndex_SingleDefinitionWithInstanceLevelDependency_ShouldIndexCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnInstance<Product>(p => p.Id);
        var definition = CreateKeyDefinition("ProductDetails", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(1);
        keys.Should().Contain(definition);
    }

    [Fact]
    public void BuildIndex_SingleDefinitionWithMultipleDependencies_ShouldIndexAllTypes()
    {
        // Arrange
        var registry = CreateRegistry();
        var productDep = CacheDependencyBuilder.OnType<Product>();
        var categoryDep = CacheDependencyBuilder.OnType<Category>();
        var definition = CreateKeyDefinition("ProductWithCategory", productDep, categoryDep);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var productKeys = registry.GetDependentKeys(typeof(Product));
        productKeys.Should().HaveCount(1);
        productKeys.Should().Contain(definition);

        var categoryKeys = registry.GetDependentKeys(typeof(Category));
        categoryKeys.Should().HaveCount(1);
        categoryKeys.Should().Contain(definition);
    }

    [Fact]
    public void BuildIndex_MultipleDefinitionsSameDependency_ShouldIndexAll()
    {
        // Arrange
        var registry = CreateRegistry();
        var dep1 = CacheDependencyBuilder.OnType<Product>();
        var dep2 = CacheDependencyBuilder.OnType<Product>();
        var dep3 = CacheDependencyBuilder.OnType<Product>();

        var def1 = CreateKeyDefinition("ProductList", dep1);
        var def2 = CreateKeyDefinition("ProductDetails", dep2);
        var def3 = CreateKeyDefinition("ProductSearch", dep3);
        var definitions = new[] { def1, def2, def3 };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(3);
        keys.Should().Contain(def1);
        keys.Should().Contain(def2);
        keys.Should().Contain(def3);
    }

    [Fact]
    public void BuildIndex_MultipleDefinitionsDifferentDependencies_ShouldIndexSeparately()
    {
        // Arrange
        var registry = CreateRegistry();
        var productDep = CacheDependencyBuilder.OnType<Product>();
        var categoryDep = CacheDependencyBuilder.OnType<Category>();
        var orderDep = CacheDependencyBuilder.OnType<Order>();

        var productDef = CreateKeyDefinition("ProductList", productDep);
        var categoryDef = CreateKeyDefinition("CategoryList", categoryDep);
        var orderDef = CreateKeyDefinition("OrderList", orderDep);
        var definitions = new[] { productDef, categoryDef, orderDef };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        registry.GetDependentKeys(typeof(Product)).Should().Contain(productDef);
        registry.GetDependentKeys(typeof(Category)).Should().Contain(categoryDef);
        registry.GetDependentKeys(typeof(Order)).Should().Contain(orderDef);
    }

    #endregion

    #region 表达式树特定属性触发测试

    [Fact]
    public void BuildIndex_DependencyWithPropertyTriggers_ShouldStorePropertyNames()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder
            .OnType<Product>()
            .OnPropertiesChange(p => p.Price, p => p.Stock);

        var definition = CreateKeyDefinition("ProductPriceCache", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();

        dep.TriggerProperties.Should().HaveCount(2);
        dep.TriggerProperties.Should().Contain("Price");
        dep.TriggerProperties.Should().Contain("Stock");
    }

    [Fact]
    public void BuildIndex_DependencyWithSinglePropertyTrigger_ShouldStoreCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder
            .OnInstance<Product>(p => p.Id)
            .OnPropertyChange(p => p.Name);

        var definition = CreateKeyDefinition("ProductNameCache", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();

        dep.TriggerProperties.Should().HaveCount(1);
        dep.TriggerProperties.Should().Contain("Name");
    }

    [Fact]
    public void BuildIndex_DependencyWithNoPropertyTriggers_ShouldHaveEmptyList()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductCache", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();

        dep.TriggerProperties.Should().BeEmpty();
    }

    [Fact]
    public void BuildIndex_DependencyWithChainedPropertyTriggers_ShouldStoreAll()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder
            .OnType<Product>()
            .OnPropertyChange(p => p.Price)
            .OnPropertyChange(p => p.DiscountPrice)
            .OnPropertyChange(p => p.Stock)
            .OnPropertyChange(p => p.IsActive);

        var definition = CreateKeyDefinition("ProductDisplayCache", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();

        dep.TriggerProperties.Should().HaveCount(4);
        dep.TriggerProperties.Should().Contain("Price");
        dep.TriggerProperties.Should().Contain("DiscountPrice");
        dep.TriggerProperties.Should().Contain("Stock");
        dep.TriggerProperties.Should().Contain("IsActive");
    }

    #endregion

    #region 实例键选择器测试

    [Fact]
    public void BuildIndex_InstanceDependencyWithKeySelector_ShouldStoreCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnInstance<Product>(p => p.Id);
        var definition = CreateKeyDefinition("ProductById", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();

        dep.Level.Should().Be(DependencyLevel.Instance);
        dep.InstanceKeySelector.Should().NotBeNull();
    }

    [Fact]
    public void BuildIndex_InstanceDependencyWithCompositeKey_ShouldWork()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder
            .OnInstance<Product>(p => new { p.CategoryId, p.Id });

        var definition = CreateKeyDefinition("ProductByCategory", dependency);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(1);

        var returnedDef = keys.First();
        var dep = returnedDef.Dependencies.First();
        dep.InstanceKeySelector.Should().NotBeNull();
    }

    #endregion

    #region 索引重建测试

    [Fact]
    public void BuildIndex_CalledTwice_ShouldReplaceOldIndex()
    {
        // Arrange
        var registry = CreateRegistry();
        var oldDep = CacheDependencyBuilder.OnType<Product>();
        var newDep = CacheDependencyBuilder.OnType<Product>();
        var oldDef = CreateKeyDefinition("OldKey", oldDep);
        var newDef = CreateKeyDefinition("NewKey", newDep);

        // Act - 第一次构建
        registry.BuildIndex(new[] { oldDef });
        var keysAfterFirst = registry.GetDependentKeys(typeof(Product));

        // Act - 第二次构建（应该替换）
        registry.BuildIndex(new[] { newDef });
        var keysAfterSecond = registry.GetDependentKeys(typeof(Product));

        // Assert
        keysAfterFirst.Should().Contain(oldDef);
        keysAfterSecond.Should().NotContain(oldDef);
        keysAfterSecond.Should().Contain(newDef);
    }

    [Fact]
    public void BuildIndex_SecondCallWithEmptyList_ShouldClearIndex()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductList", dependency);

        // Act
        registry.BuildIndex(new[] { definition });
        registry.BuildIndex(Array.Empty<CacheKeyDefinition>());

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().BeEmpty();
    }

    #endregion

    #region 查询测试

    [Fact]
    public void GetDependentKeys_UnknownEntityType_ShouldReturnEmptyList()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductList", dependency);
        registry.BuildIndex(new[] { definition });

        // Act
        var keys = registry.GetDependentKeys(typeof(Order)); // 未注册的类型

        // Assert
        keys.Should().BeEmpty();
        keys.Should().NotBeNull();
    }

    [Fact]
    public void GetDependentKeys_BeforeIndexBuilt_ShouldReturnEmptyList()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var keys = registry.GetDependentKeys(typeof(Product));

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void GetDependentKeys_MultipleCallsSameType_ShouldReturnSameResults()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductList", dependency);
        registry.BuildIndex(new[] { definition });

        // Act
        var keys1 = registry.GetDependentKeys(typeof(Product));
        var keys2 = registry.GetDependentKeys(typeof(Product));

        // Assert
        keys1.Should().BeEquivalentTo(keys2);
    }

    #endregion

    #region 复杂依赖场景测试

    [Fact]
    public void BuildIndex_ComplexDependencyGraph_ShouldIndexCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();

        // 产品列表依赖 Product（任何属性变化）
        var productList = CreateKeyDefinition("ProductList",
            CacheDependencyBuilder.OnType<Product>());

        // 分类详情依赖 Category（仅名称变化）
        var categoryDetails = CreateKeyDefinition("CategoryDetails",
            CacheDependencyBuilder.OnType<Category>()
                .OnPropertyChange(c => c.Name));

        // 分类的产品列表依赖 Category 和 Product（特定属性）
        var categoryProducts = CreateKeyDefinition("CategoryProducts",
            CacheDependencyBuilder.OnType<Category>()
                .OnPropertyChange(c => c.Name),
            CacheDependencyBuilder.OnType<Product>()
                .OnPropertiesChange(p => p.Price, p => p.Stock, p => p.IsActive));

        // 订单依赖 Order 和 Product（实例级）
        var orderWithProduct = CreateKeyDefinition("OrderWithProduct",
            CacheDependencyBuilder.OnInstance<Order>(o => o.Id)
                .OnPropertyChange(o => o.Status),
            CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                .OnPropertiesChange(p => p.Price, p => p.Name));

        var definitions = new[] { productList, categoryDetails, categoryProducts, orderWithProduct };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var productKeys = registry.GetDependentKeys(typeof(Product));
        productKeys.Should().HaveCount(3);
        productKeys.Should().Contain(productList);
        productKeys.Should().Contain(categoryProducts);
        productKeys.Should().Contain(orderWithProduct);

        var categoryKeys = registry.GetDependentKeys(typeof(Category));
        categoryKeys.Should().HaveCount(2);
        categoryKeys.Should().Contain(categoryDetails);
        categoryKeys.Should().Contain(categoryProducts);

        var orderKeys = registry.GetDependentKeys(typeof(Order));
        orderKeys.Should().HaveCount(1);
        orderKeys.Should().Contain(orderWithProduct);
    }

    [Fact]
    public void BuildIndex_MixedDependencyLevels_ShouldIndexCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();

        // 类型级依赖
        var typeLevel = CreateKeyDefinition("TypeLevel",
            CacheDependencyBuilder.OnType<Product>());

        // 实例级依赖
        var instanceLevel = CreateKeyDefinition("InstanceLevel",
            CacheDependencyBuilder.OnInstance<Product>(p => p.Id));

        // 混合依赖
        var mixed = CreateKeyDefinition("Mixed",
            CacheDependencyBuilder.OnType<Product>()
                .OnPropertyChange(p => p.Price),
            CacheDependencyBuilder.OnInstance<Product>(p => p.CategoryId)
                .OnPropertyChange(p => p.Stock));

        var definitions = new[] { typeLevel, instanceLevel, mixed };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(4); // typeLevel + instanceLevel + mixed(2个依赖)
    }

    #endregion

    #region 不同作用域的缓存键测试

    [Fact]
    public void BuildIndex_DifferentScopes_ShouldIndexAllRegardlessOfScope()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();

        var globalKey = new CacheKeyDefinition("GlobalProduct", CacheKeyScope.Global);
        globalKey.Dependencies.Add(dependency);

        var tenantKey = new CacheKeyDefinition("TenantProduct", CacheKeyScope.Tenant);
        tenantKey.Dependencies.Add(CacheDependencyBuilder.OnType<Product>());

        var userKey = new CacheKeyDefinition("UserProduct", CacheKeyScope.User);
        userKey.Dependencies.Add(CacheDependencyBuilder.OnType<Product>());

        var definitions = new[] { globalKey, tenantKey, userKey };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(3);
        keys.Should().Contain(globalKey);
        keys.Should().Contain(tenantKey);
        keys.Should().Contain(userKey);
    }

    #endregion

    #region 并发访问测试

    [Fact]
    public async Task BuildIndex_ConcurrentBuild_ShouldNotThrow()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await Task.Run(() =>
            {
                var dependency = CacheDependencyBuilder.OnType<Product>();
                var definition = CreateKeyDefinition($"Key{i}", dependency);
                registry.BuildIndex(new[] { definition });
            });
        });

        // Assert
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDependentKeys_ConcurrentRead_ShouldNotThrow()
    {
        // Arrange
        var registry = CreateRegistry();
        var dependency = CacheDependencyBuilder.OnType<Product>();
        var definition = CreateKeyDefinition("ProductList", dependency);
        registry.BuildIndex(new[] { definition });

        // Act
        var tasks = Enumerable.Range(0, 1000).Select(async i =>
        {
            return await Task.Run(() => registry.GetDependentKeys(typeof(Product)));
        });

        // Assert
        var act = async () =>
        {
            var results = await Task.WhenAll(tasks);
            results.Should().AllSatisfy(keys => keys.Should().HaveCount(1));
        };
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BuildIndexAndRead_Concurrent_ShouldBeThreadSafe()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var writeTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await Task.Run(() =>
            {
                var dependency = CacheDependencyBuilder.OnType<Product>();
                var definition = CreateKeyDefinition($"Key{i}", dependency);
                registry.BuildIndex(new[] { definition });
            });
        });

        var readTasks = Enumerable.Range(0, 100).Select(async i =>
        {
            return await Task.Run(() => registry.GetDependentKeys(typeof(Product)));
        });

        // Assert
        var act = async () => await Task.WhenAll(writeTasks.Concat(readTasks));
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region 性能测试

    [Fact]
    public void BuildIndex_LargeNumberOfDefinitions_ShouldPerformEfficiently()
    {
        // Arrange
        var registry = CreateRegistry();
        var definitions = Enumerable.Range(1, 10000)
            .Select(i =>
            {
                var dependency = CacheDependencyBuilder.OnType<Product>();
                return CreateKeyDefinition($"Key{i}", dependency);
            })
            .ToArray();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        registry.BuildIndex(definitions);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);

        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().HaveCount(10000);
    }

    [Fact]
    public void GetDependentKeys_LargeIndex_ShouldPerformEfficiently()
    {
        // Arrange
        var registry = CreateRegistry();
        var definitions = Enumerable.Range(1, 10000)
            .Select(i =>
            {
                var dependency = CacheDependencyBuilder.OnType<Product>();
                return CreateKeyDefinition($"Key{i}", dependency);
            })
            .ToArray();
        registry.BuildIndex(definitions);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var keys = registry.GetDependentKeys(typeof(Product));
        }
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    #endregion

    #region 边界情况测试

    [Fact]
    public void BuildIndex_NullDefinitions_ShouldThrow()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        var act = () => registry.BuildIndex(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildIndex_DefinitionWithNoDependencies_ShouldNotBeIndexed()
    {
        // Arrange
        var registry = CreateRegistry();
        var definition = new CacheKeyDefinition("NoDeps", CacheKeyScope.Global);

        // Act
        registry.BuildIndex(new[] { definition });

        // Assert
        var keys = registry.GetDependentKeys(typeof(Product));
        keys.Should().BeEmpty();
    }

    [Fact]
    public void GetDependentKeys_NullEntityType_ShouldThrow()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        var act = () => registry.GetDependentKeys(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 真实场景模拟测试

    [Fact]
    public void BuildIndex_RealWorldScenario_ShouldWorkCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();

        // 产品列表缓存(任何产品变化都失效)
        var productList = new CacheKeyDefinition(
            "ProductList",
            CacheKeyScope.Global,
            defaultExpiration: TimeSpan.FromMinutes(5)
        );
        productList.Dependencies.Add(CacheDependencyBuilder.OnType<Product>());

        // 产品详情缓存(特定产品实例变化失效)
        var productDetails = new CacheKeyDefinition(
            "ProductDetails:{id}",
            CacheKeyScope.Global,
            defaultExpiration: TimeSpan.FromMinutes(30) // 原 SetExpiration 移到构造函数
        );
        productDetails.Dependencies.Add(
            CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                .OnPropertiesChange(p => p.Name, p => p.Price, p => p.Stock)
        );

        // 分类产品列表(分类名称或产品价格/库存变化时失效)
        var categoryProducts = new CacheKeyDefinition(
            "CategoryProducts:{categoryId}",
            CacheKeyScope.Global,
            defaultExpiration: TimeSpan.FromMinutes(10) // 原 SetExpiration 移到构造函数
        );
        categoryProducts.Dependencies.Add(
            CacheDependencyBuilder.OnType<Product>()
                .OnPropertiesChange(p => p.Price, p => p.Stock, p => p.IsActive)
        );
        categoryProducts.Dependencies.Add(
            CacheDependencyBuilder.OnType<Category>()
                .OnPropertyChange(c => c.Name)
        );

        // 用户收藏(产品名称或价格变化时失效)
        var userFavorites = new CacheKeyDefinition(
            "UserFavorites",
            CacheKeyScope.User,
            defaultExpiration: TimeSpan.FromHours(1) // 原 SetExpiration 移到构造函数
        );
        userFavorites.Dependencies.Add(
            CacheDependencyBuilder.OnType<Product>()
                .OnPropertiesChange(p => p.Name, p => p.Price, p => p.IsActive)
        );

        // 订单摘要(订单状态或产品价格变化时失效)
        var orderSummary = new CacheKeyDefinition(
            "OrderSummary:{orderId}",
            CacheKeyScope.Tenant,
            defaultExpiration: TimeSpan.FromMinutes(15) // 原 SetExpiration 移到构造函数
        );
        orderSummary.Dependencies.Add(
            CacheDependencyBuilder.OnInstance<Order>(o => o.Id)
                .OnPropertyChange(o => o.Status)
        );
        orderSummary.Dependencies.Add(
            CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                .OnPropertyChange(p => p.Price)
        );

        var definitions = new[]
        {
            productList,
            productDetails,
            categoryProducts,
            userFavorites,
            orderSummary
        };

        // Act
        registry.BuildIndex(definitions);

        // Assert - Product 相关的缓存键
        var productKeys = registry.GetDependentKeys(typeof(Product));
        productKeys.Should().HaveCount(5);
        productKeys.Should().Contain(productList);
        productKeys.Should().Contain(productDetails);
        productKeys.Should().Contain(categoryProducts);
        productKeys.Should().Contain(userFavorites);
        productKeys.Should().Contain(orderSummary);

        // Assert - Category 相关的缓存键
        var categoryKeys = registry.GetDependentKeys(typeof(Category));
        categoryKeys.Should().HaveCount(1);
        categoryKeys.Should().Contain(categoryProducts);

        // Assert - Order 相关的缓存键
        var orderKeys = registry.GetDependentKeys(typeof(Order));
        orderKeys.Should().HaveCount(1);
        orderKeys.Should().Contain(orderSummary);

        // Assert - 验证属性触发器配置
        var productDetailsDep = productDetails.Dependencies.First();
        productDetailsDep.TriggerProperties.Should().Contain("Name");
        productDetailsDep.TriggerProperties.Should().Contain("Price");
        productDetailsDep.TriggerProperties.Should().Contain("Stock");
    }

    #endregion

    #region 日志测试

    [Fact]
    public void BuildIndex_ShouldLogIndexBuildInformation()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DependencyRegistry>>();
        var registry = new DependencyRegistry(loggerMock.Object);

        var productDep = CacheDependencyBuilder.OnType<Product>();
        var categoryDep = CacheDependencyBuilder.OnType<Category>();
        var definition = CreateKeyDefinition("ProductList", productDep, categoryDep);
        var definitions = new[] { definition };

        // Act
        registry.BuildIndex(definitions);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Built dependency index")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region 表达式树验证测试

    [Fact]
    public void CacheDependency_PropertyExpression_ShouldExtractCorrectPropertyName()
    {
        // Arrange & Act
        var dependency = CacheDependencyBuilder
            .OnType<Product>()
            .OnPropertyChange(p => p.Name);

        // Assert
        dependency.TriggerProperties.Should().Contain("Name");
    }

    [Fact]
    public void CacheDependency_MultiplePropertyExpressions_ShouldExtractAllNames()
    {
        // Arrange & Act
        var dependency = CacheDependencyBuilder
            .OnType<Product>()
            .OnPropertiesChange(
                p => p.Name,
                p => p.Price,
                p => p.Stock);

        // Assert
        dependency.TriggerProperties.Should().HaveCount(3);
        dependency.TriggerProperties.Should().Contain("Name");
        dependency.TriggerProperties.Should().Contain("Price");
        dependency.TriggerProperties.Should().Contain("Stock");
    }

    [Fact]
    public void CacheDependency_ChainedPropertyExpressions_ShouldNotDuplicate()
    {
        // Arrange & Act
        var dependency = CacheDependencyBuilder
            .OnType<Product>()
            .OnPropertyChange(p => p.Name)
            .OnPropertyChange(p => p.Name); // 重复添加

        // Assert - 当前实现会重复，如果需要去重可以在实现中处理
        dependency.TriggerProperties.Should().HaveCount(1);
    }

    #endregion
}