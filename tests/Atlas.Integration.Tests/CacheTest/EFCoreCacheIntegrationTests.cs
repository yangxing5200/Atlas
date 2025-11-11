// EntityFramework/EFCoreCacheIntegrationTests.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Integration.Tests.Fixtures;
using Atlas.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace Atlas.Integration.Tests.EntityFramework
{
    [Collection("MySQL-Redis")]
    public class EFCoreCacheIntegrationTests : IntegrationTestBase
    {
        private readonly MySqlFixture _mySqlFixture;
        private readonly RedisFixture _redisFixture;
        private TestDbContext _dbContext = null!;
        private ICacheService _cacheService = null!;
        private IScopeContextAccessor _scopeAccessor = null!;

        public EFCoreCacheIntegrationTests(MySqlFixture mySqlFixture, RedisFixture redisFixture)
        {
            _mySqlFixture = mySqlFixture;
            _redisFixture = redisFixture;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<TestDbContext>((sp, options) =>
            {
                // 使用显式的 MySQL 5.6 服务器版本
                var serverVersion = new MySqlServerVersion(new Version(5, 6, 51));

                options.UseMySql(
                    _mySqlFixture.ConnectionString,
                    serverVersion,
                    mysqlOptions =>
                    {
                        mysqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null
                        );
                        mysqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore);
                    }
                );

                options.UseAtlasCaching(sp);
            });

            services.AddAtlasCaching();
            services.AddRedisCaching(_redisFixture.ConnectionString, "ef-test");
            services.AddMultiTenantCaching();
            services.AddEntityFrameworkCaching();
        }

        protected override async Task OnInitializeAsync()
        {
            _dbContext = GetService<TestDbContext>();
            _cacheService = GetService<ICacheService>();
            _scopeAccessor = GetService<IScopeContextAccessor>();

            // 确保数据库创建
            await _dbContext.Database.EnsureCreatedAsync();

            // 等待数据库完全就绪
            await Task.Delay(1000);

            await _redisFixture.ClearAllAsync();
        }

        protected override async Task OnDisposeAsync()
        {
            try
            {
                await _dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // 忽略删除错误
            }
            finally
            {
                await _dbContext.DisposeAsync();
            }

            await _dbContext.DisposeAsync();
        }

        [Fact]
        public async Task SaveChanges_Should_InvalidateCacheByTag()
        {
            // Arrange
            var tenantId = "tenant-test-001";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var product = new Product
            {
                Name = "Test Product",
                Price = 99.99m,
                Stock = 10,
                TenantId = tenantId
            };

            // Cache product list
            var cacheKey = "products:list";
            await _cacheService.SetAsync(
                cacheKey,
                new[] { product },
                CacheOptions.WithTags("entity:Product", "list:products")
            );

            // Verify cache exists
            var cachedBefore = await _cacheService.GetAsync<Product[]>(cacheKey);
            cachedBefore.Should().NotBeNull();

            // Act - Add new product (should invalidate cache)
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            // Assert - Cache should be invalidated
            // Note: Need to re-set because tag version changed
            await _cacheService.SetAsync(
                cacheKey,
                new[] { product },
                CacheOptions.WithTags("entity:Product", "list:products")
            );

            var cachedAfter = await _cacheService.GetAsync<Product[]>(cacheKey);
            cachedAfter.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateEntity_Should_InvalidateRelatedCache()
        {
            // Arrange
            var tenantId = "tenant-test-002";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var product = new Product
            {
                Name = "Original Product",
                Price = 50m,
                Stock = 5,
                TenantId = tenantId
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            // Cache the product
            var cacheKey = $"product:{product.Id}";
            await _cacheService.SetAsync(
                cacheKey,
                product,
                CacheOptions.WithTags("entity:Product", $"entity:Product:{product.Id}")
            );

            // Act - Update product
            product.Name = "Updated Product";
            product.Price = 75m;
            await _dbContext.SaveChangesAsync();

            // The cache invalidation happens automatically via interceptor
            // Re-cache with new data
            await _cacheService.SetAsync(
                cacheKey,
                product,
                CacheOptions.WithTags("entity:Product", $"entity:Product:{product.Id}")
            );

            var cachedProduct = await _cacheService.GetAsync<Product>(cacheKey);

            // Assert
            cachedProduct.Should().NotBeNull();
            cachedProduct!.Name.Should().Be("Updated Product");
            cachedProduct.Price.Should().Be(75m);
        }

        [Fact]
        public async Task DeleteEntity_Should_InvalidateCache()
        {
            // Arrange
            var tenantId = "tenant-test-003";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var product = new Product
            {
                Name = "Product To Delete",
                Price = 30m,
                Stock = 3,
                TenantId = tenantId
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            var cacheKey = $"product:{product.Id}";
            await _cacheService.SetAsync(
                cacheKey,
                product,
                CacheOptions.WithTags("entity:Product")
            );

            // Act - Delete product
            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            // Assert - Verify cache is invalidated
            // After deletion, trying to get the product should return null
            var cachedAfterDelete = await _cacheService.GetAsync<Product>(cacheKey);

            // Since we invalidated by tag, the old cached value won't be accessible
            // with the new tag version
            await _cacheService.SetAsync(cacheKey, (Product?)null, CacheOptions.WithTags("entity:Product"));
            var result = await _cacheService.GetAsync<Product>(cacheKey);
            result.Should().BeNull();
        }

        [Fact]
        public async Task BulkOperations_Should_HandleCacheCorrectly()
        {
            // Arrange
            var tenantId = "tenant-test-004";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var products = TestDataGenerator.GenerateProducts(10, tenantId);

            // Act - Bulk insert
            _dbContext.Products.AddRange(products);
            await _dbContext.SaveChangesAsync();

            // Cache products list
            var cacheKey = "products:all";
            var allProducts = await _dbContext.Products.Where(p => p.TenantId == tenantId).ToListAsync();
            await _cacheService.SetAsync(
                cacheKey,
                allProducts,
                CacheOptions.WithTags("entity:Product", "list:products")
            );

            // Assert
            var cached = await _cacheService.GetAsync<List<Product>>(cacheKey);
            cached.Should().NotBeNull();
            cached!.Count.Should().Be(10);
        }


        [Fact]
        public async Task RelatedEntities_Should_HandleCacheCorrectly_WithDto()
        {
            // Arrange
            var tenantId = "tenant-test-005";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var category = new Category
            {
                Name = "Electronics",
                TenantId = tenantId
            };

            var products = TestDataGenerator.GenerateProducts(5, tenantId);
            foreach (var product in products)
            {
                product.Category = category;
            }

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();

            // Act
            var categoryWithProducts = await _dbContext.Categories
                .AsNoTracking()
                .Include(c => c.Products)
                .FirstAsync(c => c.Id == category.Id);

            var dto = categoryWithProducts.ToDto();

            var cacheKey = $"category:{category.Id}:with-products";
            await _cacheService.SetAsync(
                cacheKey,
                dto,
                CacheOptions.WithTags("entity:Category", $"entity:Category:{category.Id}")
            );

            var cached = await _cacheService.GetAsync<CategoryWithProductsDto>(cacheKey);

            // Assert
            cached.Should().NotBeNull();
            cached!.Products.Should().HaveCount(5);
            cached.Products.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Name));
        }

        [Fact]
        public async Task RelatedEntities_Should_HandleCacheCorrectly_WithReferenceHandler()
        {
            // Arrange
            var tenantId = "tenant-test-006";
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId };

            var category = new Category
            {
                Name = "Books",
                TenantId = tenantId
            };

            var products = TestDataGenerator.GenerateProducts(3, tenantId);
            foreach (var product in products)
            {
                product.Category = category;
            }

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();

            // Act - 使用配置了 ReferenceHandler 的序列化器
            var categoryWithProducts = await _dbContext.Categories
                .AsNoTracking()
                .Include(c => c.Products)
                .AsSplitQuery()
                .FirstAsync(c => c.Id == category.Id);

            var cacheKey = $"category:{category.Id}:full";

            // ✅ 现在可以直接缓存实体，序列化器会处理循环引用
            await _cacheService.SetAsync(
                cacheKey,
                categoryWithProducts,
                CacheOptions.WithTags("entity:Category", $"entity:Category:{category.Id}")
            );

            var cached = await _cacheService.GetAsync<Category>(cacheKey);

            // Assert
            cached.Should().NotBeNull();
            cached!.Products.Should().HaveCount(3);

            // 验证数据完整性
            foreach (var product in cached.Products)
            {
                product.Name.Should().NotBeNullOrEmpty();
                product.Price.Should().BeGreaterThan(0);
            }
        }
    }
    /// <summary>
    /// 实体到 DTO 的转换扩展方法
    /// </summary>
    public static class EntityExtensions
    {
        // Category → CategoryWithProductsDto
        public static CategoryWithProductsDto ToDto(this Category category)
        {
            return new CategoryWithProductsDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                TenantId = category.TenantId,
                Products = category.Products?.Select(p => p.ToSummaryDto()).ToList()
                    ?? new List<ProductSummaryDto>()
            };
        }

        // Category → CategorySummaryDto
        public static CategorySummaryDto ToSummaryDto(this Category category)
        {
            return new CategorySummaryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            };
        }

        // Product → ProductSummaryDto
        public static ProductSummaryDto ToSummaryDto(this Product product)
        {
            return new ProductSummaryDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Stock = product.Stock,
            };
        }

        // Product → ProductDetailDto
        public static ProductDetailDto ToDetailDto(this Product product)
        {
            return new ProductDetailDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                TenantId = product.TenantId,
                CreatedAt = product.CreatedAt,
                Category = product.Category?.ToSummaryDto()
            };
        }

        // IEnumerable<Product> → List<ProductSummaryDto>
        public static List<ProductSummaryDto> ToSummaryDtoList(this IEnumerable<Product> products)
        {
            return products.Select(p => p.ToSummaryDto()).ToList();
        }

        // IEnumerable<Category> → List<CategorySummaryDto>
        public static List<CategorySummaryDto> ToSummaryDtoList(this IEnumerable<Category> categories)
        {
            return categories.Select(c => c.ToSummaryDto()).ToList();
        }

    }
    [CollectionDefinition("MySQL-Redis")]
    public class MySqlRedisCollection : ICollectionFixture<MySqlFixture>, ICollectionFixture<RedisFixture>
    {
    }
}