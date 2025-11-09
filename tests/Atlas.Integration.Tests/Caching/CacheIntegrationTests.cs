using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Data.Abstractions.Caching;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Atlas.Integration.Tests.Caching;

/// <summary>
/// 缓存系统端到端集成测试 - 使用 Redis
/// </summary>
[Collection("Redis")]
public class CacheIntegrationTests : IAsyncLifetime
{
    #region Test Entities

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
        public Category? Category { get; set; }
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
    }

    public class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
        public Product? Product { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Price).HasPrecision(18, 2);
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.HasOne(o => o.Product)
                    .WithMany()
                    .HasForeignKey(o => o.ProductId);
            });
        }
    }

    #endregion

    #region Cache Key Definitions

    private static class CacheKeys
    {
        // 产品列表 - 依赖 Product 类型变化
        public static readonly CacheKeyDefinition ProductList = new CacheKeyDefinition(
            "ProductList",
            CacheKeyScope.Tenant,
            defaultExpiration: TimeSpan.FromMinutes(5))
        {
            Dependencies =
            {
                CacheDependencyBuilder.OnType<Product>()
            }
        };

        // 产品详情 - 依赖特定产品实例的特定属性
        public static readonly CacheKeyDefinition ProductDetails = new CacheKeyDefinition(
            "ProductDetails",
            CacheKeyScope.Tenant,
            instanceKeyName: "ProductId",
            defaultExpiration: TimeSpan.FromMinutes(30))
        {
            Dependencies =
            {
                CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                    .OnPropertiesChange(p => p.Name, p => p.Price, p => p.Stock)
            }
        };

        // 分类产品列表 - 依赖分类和产品
        public static readonly CacheKeyDefinition CategoryProducts = new CacheKeyDefinition(
            "CategoryProducts",
            CacheKeyScope.Tenant,
            instanceKeyName: "CategoryId",
            defaultExpiration: TimeSpan.FromMinutes(10))
        {
            Dependencies =
            {
                CacheDependencyBuilder.OnType<Product>()
                    .OnPropertiesChange(p => p.Price, p => p.Stock, p => p.IsActive),
                CacheDependencyBuilder.OnInstance<Category>(c => c.Id)
                    .OnPropertyChange(c => c.Name)
            }
        };

        // 产品搜索 - 只依赖产品名称和激活状态
        public static readonly CacheKeyDefinition ProductSearch = new CacheKeyDefinition(
            "ProductSearch",
            CacheKeyScope.Tenant,
            defaultExpiration: TimeSpan.FromMinutes(15))
        {
            Dependencies =
            {
                CacheDependencyBuilder.OnType<Product>()
                    .OnPropertiesChange(p => p.Name, p => p.IsActive)
            }
        };

        // 订单摘要 - 依赖订单和产品
        public static readonly CacheKeyDefinition OrderSummary = new CacheKeyDefinition(
            "OrderSummary",
            CacheKeyScope.Tenant,
            instanceKeyName: "OrderId",
            defaultExpiration: TimeSpan.FromMinutes(5))
        {
            Dependencies =
            {
                CacheDependencyBuilder.OnInstance<Order>(o => o.Id)
                    .OnPropertyChange(o => o.Status),
                CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                    .OnPropertyChange(p => p.Price)
            }
        };
    }

    #endregion

    #region Test Infrastructure

    private ServiceProvider _serviceProvider = null!;
    private TestDbContext _dbContext = null!;
    private ICacheService _cacheService = null!;
    private DependencyRegistry _dependencyRegistry = null!;
    private string _testPrefix = null!;

    public async Task InitializeAsync()
    {
        _testPrefix = $"IntegrationTest_{Guid.NewGuid():N}_";

        var services = new ServiceCollection();

        // Logging
        services.AddLogging();

        // CurrentUser
        services.AddSingleton<ICurrentUserService>(TestCurrentUserService.CreateTenant1User());

        // Cache System with Redis
        services.AddAtlasCache(options =>
        {
            options.DefaultExpirationSeconds = 300;
            options.L1CacheSizeLimitMB = 50;
            options.RedisConnectionString = "localhost:6379,allowAdmin=true";
            options.KeyPrefix = _testPrefix; // 使用唯一前缀隔离测试
        });
        services.AddSingleton<CacheSaveChangesInterceptor>();
        // DbContext with Interceptor
        services.AddDbContext<TestDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .AddInterceptors(sp.GetRequiredService<CacheSaveChangesInterceptor>());
        });

        _serviceProvider = services.BuildServiceProvider();

        // 初始化依赖注册表
        _dependencyRegistry = _serviceProvider.GetRequiredService<DependencyRegistry>();
        _dependencyRegistry.BuildIndex(new[]
        {
            CacheKeys.ProductList,
            CacheKeys.ProductDetails,
            CacheKeys.CategoryProducts,
            CacheKeys.ProductSearch,
            CacheKeys.OrderSummary
        });

        _dbContext = _serviceProvider.GetRequiredService<TestDbContext>();
        _cacheService = _serviceProvider.GetRequiredService<ICacheService>();

        // 清空 Redis 测试数据
        await _cacheService.ClearAsync();

        // 初始化测试数据
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        // 清理 Redis 测试数据
        await _cacheService.ClearAsync();

        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var category = new Category { Id = 1, Name = "Electronics" };
        _dbContext.Categories.Add(category);

        var products = new[]
        {
            new Product { Id = 1, Name = "Laptop", Price = 999.99m, Stock = 10, CategoryId = 1 },
            new Product { Id = 2, Name = "Mouse", Price = 29.99m, Stock = 100, CategoryId = 1 },
            new Product { Id = 3, Name = "Keyboard", Price = 79.99m, Stock = 50, CategoryId = 1 }
        };
        _dbContext.Products.AddRange(products);

        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region 场景1: 基础缓存流程（Redis）

    [Fact]
    public async Task Scenario1_BasicCacheFlow_WithRedis_ShouldWorkCorrectly()
    {
        // 1. 首次查询 - 应该触发数据库查询并缓存到 Redis
        var factoryCalled = false;
        var products1 = await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductList,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Products.ToListAsync();
            });

        factoryCalled.Should().BeTrue("首次查询应该调用工厂方法");
        products1.Should().HaveCount(3);

        // 2. 再次查询 - 应该从 L1 或 Redis 返回
        factoryCalled = false;
        var products2 = await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductList,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Products.ToListAsync();
            });

        factoryCalled.Should().BeFalse("第二次查询应该使用缓存");
        products2.Should().HaveCount(3);

        // 3. 修改产品 - 应该触发缓存失效（包括 Redis）
        var product = await _dbContext.Products.FindAsync(1);
        product!.Name = "Updated Laptop";
        await _dbContext.SaveChangesAsync();

        // 等待 Redis 失效传播
        await Task.Delay(100);

        // 4. 失效后查询 - 应该重新调用工厂方法
        factoryCalled = false;
        var products3 = await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductList,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Products.ToListAsync();
            });

        factoryCalled.Should().BeTrue("缓存失效后应该重新调用工厂方法");
        products3.Should().Contain(p => p.Name == "Updated Laptop");
    }

    #endregion

    #region 场景2: 属性级精确失效

    [Fact]
    public async Task Scenario2_PropertyLevelInvalidation_ShouldOnlyInvalidateRelatedCaches()
    {
        // 准备缓存
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () => await _dbContext.Products.FindAsync(1),
            instanceValue: 1);

        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductSearch,
            async () => await _dbContext.Products.Where(p => p.IsActive).ToListAsync());

        await _cacheService.GetOrCreateAsync(
            CacheKeys.CategoryProducts,
            async () => await _dbContext.Products.Where(p => p.CategoryId == 1).ToListAsync(),
            instanceValue: 1);

        // 修改产品价格（影响 ProductDetails 和 CategoryProducts，但不影响 ProductSearch）
        var product = await _dbContext.Products.FindAsync(1);
        product!.Price = 1299.99m; // 只修改价格
        await _dbContext.SaveChangesAsync();

        await Task.Delay(100); // Redis 传播延迟

        // ProductDetails 应该失效
        var detailsFactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () =>
            {
                detailsFactoryCalled = true;
                return await _dbContext.Products.FindAsync(1);
            },
            instanceValue: 1);
        detailsFactoryCalled.Should().BeTrue("价格变化应该使 ProductDetails 失效");

        // CategoryProducts 应该失效（依赖 Price）
        var categoryFactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.CategoryProducts,
            async () =>
            {
                categoryFactoryCalled = true;
                return await _dbContext.Products.Where(p => p.CategoryId == 1).ToListAsync();
            },
            instanceValue: 1);
        categoryFactoryCalled.Should().BeTrue("价格变化应该使 CategoryProducts 失效");

        // ProductSearch 不应该失效（只依赖 Name 和 IsActive）
        var searchFactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductSearch,
            async () =>
            {
                searchFactoryCalled = true;
                return await _dbContext.Products.Where(p => p.IsActive).ToListAsync();
            });
        searchFactoryCalled.Should().BeFalse("价格变化不应该使 ProductSearch 失效");
    }

    #endregion

    #region 场景3: 实例级缓存隔离

    [Fact]
    public async Task Scenario3_InstanceLevelCache_ShouldIsolateCorrectly()
    {
        // 缓存多个产品详情
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () => await _dbContext.Products.FindAsync(1),
            instanceValue: 1);

        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () => await _dbContext.Products.FindAsync(2),
            instanceValue: 2);

        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () => await _dbContext.Products.FindAsync(3),
            instanceValue: 3);

        // 只修改产品1
        var product1 = await _dbContext.Products.FindAsync(1);
        product1!.Name = "Updated Laptop";
        await _dbContext.SaveChangesAsync();

        await Task.Delay(100);

        // 产品1应该失效
        var p1FactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () =>
            {
                p1FactoryCalled = true;
                return await _dbContext.Products.FindAsync(1);
            },
            instanceValue: 1);
        p1FactoryCalled.Should().BeTrue("产品1的缓存应该失效");

        // 产品2和3不应该失效
        var p2FactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () =>
            {
                p2FactoryCalled = true;
                return await _dbContext.Products.FindAsync(2);
            },
            instanceValue: 2);
        p2FactoryCalled.Should().BeFalse("产品2的缓存不应该失效");

        var p3FactoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductDetails,
            async () =>
            {
                p3FactoryCalled = true;
                return await _dbContext.Products.FindAsync(3);
            },
            instanceValue: 3);
        p3FactoryCalled.Should().BeFalse("产品3的缓存不应该失效");
    }

    #endregion

    #region 场景4: 多实体关联失效

    [Fact]
    public async Task Scenario4_MultiEntityInvalidation_ShouldInvalidateAllRelated()
    {
        // 缓存分类产品列表
        await _cacheService.GetOrCreateAsync(
            CacheKeys.CategoryProducts,
            async () => await _dbContext.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == 1)
                .ToListAsync(),
            instanceValue: 1);

        // 修改分类名称 - 应该触发 CategoryProducts 失效
        var category = await _dbContext.Categories.FindAsync(1);
        category!.Name = "Consumer Electronics";
        await _dbContext.SaveChangesAsync();

        await Task.Delay(100);

        var factoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.CategoryProducts,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Products
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == 1)
                    .ToListAsync();
            },
            instanceValue: 1);

        factoryCalled.Should().BeTrue("分类名称变化应该使 CategoryProducts 失效");
    }

    #endregion

    #region 场景5: 复杂业务场景

    [Fact]
    public async Task Scenario5_ComplexBusinessScenario_OrderWithProduct()
    {
        // 创建订单
        var order = new Order
        {
            Id = 1,
            ProductId = 1,
            Amount = 999.99m,
            Status = "Pending"
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // 缓存订单摘要
        await _cacheService.GetOrCreateAsync(
            CacheKeys.OrderSummary,
            async () =>
            {
                var o = await _dbContext.Orders
                    .Include(x => x.Product)
                    .FirstOrDefaultAsync(x => x.Id == 1);
                return o;
            },
            instanceValue: 1);

        // 场景1: 修改订单状态 - 应该失效订单摘要
        order.Status = "Paid";
        await _dbContext.SaveChangesAsync();
        await Task.Delay(100);

        var factoryCalled = false;
        var result1 = await _cacheService.GetOrCreateAsync(
            CacheKeys.OrderSummary,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Orders
                    .Include(x => x.Product)
                    .FirstOrDefaultAsync(x => x.Id == 1);
            },
            instanceValue: 1);

        factoryCalled.Should().BeTrue("订单状态变化应该使订单摘要失效");
        result1!.Status.Should().Be("Paid");

        // 场景2: 修改产品价格 - 应该失效订单摘要
        var product = await _dbContext.Products.FindAsync(1);
        product!.Price = 1099.99m;
        await _dbContext.SaveChangesAsync();
        await Task.Delay(100);

        factoryCalled = false;
        var result2 = await _cacheService.GetOrCreateAsync(
            CacheKeys.OrderSummary,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Orders
                    .Include(x => x.Product)
                    .FirstOrDefaultAsync(x => x.Id == 1);
            },
            instanceValue: 1);

        factoryCalled.Should().BeTrue("产品价格变化应该使订单摘要失效");

        // 场景3: 修改产品库存 - 不应该失效订单摘要（OrderSummary 只依赖产品价格）
        product!.Stock = 5;
        await _dbContext.SaveChangesAsync();
        await Task.Delay(100);

        factoryCalled = false;
        await _cacheService.GetOrCreateAsync(
            CacheKeys.OrderSummary,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Orders
                    .Include(x => x.Product)
                    .FirstOrDefaultAsync(x => x.Id == 1);
            },
            instanceValue: 1);

        factoryCalled.Should().BeFalse("产品库存变化不应该使订单摘要失效");
    }

    #endregion

    #region 场景6: 批量操作

    [Fact]
    public async Task Scenario6_BulkOperations_ShouldInvalidateCorrectly()
    {
        // 缓存产品列表
        await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductList,
            async () => await _dbContext.Products.ToListAsync());

        // 批量添加产品
        var newProducts = new[]
        {
            new Product { Id = 4, Name = "Monitor", Price = 299.99m, Stock = 20, CategoryId = 1 },
            new Product { Id = 5, Name = "Webcam", Price = 89.99m, Stock = 30, CategoryId = 1 },
            new Product { Id = 6, Name = "Headset", Price = 129.99m, Stock = 25, CategoryId = 1 }
        };
        _dbContext.Products.AddRange(newProducts);
        await _dbContext.SaveChangesAsync();

        await Task.Delay(100);

        // 产品列表应该失效
        var factoryCalled = false;
        var products = await _cacheService.GetOrCreateAsync(
            CacheKeys.ProductList,
            async () =>
            {
                factoryCalled = true;
                return await _dbContext.Products.ToListAsync();
            });

        factoryCalled.Should().BeTrue("批量添加应该使产品列表失效");
        products.Should().HaveCount(6);
    }

    #endregion

    #region 场景7: 不同租户隔离（Redis）

    [Fact]
    public async Task Scenario7_MultiTenantIsolation_WithRedis_ShouldWorkCorrectly()
    {
        // 租户1的服务提供者
        var tenant1Services = new ServiceCollection();
        tenant1Services.AddLogging();
        tenant1Services.AddSingleton<ICurrentUserService>(TestCurrentUserService.CreateTenant1User());
        tenant1Services.AddAtlasCache(options =>
        {
            options.RedisConnectionString = "localhost:6379,allowAdmin=true";
            options.KeyPrefix = _testPrefix;
        });
        var tenant1Sp = tenant1Services.BuildServiceProvider();
        var tenant1Cache = tenant1Sp.GetRequiredService<ICacheService>();

        // 租户2的服务提供者
        var tenant2Services = new ServiceCollection();
        tenant2Services.AddLogging();
        tenant2Services.AddSingleton<ICurrentUserService>(TestCurrentUserService.CreateTenant2User());
        tenant2Services.AddAtlasCache(options =>
        {
            options.RedisConnectionString = "localhost:6379,allowAdmin=true";
            options.KeyPrefix = _testPrefix;
        });
        var tenant2Sp = tenant2Services.BuildServiceProvider();
        var tenant2Cache = tenant2Sp.GetRequiredService<ICacheService>();

        try
        {
            // 租户1缓存数据
            await tenant1Cache.SetAsync(CacheKeys.ProductList, new List<Product>
            {
                new() { Id = 1, Name = "Tenant1 Product" }
            });

            // 租户2缓存数据
            await tenant2Cache.SetAsync(CacheKeys.ProductList, new List<Product>
            {
                new() { Id = 2, Name = "Tenant2 Product" }
            });

            // 验证隔离
            var tenant1Data = await tenant1Cache.GetOrCreateAsync(
                CacheKeys.ProductList,
                () => Task.FromResult<List<Product>>(null!));

            var tenant2Data = await tenant2Cache.GetOrCreateAsync(
                CacheKeys.ProductList,
                () => Task.FromResult<List<Product>>(null!));

            tenant1Data.Should().HaveCount(1);
            tenant1Data![0].Name.Should().Be("Tenant1 Product");

            tenant2Data.Should().HaveCount(1);
            tenant2Data![0].Name.Should().Be("Tenant2 Product");
        }
        finally
        {
            await tenant1Sp.DisposeAsync();
            await tenant2Sp.DisposeAsync();
        }
    }

    #endregion

    #region 场景8: Redis 持久化验证

    [Fact]
    public async Task Scenario8_RedisPersistence_ShouldSurviveServiceRestart()
    {
        var testKey = new CacheKeyDefinition("PersistenceTest", CacheKeyScope.Global);
        var testData = new List<Product>
        {
            new() { Id = 999, Name = "Persistence Test Product" }
        };

        // 第一个服务实例 - 写入缓存
        await _cacheService.SetAsync(testKey, testData);

        // 创建新的服务实例（模拟应用重启）
        var newServices = new ServiceCollection();
        newServices.AddLogging();
        newServices.AddSingleton<ICurrentUserService>(TestCurrentUserService.CreateTenant1User());
        newServices.AddAtlasCache(options =>
        {
            options.RedisConnectionString = "localhost:6379,allowAdmin=true";
            options.KeyPrefix = _testPrefix;
        });

        await using var newSp = newServices.BuildServiceProvider();
        var newCacheService = newSp.GetRequiredService<ICacheService>();

        // 从新实例读取缓存
        var retrieved = await newCacheService.GetOrCreateAsync(
            testKey,
            () => Task.FromResult<List<Product>>(null!));

        // 验证数据持久化到 Redis
        retrieved.Should().NotBeNull("数据应该持久化到 Redis");
        retrieved.Should().HaveCount(1);
        retrieved![0].Name.Should().Be("Persistence Test Product");
    }

    #endregion

    #region 场景9: 并发场景

    [Fact]
    public async Task Scenario9_ConcurrentAccess_ShouldMaintainConsistency()
    {
        var definition = new CacheKeyDefinition("ConcurrentTest", CacheKeyScope.Global);
        var callCount = 0;

        // 100个并发请求
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            return await _cacheService.GetOrCreateAsync(
                definition,
                async () =>
                {
                    Interlocked.Increment(ref callCount);
                    await Task.Delay(10); // 模拟数据库查询
                    return new List<Product> { new() { Id = 1, Name = "Concurrent Test" } };
                });
        });

        var results = await Task.WhenAll(tasks);

        // 工厂方法应该只被调用一次（缓存穿透保护）
        callCount.Should().Be(1, "并发请求应该只触发一次数据加载");
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region 场景10: 性能基准测试

    [Fact]
    public async Task Scenario10_PerformanceBenchmark_CacheVsDatabase()
    {
        var definition = new CacheKeyDefinition("PerfTest", CacheKeyScope.Global);

        // 首次加载（数据库）
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        await _cacheService.GetOrCreateAsync(
            definition,
            async () => await _dbContext.Products.ToListAsync());
        sw1.Stop();
        var dbTime = sw1.ElapsedMilliseconds;

        // 后续加载（缓存）
        var cacheTimes = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            await _cacheService.GetOrCreateAsync(
                definition,
                async () => await _dbContext.Products.ToListAsync());
            sw2.Stop();
            cacheTimes.Add(sw2.ElapsedMilliseconds);
        }

        var avgCacheTime = cacheTimes.Average();

        // 缓存应该明显快于数据库（至少快50%）
        avgCacheTime.Should().BeLessThan(dbTime * 0.5,
            $"缓存读取({avgCacheTime}ms)应该明显快于数据库({dbTime}ms)");
    }

    #endregion
}

/// <summary>
/// Redis 测试集合 - 确保测试按顺序执行
/// </summary>
[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisFixture>
{
}

/// <summary>
/// Redis 测试夹具 - 验证 Redis 连接
/// </summary>
public class RedisFixture : IAsyncLifetime
{
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        // 验证 Redis 连接
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICurrentUserService>(TestCurrentUserService.CreateTenant1User());
            services.AddAtlasCache(options =>
            {
                options.RedisConnectionString = "localhost:6379,allowAdmin=true";
            });

            await using var sp = services.BuildServiceProvider();
            var cache = sp.GetRequiredService<ICacheService>();

            // 测试连接
            var testKey = new CacheKeyDefinition("RedisConnectionTest", CacheKeyScope.Global);
            await cache.SetAsync(testKey, new List<int> { 1, 2, 3 });
            await cache.RemoveAsync(testKey);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to Redis at localhost:6379. Please ensure Redis is running.", ex);
        }

    }
}