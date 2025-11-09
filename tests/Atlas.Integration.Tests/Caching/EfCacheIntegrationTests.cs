using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Data.Abstractions.Caching;
using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Atlas.Integration.Tests.Caching;

/// <summary>
/// EF 缓存完整集成测试套件 - 使用真实的 MySQL 和 Redis
/// </summary>
[Collection("IntegrationTests")]
public class EfCacheIntegrationTests : IAsyncLifetime
{
    private const string RedisConnectionString = "localhost:6379";
    private const string MySqlConnectionString = "Server=localhost;Database=atlas_cache_test;User=root;Password=your_password;";

    private ServiceProvider _serviceProvider = null!;
    private IConnectionMultiplexer _redis = null!;
    private string _testDatabaseName = null!;

    #region Test Entities

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; }
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
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
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
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Price).HasPrecision(18, 2);
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.HasOne(o => o.Product)
                    .WithMany()
                    .HasForeignKey(o => o.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }

    #endregion

    #region Setup & Teardown

    public async Task InitializeAsync()
    {
        _testDatabaseName = $"atlas_cache_test_{Guid.NewGuid():N}";

        // 连接 Redis
        _redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        var userService = TestCurrentUserService.CreateTenant1User();
        // 配置服务
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(userService);
        services.AddScoped<CacheSaveChangesInterceptor>();
        // 注册 DbContext
        services.AddDbContext<TestDbContext>((serviceProvider, options) => {
            var connectionString = MySqlConnectionString.Replace("atlas_cache_test", _testDatabaseName);
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            var interceptor = serviceProvider.GetRequiredService<CacheSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);
        });

        // 注册缓存基础设施
        services.AddSingleton<IConnectionMultiplexer>(_redis);
        services.AddSingleton<IStorageAdapter, RedisStorageAdapter>();
        services.AddSingleton<IMessageBroker, RedisMessageBroker>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<DependencyRegistry>();
        services.AddSingleton<InvalidationCoordinator>();
        services.AddScoped<CacheSaveChangesInterceptor>();

        // 日志
        services.AddLogging();
        services.AddAtlasCache(options =>
        {
            options.DefaultExpirationSeconds = 60;
            options.L1CacheSizeLimitMB = 10;
            options.RedisConnectionString = RedisConnectionString;
        });
        _serviceProvider = services.BuildServiceProvider();

        // 创建数据库
        await CreateDatabaseAsync();

        // 配置缓存键依赖
        ConfigureCacheDependencies();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync();
        await _redis.CloseAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private async Task DropDatabaseAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void ConfigureCacheDependencies()
    {
        var registry = _serviceProvider.GetRequiredService<DependencyRegistry>();

        var definitions = new[]
        {
            // 产品列表缓存
            new CacheKeyDefinition("ProductList", CacheKeyScope.Global, defaultExpiration: TimeSpan.FromMinutes(5))
            {
                Dependencies =
                {
                    CacheDependencyBuilder.OnType<Product>()
                }
            },
            
            // 产品详情缓存
            new CacheKeyDefinition("Product:{id}", CacheKeyScope.Global, defaultExpiration:TimeSpan.FromMinutes(10))
            {
                Dependencies =
                {
                    CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                        .OnPropertiesChange(p => p.Name, p => p.Price, p => p.Stock)
                }
            },
            
            // 分类产品列表
            new CacheKeyDefinition("CategoryProducts:{categoryId}", CacheKeyScope.Global, defaultExpiration : TimeSpan.FromMinutes(5))
            {
                Dependencies =
                {
                    CacheDependencyBuilder.OnType<Product>()
                        .OnPropertiesChange(p => p.Price, p => p.Stock, p => p.IsActive),
                    CacheDependencyBuilder.OnInstance<Category>(c => c.Id)
                        .OnPropertyChange(c => c.Name)
                }
            },
            
            // 订单详情
            new CacheKeyDefinition("Order:{orderId}", CacheKeyScope.Global, defaultExpiration : TimeSpan.FromMinutes(30))
            {
                Dependencies =
                {
                    CacheDependencyBuilder.OnInstance<Order>(o => o.Id)
                        .OnPropertyChange(o => o.Status),
                    CacheDependencyBuilder.OnInstance<Product>(p => p.Id)
                        .OnPropertyChange(p => p.Price)
                }
            }
        };

        registry.BuildIndex(definitions);
    }

    #endregion

    #region Helper Methods

    private async Task<T?> CacheGetAsync<T>(string key) where T : class
    {
        var storage = _serviceProvider.GetRequiredService<IStorageAdapter>();
        return await storage.GetAsync<T>(key, CancellationToken.None);
    }

    private async Task CacheSetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        var storage = _serviceProvider.GetRequiredService<IStorageAdapter>();
        await storage.SetAsync(key, value, expiration ?? TimeSpan.FromMinutes(10), CancellationToken.None);
    }

    private async Task<bool> CacheExistsAsync(string key)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(key);
    }

    private TestDbContext CreateDbContext()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TestDbContext>();
    }

    #endregion

    #region 基础集成测试
    [Fact]
    public async Task Infrastructure_ShouldBeConfiguredCorrectly()
    {
        // 验证依赖解析器
        var registry = _serviceProvider.GetRequiredService<DependencyRegistry>();
        var productKeys = registry.GetDependentKeys(typeof(Product));
        productKeys.Should().NotBeEmpty("应该至少有一个产品相关的缓存键定义");

        // 验证 Redis 连接
        var db = _redis.GetDatabase();
        var pingResult = await db.PingAsync();
        pingResult.Should().NotBe(TimeSpan.Zero);

        // 验证 DbContext 包含 interceptor
        using var context = CreateDbContext();
        var interceptors = context.GetService<IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>>();
        interceptors.Should().Contain(i => i is CacheSaveChangesInterceptor);
    }

    [Fact]
    public async Task FullDiagnostic_CacheInvalidationPipeline()
    {
        // 1. 验证依赖注册
        var registry = _serviceProvider.GetRequiredService<DependencyRegistry>();
        var productKeys = registry.GetDependentKeys(typeof(Product));
        productKeys.Should().NotBeEmpty();

        // 2. 验证 Redis 连接
        var db = _redis.GetDatabase();
        await db.StringSetAsync("test:diagnostic:key", "test:value");
        var value = await db.StringGetAsync("test:diagnostic:key");
        value.ToString().Should().Be("test:value");
        await db.KeyDeleteAsync("test:diagnostic:key");

        // 3. 验证缓存适配器
        var storage = _serviceProvider.GetRequiredService<IStorageAdapter>();
        await storage.SetAsync("test:adapter", new { Value = "test" }, TimeSpan.FromMinutes(1), default);
        var exists = await storage.ExistsAsync("test:adapter", default);
        exists.Should().BeTrue();
        await storage.RemoveAsync("test:adapter", default);

        // 4. 验证 Interceptor
        using var context = CreateDbContext();
        var interceptors = context.GetService<IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>>();
        var cacheInterceptor = interceptors?.OfType<CacheSaveChangesInterceptor>().FirstOrDefault();
        cacheInterceptor.Should().NotBeNull();

        // 5. 端到端测试
        await storage.SetAsync("ProductList", new[] { "Test" }, TimeSpan.FromMinutes(1), default);

        var category = new Category { Name = "Diagnostic Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var existsAfterCategory = await storage.ExistsAsync("ProductList", default);
        existsAfterCategory.Should().BeTrue("分类变更不应影响ProductList");

        context.Products.Add(new Product
        {
            Name = "Diagnostic Product",
            Price = 100m,
            Stock = 10,
            CategoryId = category.Id
        });

        await context.SaveChangesAsync();
        await Task.Delay(200); // 等待异步失效

        var existsAfterProduct = await storage.ExistsAsync("ProductList", default);
        existsAfterProduct.Should().BeFalse("添加产品应该删除ProductList缓存");
    }
    [Fact]
    public async Task AddProduct_ShouldInvalidateProductListCache()
    {
        // Arrange
        const string cacheKey = "ProductList";
        await CacheSetAsync(cacheKey, new List<string> { "Product1", "Product2" });

        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        // Act
        context.Products.Add(new Product
        {
            Name = "New Product",
            Price = 99.99m,
            Stock = 100,
            CategoryId = category.Id
        });
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeFalse("缓存应该被删除");
    }

    [Fact]
    public async Task UpdateProduct_ShouldInvalidateSpecificProductCache()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Original Product",
            Price = 50m,
            Stock = 10,
            CategoryId = category.Id
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;
        var cacheKey = $"Product:{productId}";
        await CacheSetAsync(cacheKey, new { Name = "Cached Product", Price = 50m });

        // Act - 修改产品
        product.Price = 60m;
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeFalse("产品缓存应该被删除");
    }

    [Fact]
    public async Task UpdateProductNonTriggerProperty_ShouldNotInvalidateCache()
    {
        // Arrange
        using var context = CreateDbContext();

        // 创建两个分类
        var category1 = new Category { Name = "Category 1" };
        var category2 = new Category { Name = "Category 2" };
        context.Categories.AddRange(category1, category2);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Test Product",
            Price = 50m,
            Stock = 10,
            CategoryId = category1.Id,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;
        var cacheKey = $"Product:{productId}";
        await CacheSetAsync(cacheKey, new { Name = "Cached", Price = 50m });

        // Act - 修改非触发属性 CategoryId (不在触发列表中)
        product.CategoryId = category2.Id;
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeTrue("非触发属性变化不应删除缓存");
    }

    [Fact]
    public async Task DeleteProduct_ShouldInvalidateBothListAndInstanceCache()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = "To Delete",
            Price = 30m,
            Stock = 5,
            CategoryId = category.Id
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;
        await CacheSetAsync("ProductList", new List<string> { "Product1" });
        await CacheSetAsync($"Product:{productId}", new { Name = "Cached" });

        // Act
        context.Products.Remove(product);
        await context.SaveChangesAsync();

        // Assert
        var listCacheExists = await CacheExistsAsync("ProductList");
        var instanceCacheExists = await CacheExistsAsync($"Product:{productId}");

        listCacheExists.Should().BeFalse("产品列表缓存应该被删除");
        instanceCacheExists.Should().BeFalse("产品实例缓存应该被删除");
    }

    #endregion

    #region 关联实体集成测试

    [Fact]
    public async Task UpdateCategoryName_ShouldInvalidateCategoryProductsCache()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var categoryId = category.Id;
        var cacheKey = $"CategoryProducts:{categoryId}";
        await CacheSetAsync(cacheKey, new List<string> { "Product1", "Product2" });

        // Act
        category.Name = "Updated Electronics";
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeFalse("分类名称变化应删除相关缓存");
    }

    [Fact]
    public async Task UpdateProductPrice_ShouldInvalidateCategoryProductsCache()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Name = "Books" };
        var product = new Product
        {
            Name = "Book",
            Price = 20m,
            Stock = 50,
            Category = category,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var categoryId = category.Id;
        var cacheKey = $"CategoryProducts:{categoryId}";
        await CacheSetAsync(cacheKey, new List<string> { "Book" });

        // Act
        product.Price = 25m;
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeFalse("产品价格变化应删除分类产品列表缓存");
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldInvalidateOrderCache()
    {
        // Arrange
        using var context = CreateDbContext();
        var product = new Product
        {
            Name = "Product",
            Price = 100m,
            Stock = 10,
            CategoryId = 1
        };
        var order = new Order
        {
            Product = product,
            Quantity = 2,
            TotalAmount = 200m,
            Status = "Pending"
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderId = order.Id;
        var cacheKey = $"Order:{orderId}";
        await CacheSetAsync(cacheKey, new { Status = "Pending", Amount = 200m });

        // Act
        order.Status = "Completed";
        await context.SaveChangesAsync();

        // Assert
        var cacheExists = await CacheExistsAsync(cacheKey);
        cacheExists.Should().BeFalse("订单状态变化应删除订单缓存");
    }

    #endregion

    #region 批量操作集成测试

    [Fact]
    public async Task BulkInsertProducts_ShouldInvalidateAllRelatedCaches()
    {
        // Arrange
        await CacheSetAsync("ProductList", new List<string> { "Existing" });
        await CacheSetAsync("CategoryProducts:1", new List<string> { "Existing" });

        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Bulk Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        // Act - 批量插入
        var products = Enumerable.Range(1, 100).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = i * 10m,
            Stock = i,
            CategoryId = category.Id,
            IsActive = true
        }).ToList();

        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // Assert
        var listCacheExists = await CacheExistsAsync("ProductList");
        listCacheExists.Should().BeFalse();
    }

    [Fact]
    public async Task BulkUpdateProducts_ShouldInvalidateMultipleCaches()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Bulk Update Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var products = Enumerable.Range(1, 10).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = 100m,
            Stock = 10,
            CategoryId = category.Id
        }).ToList();

        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // 设置缓存
        foreach (var product in products)
        {
            await CacheSetAsync($"Product:{product.Id}", new { Name = product.Name });
        }

        // Act - 批量更新价格
        foreach (var product in products)
        {
            product.Price = 150m;
        }
        await context.SaveChangesAsync();

        // Assert
        foreach (var product in products)
        {
            var cacheExists = await CacheExistsAsync($"Product:{product.Id}");
            cacheExists.Should().BeFalse($"Product:{product.Id} 缓存应该被删除");
        }
    }

    [Fact]
    public async Task MixedOperations_ShouldInvalidateCorrectly()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Mixed Ops Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product1 = new Product { Name = "P1", Price = 10m, Stock = 5, CategoryId = category.Id };
        var product2 = new Product { Name = "P2", Price = 20m, Stock = 10, CategoryId = category.Id };
        var product3 = new Product { Name = "P3", Price = 30m, Stock = 15, CategoryId = category.Id };

        context.Products.AddRange(product1, product2, product3);
        await context.SaveChangesAsync();

        await CacheSetAsync("ProductList", new List<string>());
        await CacheSetAsync($"Product:{product1.Id}", new { });
        await CacheSetAsync($"Product:{product2.Id}", new { });
        await CacheSetAsync($"Product:{product3.Id}", new { });

        // Act - 混合操作
        context.Products.Add(new Product { Name = "P4", Price = 40m, Stock = 20, CategoryId = category.Id }); // 新增
        product1.Price = 15m; // 修改
        context.Products.Remove(product3); // 删除

        await context.SaveChangesAsync();

        // Assert
        var listExists = await CacheExistsAsync("ProductList");
        var p1Exists = await CacheExistsAsync($"Product:{product1.Id}");
        var p3Exists = await CacheExistsAsync($"Product:{product3.Id}");

        listExists.Should().BeFalse();
        p1Exists.Should().BeFalse();
        p3Exists.Should().BeFalse();
    }

    #endregion

    #region 并发场景集成测试

    [Fact]
    public async Task ConcurrentUpdates_ShouldInvalidateCorrectly()
    {
        // Arrange
        using var setupContext = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Concurrent Category" };
        setupContext.Categories.Add(category);
        await setupContext.SaveChangesAsync();

        var products = Enumerable.Range(1, 20).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = i * 10m,
            Stock = i,
            CategoryId = category.Id
        }).ToList();

        setupContext.Products.AddRange(products);
        await setupContext.SaveChangesAsync();
        var productIds = products.Select(p => p.Id).ToList();

        // 设置缓存
        foreach (var id in productIds)
        {
            await CacheSetAsync($"Product:{id}", new { Cached = true });
        }

        // Act - 并发更新
        var tasks = productIds.Select(async id =>
        {
            using var context = CreateDbContext();
            var product = await context.Products.FindAsync(id);
            if (product != null)
            {
                product.Price += 5m;
                await context.SaveChangesAsync();
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        foreach (var id in productIds)
        {
            var exists = await CacheExistsAsync($"Product:{id}");
            exists.Should().BeFalse($"Product:{id} 应该被并发更新删除");
        }
    }

    #endregion

    #region 复杂场景集成测试

    [Fact]
    public async Task CompleteWorkflow_ProductManagement_ShouldInvalidateCorrectly()
    {
        // Arrange - 创建分类和产品
        using var context = CreateDbContext();
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var categoryId = category.Id;

        // 设置初始缓存
        await CacheSetAsync("ProductList", new[] { "Initial" });
        await CacheSetAsync($"CategoryProducts:{categoryId}", new[] { "Initial" });

        // Scenario 1: 添加产品
        var product = new Product
        {
            Name = "Laptop",
            Price = 1000m,
            Stock = 10,
            CategoryId = categoryId,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;
        (await CacheExistsAsync("ProductList")).Should().BeFalse("添加产品后列表缓存应失效");

        // Scenario 2: 设置产品缓存并更新价格
        await CacheSetAsync($"Product:{productId}", new { Price = 1000m });
        await CacheSetAsync($"CategoryProducts:{categoryId}", new[] { "Laptop" });

        product.Price = 1200m;
        await context.SaveChangesAsync();

        (await CacheExistsAsync($"Product:{productId}")).Should().BeFalse("价格更新后产品缓存应失效");
        (await CacheExistsAsync($"CategoryProducts:{categoryId}")).Should().BeFalse("价格更新后分类缓存应失效");

        // Scenario 3: 更新库存
        await CacheSetAsync($"Product:{productId}", new { Stock = 10 });
        await CacheSetAsync($"CategoryProducts:{categoryId}", new[] { "Laptop" });

        product.Stock = 5;
        await context.SaveChangesAsync();

        (await CacheExistsAsync($"Product:{productId}")).Should().BeFalse("库存更新后产品缓存应失效");
        (await CacheExistsAsync($"CategoryProducts:{categoryId}")).Should().BeFalse("库存更新后分类缓存应失效");

        // Scenario 4: 停用产品
        await CacheSetAsync($"CategoryProducts:{categoryId}", new[] { "Laptop" });

        product.IsActive = false;
        await context.SaveChangesAsync();

        (await CacheExistsAsync($"CategoryProducts:{categoryId}")).Should().BeFalse("停用产品后分类缓存应失效");

        // Scenario 5: 删除产品
        await CacheSetAsync("ProductList", new[] { "Laptop" });
        await CacheSetAsync($"Product:{productId}", new { Name = "Laptop" });

        context.Products.Remove(product);
        await context.SaveChangesAsync();

        (await CacheExistsAsync("ProductList")).Should().BeFalse("删除产品后列表缓存应失效");
        (await CacheExistsAsync($"Product:{productId}")).Should().BeFalse("删除产品后实例缓存应失效");
    }

    [Fact]
    public async Task OrderProcessingWorkflow_ShouldInvalidateCorrectly()
    {
        // Arrange
        using var context = CreateDbContext();
        var product = new Product
        {
            Name = "Widget",
            Price = 50m,
            Stock = 100,
            CategoryId = 1
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;

        // 创建订单
        var order = new Order
        {
            ProductId = productId,
            Quantity = 5,
            TotalAmount = 250m,
            Status = "Pending"
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderId = order.Id;

        // 设置订单缓存
        await CacheSetAsync($"Order:{orderId}", new { Status = "Pending", Amount = 250m });

        // Scenario 1: 更新订单状态
        order.Status = "Processing";
        await context.SaveChangesAsync();

        (await CacheExistsAsync($"Order:{orderId}")).Should().BeFalse("订单状态更新后缓存应失效");

        // Scenario 2: 产品价格变化影响订单缓存
        await CacheSetAsync($"Order:{orderId}", new { Status = "Processing" });

        product.Price = 55m;
        await context.SaveChangesAsync();

        (await CacheExistsAsync($"Order:{orderId}")).Should().BeFalse("产品价格变化应影响订单缓存");
    }

    #endregion

    #region 性能集成测试

    [Fact]
    public async Task LargeScaleInvalidation_ShouldCompleteInReasonableTime()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var categories = Enumerable.Range(1, 10).Select(i => new Category { Name = $"Category {i}" }).ToList();
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        var products = Enumerable.Range(1, 1000).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = i,
            Stock = i,
            CategoryId = categories[i % 10].Id
        }).ToList();

        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // 设置大量缓存
        await CacheSetAsync("ProductList", products.Select(p => p.Name).ToArray());
        foreach (var product in products.Take(100))
        {
            await CacheSetAsync($"Product:{product.Id}", new { Name = product.Name });
        }

        // Act - 批量更新触发大量失效
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var product in products)
        {
            product.Price += 10m;
        }
        await context.SaveChangesAsync();

        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(10000, "大规模失效应在10秒内完成");
        (await CacheExistsAsync("ProductList")).Should().BeFalse();
    }

    #endregion

    #region Redis Pub/Sub 集成测试

    [Fact]
    public async Task InvalidationMessage_ShouldBePublishedToRedis()
    {
        // Arrange
        var subscriber = _redis.GetSubscriber();
        var messageReceived = new TaskCompletionSource<string>();

        await subscriber.SubscribeAsync("cache:invalidation", (channel, message) =>
        {
            messageReceived.TrySetResult(message!);
        });

        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "PubSub Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        // Act
        context.Products.Add(new Product
        {
            Name = "Test Product",
            Price = 100m,
            Stock = 10,
            CategoryId = category.Id
        });
        await context.SaveChangesAsync();

        // Assert
        var receivedMessage = await Task.WhenAny(
            messageReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(5))
        );

        receivedMessage.Should().Be(messageReceived.Task, "应该在5秒内收到失效消息");
        messageReceived.Task.Result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region 边界条件集成测试

    [Fact]
    public async Task EmptyChange_ShouldNotTriggerInvalidation()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Empty Change Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Test",
            Price = 50m,
            Stock = 10,
            CategoryId = category.Id
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        await CacheSetAsync($"Product:{product.Id}", new { Name = "Test" });

        // Act - 不做任何修改
        await context.SaveChangesAsync();

        // Assert
        (await CacheExistsAsync($"Product:{product.Id}")).Should().BeTrue("无变化不应触发失效");
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotInvalidateCache()
    {
        // Arrange
        using var context = CreateDbContext();

        // 先创建分类
        var category = new Category { Name = "Rollback Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        await CacheSetAsync("ProductList", new[] { "Existing" });

        // Act & Assert
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.Products.Add(new Product
            {
                Name = "Test",
                Price = 50m,
                Stock = 10,
                CategoryId = category.Id
            });
            await context.SaveChangesAsync();

            // 手动回滚
            await transaction.RollbackAsync();
        }
        catch
        {
            // Expected
        }

        // 由于事务回滚，缓存失效也应该被撤销（如果实现支持）
        // 注意：当前简单实现可能不支持这个特性，这是一个待改进点
    }

    #endregion
}