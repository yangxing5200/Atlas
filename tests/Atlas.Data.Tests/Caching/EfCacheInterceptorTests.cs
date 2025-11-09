using Atlas.Data.Abstractions.Caching;
using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Atlas.Data.Tests.Caching;

/// <summary>
/// EF 缓存拦截器完整测试套件
/// </summary>
public class EfCacheInterceptorTests
{
    #region Test Entities

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }

    private class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public Product? Product { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;

        public TestDbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Product)
                .WithMany()
                .HasForeignKey(o => o.ProductId);
        }
    }

    #endregion

    #region Test Helpers

    private (Mock<IDependencyResolver>, Mock<IStorageAdapter>, Mock<IMessageBroker>, CacheSaveChangesInterceptor) CreateInterceptor()
    {
        var resolverMock = new Mock<IDependencyResolver>();
        var storageAdapterMock = new Mock<IStorageAdapter>();
        var messageBrokerMock = new Mock<IMessageBroker>();

        var coordinator = new InvalidationCoordinator(
            storageAdapterMock.Object,
            messageBrokerMock.Object,
            Mock.Of<ILogger<InvalidationCoordinator>>());

        var interceptor = new CacheSaveChangesInterceptor(
            resolverMock.Object,
            coordinator,
            Mock.Of<ILogger<CacheSaveChangesInterceptor>>());

        return (resolverMock, storageAdapterMock, messageBrokerMock, interceptor);
    }

    private TestDbContext CreateDbContext(CacheSaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    #endregion

    #region 基础场景测试

    [Fact]
    public async Task SaveChanges_NoChanges_ShouldNotTriggerInvalidation()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        // Act
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        storageMock.Verify(x => x.RemoveManyAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveChanges_AddEntity_ShouldTriggerInvalidation()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act
        context.Products.Add(new Product { Name = "Test Product" });
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c =>
                    c.EntityType == typeof(Product) &&
                    c.State == EntityChangeState.Added)),
            It.IsAny<CancellationToken>()), Times.Once);

        storageMock.Verify(x => x.RemoveManyAsync(
            It.Is<IEnumerable<string>>(keys => keys.Contains("Product:*")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_UpdateEntity_ShouldTriggerInvalidation()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var product = new Product { Id = 1, Name = "Original" };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:1" });

        // Act
        product.Name = "Updated";
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c =>
                    c.EntityType == typeof(Product) &&
                    c.State == EntityChangeState.Modified)),
            It.IsAny<CancellationToken>()), Times.Once);

        storageMock.Verify(x => x.RemoveManyAsync(
            It.Is<IEnumerable<string>>(keys => keys.Contains("Product:1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_DeleteEntity_ShouldTriggerInvalidation()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var product = new Product { Id = 1, Name = "Test" };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:1", "Product:*" });

        // Act
        context.Products.Remove(product);
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c =>
                    c.EntityType == typeof(Product) &&
                    c.State == EntityChangeState.Deleted)),
            It.IsAny<CancellationToken>()), Times.Once);

        storageMock.Verify(x => x.RemoveManyAsync(
            It.Is<IEnumerable<string>>(keys =>
                keys.Contains("Product:1") && keys.Contains("Product:*")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 批量操作测试

    [Fact]
    public async Task SaveChanges_MultipleSameTypeEntities_ShouldCollectAllChanges()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act
        context.Products.AddRange(
            new Product { Name = "Product 1" },
            new Product { Name = "Product 2" },
            new Product { Name = "Product 3" }
        );
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Count(c => c.EntityType == typeof(Product)) == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_MultipleDifferentTypeEntities_ShouldInvalidateAll()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*", "Category:*" });

        // Act
        context.Products.Add(new Product { Name = "Product" });
        context.Categories.Add(new Category { Name = "Category" });
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c => c.EntityType == typeof(Product)) &&
                changes.Any(c => c.EntityType == typeof(Category))),
            It.IsAny<CancellationToken>()), Times.Once);

        storageMock.Verify(x => x.RemoveManyAsync(
            It.Is<IEnumerable<string>>(keys =>
                keys.Contains("Product:*") && keys.Contains("Category:*")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_MixedOperations_ShouldCaptureAllChangeTypes()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var existingProduct = new Product { Id = 1, Name = "Existing" };
        context.Products.Add(existingProduct);
        await context.SaveChangesAsync();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act
        context.Products.Add(new Product { Name = "New" }); // Added
        existingProduct.Name = "Modified"; // Modified
        context.Products.Remove(existingProduct); // Deleted
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c => c.State == EntityChangeState.Added) &&
                changes.Any(c => c.State == EntityChangeState.Deleted)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 关联实体测试

    [Fact]
    public async Task SaveChanges_RelatedEntities_ShouldInvalidateBoth()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*", "Category:*" });

        // Act
        var category = new Category { Name = "Electronics" };
        var product = new Product { Name = "Laptop", Category = category };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes =>
                changes.Any(c => c.EntityType == typeof(Product)) &&
                changes.Any(c => c.EntityType == typeof(Category))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_UpdateForeignKey_ShouldInvalidateRelatedCaches()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var category1 = new Category { Id = 1, Name = "Category 1" };
        var category2 = new Category { Id = 2, Name = "Category 2" };
        var product = new Product { Id = 1, Name = "Product", Category = category1 };

        context.Categories.AddRange(category1, category2);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:1", "Category:1:Products", "Category:2:Products" });

        // Act - 修改外键
        product.CategoryId = 2;
        await context.SaveChangesAsync();

        // Assert
        storageMock.Verify(x => x.RemoveManyAsync(
            It.Is<IEnumerable<string>>(keys =>
                keys.Contains("Category:1:Products") &&
                keys.Contains("Category:2:Products")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 属性变更检测测试

    [Fact]
    public async Task SaveChanges_OnlySpecificPropertiesChanged_ShouldCaptureModifiedProperties()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var product = new Product { Id = 1, Name = "Original", CategoryId = 1 };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        EntityChangeInfo? capturedChange = null;
        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EntityChangeInfo>, CancellationToken>((changes, _) =>
            {
                capturedChange = changes.FirstOrDefault();
            })
            .ReturnsAsync(new List<string> { "Product:1" });

        // Act - 只修改 Name 属性
        product.Name = "Updated";
        await context.SaveChangesAsync();

        // Assert
        capturedChange.Should().NotBeNull();
        capturedChange!.ModifiedProperties.Should().Contain("Name");
        capturedChange.ModifiedProperties.Should().NotContain("CategoryId");
        capturedChange.Entity.Should().Be(product);
    }

    [Fact]
    public async Task SaveChanges_WithOldAndNewValues_ShouldCaptureValueChanges()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        var product = new Product { Id = 1, Name = "Original", CategoryId = 1 };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        EntityChangeInfo? capturedChange = null;
        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EntityChangeInfo>, CancellationToken>((changes, _) =>
            {
                capturedChange = changes.FirstOrDefault();
            })
            .ReturnsAsync(new List<string> { "Product:1" });

        // Act
        product.Name = "Updated";
        await context.SaveChangesAsync();

        // Assert
        capturedChange.Should().NotBeNull();
        capturedChange!.OldValues.Should().ContainKey("Name");
        capturedChange.OldValues["Name"].Should().Be("Original");
        capturedChange.NewValues.Should().ContainKey("Name");
        capturedChange.NewValues["Name"].Should().Be("Updated");
    }

    [Fact]
    public async Task SaveChanges_NoActualChange_ShouldNotTriggerInvalidation()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);
        var product = new Product { Id = 1, Name = "Test" };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // 重置 mock 的调用记录，只关注后续的调用
        resolverMock.Invocations.Clear();

        // Act - 设置相同的值
        product.Name = "Test";
        await context.SaveChangesAsync();

        // Assert - 应该不触发失效（因为没有实际变更）
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
    #endregion

    #region 实体信息提取测试

    [Fact]
    public async Task SaveChanges_ShouldCaptureEntityInstance()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        EntityChangeInfo? capturedChange = null;
        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EntityChangeInfo>, CancellationToken>((changes, _) =>
            {
                capturedChange = changes.FirstOrDefault();
            })
            .ReturnsAsync(new List<string>());

        var product = new Product { Id = 1, Name = "Test" };

        // Act
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Assert
        capturedChange.Should().NotBeNull();
        capturedChange!.Entity.Should().BeSameAs(product);
        capturedChange.EntityType.Should().Be(typeof(Product));
    }

    #endregion

    #region 消息发布测试

    [Fact]
    public async Task SaveChanges_WithInvalidationKeys_ShouldPublishMessage()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act
        context.Products.Add(new Product { Name = "Test" });
        await context.SaveChangesAsync();

        // Assert
        brokerMock.Verify(x => x.PublishAsync(
            It.Is<InvalidationMessage>(msg => msg.Keys.Contains("Product:*")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChanges_NoInvalidationKeys_ShouldNotPublishMessage()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>()); // 返回空列表

        // Act
        context.Products.Add(new Product { Name = "Test" });
        await context.SaveChangesAsync();

        // Assert
        storageMock.Verify(x => x.RemoveManyAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        brokerMock.Verify(x => x.PublishAsync(
            It.IsAny<InvalidationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 异常处理测试

    [Fact]
    public async Task SaveChanges_ResolverThrowsException_ShouldPropagateException()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Resolver error"));

        context.Products.Add(new Product { Name = "Test" });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_InvalidationThrowsException_ShouldPropagateException()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        storageMock.Setup(x => x.RemoveManyAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        context.Products.Add(new Product { Name = "Test" });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.SaveChangesAsync());
    }

    #endregion

    #region 并发测试

    [Fact]
    public async Task SaveChanges_ConcurrentOperations_ShouldHandleCorrectly()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act - 并发保存多个上下文
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var context = CreateDbContext(interceptor);
            context.Products.Add(new Product { Name = $"Product {i}" });
            await context.SaveChangesAsync();
        });

        await Task.WhenAll(tasks);

        // Assert
        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    #endregion

    #region 取消令牌测试

    [Fact]
    public async Task SaveChanges_WithCancellationToken_ShouldPassToResolver()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);
        using var cts = new CancellationTokenSource();

        CancellationToken capturedToken = default;
        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EntityChangeInfo>, CancellationToken>((_, token) =>
            {
                capturedToken = token;
            })
            .ReturnsAsync(new List<string>());

        // Act
        context.Products.Add(new Product { Name = "Test" });
        await context.SaveChangesAsync(cts.Token);

        // Assert
        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task SaveChanges_CancelledToken_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        context.Products.Add(new Product { Name = "Test" });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await context.SaveChangesAsync(cts.Token));
    }

    #endregion

    #region 性能测试

    [Fact]
    public async Task SaveChanges_LargeNumberOfEntities_ShouldPerformEfficiently()
    {
        // Arrange
        var (resolverMock, storageMock, brokerMock, interceptor) = CreateInterceptor();
        using var context = CreateDbContext(interceptor);

        resolverMock.Setup(x => x.ResolveInvalidationKeysAsync(
            It.IsAny<IEnumerable<EntityChangeInfo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product:*" });

        // Act
        var products = Enumerable.Range(1, 1000)
            .Select(i => new Product { Name = $"Product {i}" })
            .ToList();

        context.Products.AddRange(products);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await context.SaveChangesAsync();
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(5000); // 应该在5秒内完成

        resolverMock.Verify(x => x.ResolveInvalidationKeysAsync(
            It.Is<IEnumerable<EntityChangeInfo>>(changes => changes.Count() == 1000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}