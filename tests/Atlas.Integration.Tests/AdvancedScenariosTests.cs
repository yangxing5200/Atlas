using Atlas.Core.Enums;
using Atlas.Data.Tenant.Repositories;
using Atlas.Data.Tenant.Repositories.Impl;
using Atlas.Models.Tenant.Entities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.Repositories
{
    /// <summary>
    /// 高级场景测试
    /// 测试复杂业务场景和边界情况
    /// </summary>
    public class AdvancedScenariosTests : TenantTestBase
    {
        private IStoreRepository _storeRepository = null!;
        private IProductRepository _productRepository = null!;
        private IMemberRepository _memberRepository = null!;
        private IOrderRepository _orderRepository = null!;
        private IInventoryRepository _inventoryRepository = null!;

        protected override void ConfigureAdditionalServices(
            IServiceCollection services,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            services.AddScoped<IStoreRepository, StoreRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IMemberRepository, MemberRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
        }

        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
            _storeRepository = GetService<IStoreRepository>();
            _productRepository = GetService<IProductRepository>();
            _memberRepository = GetService<IMemberRepository>();
            _orderRepository = GetService<IOrderRepository>();
            _inventoryRepository = GetService<IInventoryRepository>();

            await SetupCompleteStoreHierarchy();
        }

        #region 场景1：总部分发商品到门店

        [Fact]
        public async Task Scenario_HeadquartersDistribute_ProductToStores()
        {
            // Arrange - 平台总部创建标准商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 0);
            var standardProduct = new Product
            {
                StoreId = 0,
                TenantId = TestTenants.ChainEnterprise,
                Name = "标准商品套餐",
                Price = 99.99m,
                SourceStoreId = null,
                IsCustomized = false
            };
            await _productRepository.AddAsync(standardProduct);
            await _productRepository.SaveChangesAsync();

            // Act - 门店A直接使用总部商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var products = await _productRepository.GetAllAsync();

            // Assert - 门店A可以看到总部商品
            products.Should().HaveCount(1);
            products.First().Name.Should().Be("标准商品套餐");
            products.First().StoreId.Should().Be(0); // 原始创建门店
        }

        [Fact]
        public async Task Scenario_Store_CustomizeHeadquartersProduct()
        {
            // Arrange - 总部创建标准商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 0);
            var standardProduct = new Product
            {
                StoreId = 0,
                TenantId = TestTenants.ChainEnterprise,
                Name = "标准商品",
                Price = 100m,
                SourceStoreId = null,
                IsCustomized = false
            };
            await _productRepository.AddAsync(standardProduct);
            await _productRepository.SaveChangesAsync();
            var standardProductId = standardProduct.Id;

            // Act - 门店A基于总部商品创建自定义版本
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var customizedProduct = new Product
            {
                StoreId = 1,
                TenantId = TestTenants.ChainEnterprise,
                Name = "标准商品-A店特惠",
                Price = 89.99m,  // 门店自定义价格
                SourceStoreId = 0,  // 标记来源于总部
                IsCustomized = true
            };
            await _productRepository.AddAsync(customizedProduct);
            await _productRepository.SaveChangesAsync();

            // Assert - 门店A现在可以看到两个商品
            var products = await _productRepository.GetAllAsync();
            products.Should().HaveCount(2);
            products.Should().Contain(p => p.StoreId == 0 && !p.IsCustomized);
            products.Should().Contain(p => p.StoreId == 1 && p.IsCustomized);
        }

        #endregion

        #region 场景2：跨门店会员消费

        [Fact]
        public async Task Scenario_Member_ConsumeInDifferentStores()
        {
            // Arrange - 门店A注册会员
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var member = new Member
            {
                StoreId = 1,
                TenantId = TestTenants.ChainEnterprise,
                MemberName = "张三",
                Phone = "13800138000",
                Points = 0
            };
            await _memberRepository.AddAsync(member);
            await _memberRepository.SaveChangesAsync();
            var memberId = member.Id;

            // Act - 会员在门店B消费（门店B可以查到该会员）
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var memberInStoreB = await _memberRepository.GetByIdAsync(memberId);
            memberInStoreB.Should().NotBeNull();

            // 门店B创建订单
            var order = new Order
            {
                StoreId = 2,  // 订单属于门店B
                TenantId = TestTenants.ChainEnterprise,
                OrderNo = "ORDER-B-001",
                MemberId = memberId,  // 但会员是在门店A注册的
                TotalAmount = 200m,
                Status = OrderStatus.Comfirm,
            };
            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();

            // Assert - 验证数据归属
            var savedOrder = await _orderRepository.GetByIdAsync(order.Id);
            savedOrder.Should().NotBeNull();
            savedOrder!.StoreId.Should().Be(2);  // 订单属于门店B
            savedOrder.MemberId.Should().Be(memberId);  // 会员来自门店A

            // 门店A看不到门店B的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var orderInStoreA = await _orderRepository.GetByIdAsync(order.Id);
            orderInStoreA.Should().BeNull();
        }

        #endregion

        #region 场景3：多门店库存管理

        [Fact]
        public async Task Scenario_SameProduct_DifferentInventory_InStores()
        {
            // Arrange - 总部创建商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 0);
            var product = new Product
            {
                StoreId = 0,
                TenantId = TestTenants.ChainEnterprise,
                Name = "热销商品",
                Price = 50m
            };
            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();
            var productId = product.Id;

            // Act - 不同门店设置各自的库存
            // 门店A设置库存
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var inventoryA = new Inventory
            {
                StoreId = 1,
                TenantId = TestTenants.ChainEnterprise,
                ProductId = productId,
                Quantity = 100,
                SafetyStock = 20
            };
            await _inventoryRepository.AddAsync(inventoryA);
            await _inventoryRepository.SaveChangesAsync();

            // 门店B设置库存
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var inventoryB = new Inventory
            {
                StoreId = 2,
                TenantId = TestTenants.ChainEnterprise,
                ProductId = productId,
                Quantity = 50,
                SafetyStock = 10
            };
            await _inventoryRepository.AddAsync(inventoryB);
            await _inventoryRepository.SaveChangesAsync();

            // Assert - 验证库存独立
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var inventoryInA = await _inventoryRepository.FirstOrDefaultAsync(
                i => i.ProductId == productId);
            inventoryInA.Should().NotBeNull();
            inventoryInA!.Quantity.Should().Be(100);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var inventoryInB = await _inventoryRepository.FirstOrDefaultAsync(
                i => i.ProductId == productId);
            inventoryInB.Should().NotBeNull();
            inventoryInB!.Quantity.Should().Be(50);
        }

        #endregion

        #region 场景4：门店层级权限验证

        [Fact]
        public async Task Scenario_FranchiseStore_CannotAccess_PlatformData()
        {
            // Arrange - 平台总部创建数据
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 0);
            var platformProduct = new Product
            {
                StoreId = 0,
                TenantId = TestTenants.ChainEnterprise,
                Name = "平台专属商品",
                Price = 999m
            };
            await _productRepository.AddAsync(platformProduct);

            var platformMember = new Member
            {
                StoreId = 0,
                TenantId = TestTenants.ChainEnterprise,
                MemberName = "平台VIP",
                Phone = "13900000000"
            };
            await _memberRepository.AddAsync(platformMember);
            await _productRepository.SaveChangesAsync();

            // Act - 加盟商X的加盟门店F尝试访问
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var products = await _productRepository.GetAllAsync();
            var members = await _memberRepository.GetAllAsync();

            // Assert - 完全看不到平台数据
            products.Should().BeEmpty();
            members.Should().BeEmpty();
        }

        [Fact]
        public async Task Scenario_FranchiseHeadquarters_CannotAccess_FranchiseStoreData()
        {
            // Arrange - 加盟门店F创建商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var franchiseProduct = new Product
            {
                StoreId = 103,
                TenantId = TestTenants.ChainEnterprise,
                Name = "加盟门店F独家商品",
                Price = 88m
            };
            await _productRepository.AddAsync(franchiseProduct);
            await _productRepository.SaveChangesAsync();

            // Act - 加盟商X总部尝试访问
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var products = await _productRepository.GetAllAsync();

            // Assert - 总部看不到加盟门店的商品
            products.Should().NotContain(p => p.StoreId == 103);
        }

        #endregion

        #region 场景5：数据统计与报表

        [Fact]
        public async Task Scenario_Store_Statistics_OnlyOwnData()
        {
            // Arrange - 多个门店创建订单
            await CreateMultipleOrdersForStore(1, 10, totalAmount: 100m);
            await CreateMultipleOrdersForStore(2, 15, totalAmount: 200m);
            await CreateMultipleOrdersForStore(3, 5, totalAmount: 300m);

            // Act & Assert - 门店A统计
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var countA = await _orderRepository.CountAsync();
            var totalA = await _orderRepository.SumAsync(o => o.TotalAmount);
            countA.Should().Be(10);
            totalA.Should().Be(1000m);

            // Act & Assert - 门店B统计
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var countB = await _orderRepository.CountAsync();
            var totalB = await _orderRepository.SumAsync(o => o.TotalAmount);
            countB.Should().Be(15);
            totalB.Should().Be(3000m);
        }

        [Fact]
        public async Task Scenario_SharedData_Statistics_AcrossStores()
        {
            // Arrange - 不同门店创建商品
            await CreateProductAsStore(0, "总部商品A", 100m);
            await CreateProductAsStore(0, "总部商品B", 200m);
            await CreateProductAsStore(1, "门店A商品", 150m);
            await CreateProductAsStore(2, "门店B商品", 250m);

            // Act - 门店A统计（可以看到总部 + A + B）
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var count = await _productRepository.CountAsync();
            var avgPrice = await _productRepository.AverageAsync(p => p.Price);
            var maxPrice = await _productRepository.MaxAsync(p => p.Price);

            // Assert
            count.Should().Be(4);  // 总部2个 + 门店A1个 + 门店B1个
            avgPrice.Should().Be(175m);  // (100+200+150+250)/4
            maxPrice.Should().Be(250m);
        }

        #endregion

        #region 场景6：分页查询性能

        [Fact]
        public async Task Scenario_PagedQuery_WithStoreFilter()
        {
            // Arrange - 创建大量数据
            await CreateMultipleProductsForStore(1, 50);
            await CreateMultipleProductsForStore(2, 30);

            // Act - 门店A分页查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var (items, total) = await _productRepository.GetPagedAsync(
                pageIndex: 1,
                pageSize: 20);

            // Assert
            items.Should().HaveCount(20);
            total.Should().Be(50);
            items.Should().AllSatisfy(p => p.StoreId.Should().Be(1));

            // Act - 第二页
            var (items2, total2) = await _productRepository.GetPagedAsync(
                pageIndex: 2,
                pageSize: 20);

            // Assert
            items2.Should().HaveCount(20);
            total2.Should().Be(50);
        }

        #endregion

        #region 场景7：并发操作

        [Fact]
        public async Task Scenario_ConcurrentAccess_DifferentStores()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act - 模拟多个门店同时创建商品
            for (int storeId = 1; storeId <= 3; storeId++)
            {
                var currentStoreId = storeId;
                tasks.Add(Task.Run(async () =>
                {
                    SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, currentStoreId);
                    for (int i = 0; i < 10; i++)
                    {
                        var product = new Product
                        {
                            StoreId = currentStoreId,
                            TenantId = TestTenants.ChainEnterprise,
                            Name = $"Store{currentStoreId}-Product{i}",
                            Price = 100m
                        };
                        await _productRepository.AddAsync(product);
                        await _productRepository.SaveChangesAsync();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - 验证每个门店的数据
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var count1 = await _productRepository.CountAsync();
            count1.Should().Be(10);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var count2 = await _productRepository.CountAsync();
            count2.Should().Be(10);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 3);
            var count3 = await _productRepository.CountAsync();
            count3.Should().Be(10);
        }

        #endregion

        #region 辅助方法

        private async Task SetupCompleteStoreHierarchy()
        {
            var stores = new[]
            {
                new Store { Id = 0, TenantId = TestTenants.ChainEnterprise, Code = "HQ", Name = "平台总部", Type = StoreType.Headquarters, ParentStoreId = null, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 1, TenantId = TestTenants.ChainEnterprise, Code = "STORE-A", Name = "平台直营A", Type = StoreType.DirectOperated, ParentStoreId = 0, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 2, TenantId = TestTenants.ChainEnterprise, Code = "STORE-B", Name = "平台直营B", Type = StoreType.DirectOperated, ParentStoreId = 0, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 3, TenantId = TestTenants.ChainEnterprise, Code = "STORE-C", Name = "平台直营C", Type = StoreType.DirectOperated, ParentStoreId = 0, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 100, TenantId = TestTenants.ChainEnterprise, Code = "HQ-X", Name = "加盟商X总部", Type = StoreType.FranchiseHeadquarters, ParentStoreId = 0, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 101, TenantId = TestTenants.ChainEnterprise, Code = "STORE-D", Name = "加盟商X直营D", Type = StoreType.DirectOperated, ParentStoreId = 100, IsActive = true, Status = StoreStatus.Active },
                new Store { Id = 103, TenantId = TestTenants.ChainEnterprise, Code = "STORE-F", Name = "加盟商X加盟F", Type = StoreType.Franchised, ParentStoreId = 100, IsActive = true, Status = StoreStatus.Active },
            };

            await _storeRepository.AddRangeAsync(stores);
            await _storeRepository.SaveChangesAsync();
        }

        private async Task CreateProductAsStore(long storeId, string name, decimal price)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var product = new Product
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                Name = name,
                Price = price
            };
            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();
        }

        private async Task CreateMultipleProductsForStore(long storeId, int count)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var products = Enumerable.Range(1, count).Select(i => new Product
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                Name = $"Product-{storeId}-{i}",
                Price = i * 10m
            });
            await _productRepository.AddRangeAsync(products);
            await _productRepository.SaveChangesAsync();
        }

        private async Task CreateMultipleOrdersForStore(long storeId, int count, decimal totalAmount)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var orders = Enumerable.Range(1, count).Select(i => new Order
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                OrderNo = $"ORDER-{storeId}-{i:D3}",
                TotalAmount = totalAmount,
                Status = OrderStatus.Comfirm
            });
            await _orderRepository.AddRangeAsync(orders);
            await _orderRepository.SaveChangesAsync();
        }

        #endregion
    }
}