using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Tests.Helpers
{
    /// <summary>
    /// 测试辅助类 - 提供常用的测试数据和工厂方法
    /// </summary>
    public static class TestHelpers
    {
        // 测试常量
        public const string TestTenantId = "tenant-001";
        public const string TestStoreId = "store-001";
        public const string TestUserId = "user-001";
        public const string TestBaseKey = "test-key";
        public const string TestTag = "test-tag";

        /// <summary>
        /// 创建测试用的 ScopeContext
        /// </summary>
        public static ScopeContext CreateScopeContext(
            string? tenantId = TestTenantId,
            string? storeId = TestStoreId,
            string? userId = TestUserId)
        {
            return new ScopeContext
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = userId
            };
        }

        /// <summary>
        /// 创建测试用的 CacheKeyDefinition
        /// </summary>
        public static CacheKeyDefinition CreateKeyDefinition(
            string name = "product:{id}",
            CacheScope scope = CacheScope.Tenant,
            TimeSpan? expiration = null,
            bool allowNull = false)
        {
            return CacheKeyDefinition.Create(name)
                .WithScope(scope)
                .WithInstanceKey("id")
                .WithExpiration(expiration ?? TimeSpan.FromMinutes(30))
                .AllowNull(allowNull)
                .Build();
        }

        /// <summary>
        /// 创建测试用的 CacheOptions
        /// </summary>
        public static CacheOptions CreateCacheOptions(
            TimeSpan? expiration = null,
            params string[] tags)
        {
            return new CacheOptions
            {
                AbsoluteExpiration = expiration ?? TimeSpan.FromMinutes(30),
                Tags = new HashSet<string>(tags)
            };
        }

        /// <summary>
        /// 创建 Mock ICacheProvider
        /// </summary>
        public static Mock<ICacheProvider> CreateMockProvider()
        {
            var mock = new Mock<ICacheProvider>();

            // 默认行为：Get 返回 null
            mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // 默认行为：Set 成功
            mock.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // 默认行为：Remove 成功
            mock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // 默认行为：Exists 返回 false
            mock.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // 默认行为：GetMany 返回空字典
            mock.Setup(x => x.GetManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, byte[]?>());

            // 默认行为：RemoveMany 返回 0
            mock.Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // 默认行为：GetKeysByPattern 返回空列表
            mock.Setup(x => x.GetKeysByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            return mock;
        }

        /// <summary>
        /// 创建 Mock ITagVersionStore
        /// </summary>
        public static Mock<ITagVersionStore> CreateMockTagVersionStore()
        {
            var mock = new Mock<ITagVersionStore>();

            mock.Setup(x => x.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1L);

            mock.Setup(x => x.GetVersionsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> tags, CancellationToken ct) =>
                {
                    var result = new Dictionary<string, long>();
                    foreach (var tag in tags)
                        result[tag] = 1L;
                    return result;
                });

            mock.Setup(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2L);

            mock.Setup(x => x.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            return mock;
        }

        /// <summary>
        /// 创建 Mock IScopeContextAccessor
        /// </summary>
        public static Mock<IScopeContextAccessor> CreateMockScopeAccessor(
            string? tenantId = TestTenantId,
            string? storeId = TestStoreId,
            string? userId = TestUserId)
        {
            var mock = new Mock<IScopeContextAccessor>();
            var context = CreateScopeContext(tenantId, storeId, userId);

            mock.Setup(x => x.Current).Returns(context);
            mock.Setup(x => x.TenantId).Returns(tenantId);
            mock.Setup(x => x.StoreId).Returns(storeId);
            mock.Setup(x => x.UserId).Returns(userId);
            mock.Setup(x => x.HasTenant).Returns(!string.IsNullOrEmpty(tenantId));
            mock.Setup(x => x.HasStore).Returns(!string.IsNullOrEmpty(storeId));
            mock.Setup(x => x.HasUser).Returns(!string.IsNullOrEmpty(userId));

            return mock;
        }
    }

    /// <summary>
    /// 测试数据生成器
    /// </summary>
    public static class TestDataGenerator
    {
        /// <summary>
        /// 生成测试产品数据
        /// </summary>
        public static TestProduct CreateProduct(int id = 1, string name = "Test Product")
        {
            return new TestProduct
            {
                Id = id,
                Name = name,
                Price = 99.99m,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 生成多个测试产品
        /// </summary>
        public static List<TestProduct> CreateProducts(int count)
        {
            var products = new List<TestProduct>();
            for (int i = 1; i <= count; i++)
            {
                products.Add(CreateProduct(i, $"Product {i}"));
            }
            return products;
        }
    }

    /// <summary>
    /// 测试用的产品类
    /// </summary>
    public class TestProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is TestProduct product &&
                   Id == product.Id &&
                   Name == product.Name &&
                   Price == product.Price;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Price);
        }
    }
}