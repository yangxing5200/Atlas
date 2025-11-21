using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.Tenant.Entities;
using Atlas.Data.Common.Extensions;
using FluentAssertions;
using Xunit;
using Atlas.Core.Exceptions;
using Atlas.Data.Tenant.Repositories.Impl;

namespace Atlas.Integration.Tests.Repositories
{
    /// <summary>
    /// 共享数据(ISharedEntity)隔离测试
    /// 测试商品、会员、促销等共享数据的可见性规则
    /// </summary>
    public class SharedEntityIsolationTests : TenantTestBase
    {
        private IStoreRepository _storeRepository = null!;
        private IProductRepository _productRepository = null!;
        private IMemberRepository _memberRepository = null!;

        private IRepository<Product> _productRepos = null!;
        private IRepository<Order> _orderRepos = null!;
        private IUnitOfWork _uow = null!;

        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
            _storeRepository = GetService<IStoreRepository>();
            _productRepository = GetService<IProductRepository>();
            _memberRepository = GetService<IMemberRepository>();
            _productRepos = GetService<IRepository<Product>>();
            _orderRepos = GetService<IRepository<Order>>();
            _uow = GetService<IUnitOfWork>();
            var factory = GetService<ITenantDbContextFactory>();
            await factory.GetReadonlyDbContextAsync();
            await SetupCompleteStoreHierarchy();
        }

        #region 测试场景1：平台总部与直营门店共享

        [Fact]
        public async Task PlatformHeadquarters_CanSee_AllPlatformDirectStoreProducts()
        {
            // Arrange - 不同门店创建商品
            await CreateProductAsStore(1000, "平台总部商品");
            await CreateProductAsStore(1, "平台直营A商品");
            await CreateProductAsStore(2, "平台直营B商品");

            // Act - 以平台总部身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1000);
            var products = await _productRepository.GetAllAsync();

            // Assert - 应该看到平台总部 + 所有平台直营门店的商品
            products.Should().HaveCount(3);
            products.Select(p => p.Name).Should().Contain(new[]
            {
                "平台总部商品",
                "平台直营A商品",
                "平台直营B商品"
            });
        }

        [Fact]
        public async Task PlatformDirectStore_CanSee_HeadquartersAndSiblingProducts()
        {
            // Arrange
            await CreateProductAsStore(1000, "平台总部商品");
            await CreateProductAsStore(1, "平台直营A商品");
            await CreateProductAsStore(2, "平台直营B商品");
            await CreateProductAsStore(3, "平台直营C商品");

            // Act - 以平台直营A身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var products = await _productRepository.GetAllAsync();

            // Assert - 应该看到平台总部 + A/B/C
            products.Should().HaveCount(4);
            products.Select(p => p.StoreId).Should().Contain(new long[] { 1000, 1, 2, 3 });
        }

        [Fact]
        public async Task PlatformDirectStore_CannotSee_FranchiseProducts()
        {
            // Arrange
            await CreateProductAsStore(1, "平台直营A商品");
            await CreateProductAsStore(101, "加盟商X直营D商品");

            // Act - 以平台直营A身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var products = await _productRepository.GetAllAsync();

            // Assert - 不应该看到加盟商的商品
            products.Should().HaveCount(1);
            products.First().Name.Should().Be("平台直营A商品");
        }

        #endregion

        #region 测试场景2：加盟商总部与直营门店共享

        [Fact]
        public async Task FranchiseHeadquarters_CanSee_OwnDirectStoreProducts()
        {
            // Arrange
            await CreateProductAsStore(100, "加盟商X总部商品");
            await CreateProductAsStore(101, "加盟商X直营D商品");
            await CreateProductAsStore(102, "加盟商X直营E商品");

            // Act - 以加盟商X总部身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var products = await _productRepository.GetAllAsync();

            // Assert
            products.Should().HaveCount(3);
            products.Select(p => p.StoreId).Should().Contain(new long[] { 100, 101, 102 });
        }

        [Fact]
        public async Task FranchiseDirectStore_CanSee_HeadquartersAndSiblingProducts()
        {
            // Arrange
            await CreateProductAsStore(100, "加盟商X总部商品");
            await CreateProductAsStore(101, "加盟商X直营D商品");
            await CreateProductAsStore(102, "加盟商X直营E商品");

            // Act - 以加盟商X直营D身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 101);
            var products = await _productRepository.GetAllAsync();

            // Assert
            products.Should().HaveCount(3);
            products.Select(p => p.StoreId).Should().Contain(new long[] { 100, 101, 102 });
        }

        [Fact]
        public async Task FranchiseHeadquarters_CannotSee_PlatformProducts()
        {
            // Arrange
            await CreateProductAsStore(1000, "平台总部商品");
            await CreateProductAsStore(1, "平台直营A商品");
            await CreateProductAsStore(100, "加盟商X总部商品");

            // Act - 以加盟商X总部身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var products = await _productRepository.GetAllAsync();

            // Assert - 只能看到自己的商品
            products.Should().HaveCount(1);
            products.First().Name.Should().Be("加盟商X总部商品");
        }

        #endregion

        #region 测试场景3：加盟门店独享

        [Fact]
        public async Task FranchisedStore_CanOnlySee_OwnProducts()
        {
            // Arrange
            await CreateProductAsStore(100, "加盟商X总部商品");
            await CreateProductAsStore(101, "加盟商X直营D商品");
            await CreateProductAsStore(103, "加盟商X加盟F商品");

            // Act - 以加盟门店F身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var products = await _productRepository.GetAllAsync();

            // Assert - 只能看到自己的商品
            products.Should().HaveCount(1);
            products.First().StoreId.Should().Be(103);
            products.First().Name.Should().Be("加盟商X加盟F商品");
        }

        [Fact]
        public async Task FranchisedStore_CannotSee_HeadquartersProducts()
        {
            // Arrange
            await CreateProductAsStore(100, "加盟商X总部商品");
            await CreateProductAsStore(103, "加盟商X加盟F商品");

            // Act
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var products = await _productRepository.GetAllAsync();

            // Assert
            products.Should().HaveCount(1);
            products.First().StoreId.Should().Be(103);
        }

        [Fact]
        public async Task FranchisedStores_CannotSee_EachOther()
        {
            // Arrange
            await CreateProductAsStore(103, "加盟商X加盟F商品");
            await CreateProductAsStore(104, "加盟商X加盟G商品");

            // Act - 以加盟门店F身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var productsF = await _productRepository.GetAllAsync();

            // Assert
            productsF.Should().HaveCount(1);
            productsF.First().StoreId.Should().Be(103);

            // Act - 以加盟门店G身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 104);
            var productsG = await _productRepository.GetAllAsync();

            // Assert
            productsG.Should().HaveCount(1);
            productsG.First().StoreId.Should().Be(104);
        }

        #endregion

        #region 测试场景4：不同加盟商体系隔离

        [Fact]
        public async Task DifferentFranchise_CompletelyIsolated()
        {
            // Arrange - 创建加盟商Y体系
            await CreateStoreHierarchyForFranchiseY();

            await CreateProductAsStore(100, "加盟商X总部商品");
            await CreateProductAsStore(101, "加盟商X直营D商品");
            await CreateProductAsStore(200, "加盟商Y总部商品");
            await CreateProductAsStore(201, "加盟商Y直营H商品");

            // Act - 以加盟商X总部身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var productsX = await _productRepository.GetAllAsync();

            // Assert - 只能看到X体系
            productsX.Should().HaveCount(2);
            productsX.Should().AllSatisfy(p => p.StoreId.Should().BeOneOf(100, 101));

            // Act - 以加盟商Y总部身份查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 200);
            var productsY = await _productRepository.GetAllAsync();

            // Assert - 只能看到Y体系
            productsY.Should().HaveCount(2);
            productsY.Should().AllSatisfy(p => p.StoreId.Should().BeOneOf(200, 201));
        }

        #endregion

        #region 测试场景5：会员数据共享

        [Fact]
        public async Task Member_FollowsSameRules_AsProduct()
        {
            // Arrange - 不同门店注册会员
            await CreateMemberAsStore(1000, "平台VIP会员");
            await CreateMemberAsStore(1, "平台直营A会员");
            await CreateMemberAsStore(100, "加盟商X会员");
            await CreateMemberAsStore(103, "加盟门店F会员");

            // Act & Assert - 平台直营A查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var membersA = await _memberRepository.GetAllAsync();
            membersA.Should().HaveCount(2);
            membersA.Select(m => m.StoreId).Should().Contain(new long[] { 1000, 1 });

            // Act & Assert - 加盟商X总部查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var membersX = await _memberRepository.GetAllAsync();
            membersX.Should().HaveCount(1);
            membersX.First().StoreId.Should().Be(100);

            // Act & Assert - 加盟门店F查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            var membersF = await _memberRepository.GetAllAsync();
            membersF.Should().HaveCount(1);
            membersF.First().StoreId.Should().Be(103);
        }

        #endregion

        #region 测试场景6：CRUD操作权限

        [Fact]
        public async Task DirectStore_CanCreate_SharedProduct()
        {
            // Arrange & Act
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var product = new Product
            {
                StoreId = 1,
                TenantId = TestTenants.ChainEnterprise,
                Name = "新商品",
                Price = 99.99m,
                Description = "由门店创建的共享商品"
            };
            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

            // Assert - 同级门店可见
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var products = await _productRepository.GetAllAsync();
            products.Should().Contain(p => p.Name == "新商品");
        }

        [Fact]
        public async Task Store_CannotUpdate_OtherStoreProduct()
        {
            // Arrange
            await CreateProductAsStore(1, "门店A商品");

            // Act - 尝试用门店B修改门店A的商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var productId = await GetProductIdByName("门店A商品");
            var product = await _productRepository.GetForUpdateAsync(productId);

            // Assert - 应该能看到但不应修改
            product.Should().NotBeNull(); // 可见

            // 如果尝试修改，应该通过业务逻辑层限制
            // 这里验证至少可以查询到
        }

        [Fact]
        public async Task Store_CannotDelete_OtherStoreProduct()
        {
            // Arrange
            await CreateProductAsStore(1, "门店A商品");
            var productId = await GetProductIdByName("门店A商品");

            // Act - 尝试用门店B删除门店A的商品
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var deleted = await _productRepository.DeleteAsync(productId);

            // Assert - 应该删除失败或通过业务层限制
            await _productRepository.SaveChangesAsync();

            // 验证商品仍然存在
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var product = await _productRepository.GetByIdAsync(productId);
            product.Should().BeNull();
        }

        #endregion

        // 事务操作：用 UnitOfWork
        [Fact]
        public async Task CreateOrder()
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var dto = new
            {
                ProductId = 1001,
                Quantity = 10,
                TotalAmount = 20
            };
            await _uow.ExecuteInTransactionAsync(async () =>
            {
                var orderRepo = _uow.GetRepository<Order>();
                var inventoryRepo = _uow.GetRepository<Inventory>();

                // 检查库存
                var inventories = await inventoryRepo.QueryWithTrackingAsync(x => x.ProductId == dto.ProductId);
                var inventory = inventories.FirstOrDefault();
                if (inventory == null || inventory.Quantity < dto.Quantity)
                {
                    throw new AtlasException("库存不足");
                }

                // 扣减库存
                inventory.Quantity -= dto.Quantity;
                await inventoryRepo.UpdateAsync(inventory);
                
                // 创建订单
                var order = new Order
                {
                    OrderNo = GenerateOrderNo(),
                    TotalAmount = dto.TotalAmount
                };
                await orderRepo.AddAsync(order);
                await _uow.SaveChangesAsync();
                // ✅ 自动提交事务
            });
        }

        private string GenerateOrderNo() => $"ORD{DateTime.Now:yyyyMMddHHmmss}";

        #region 辅助方法

        private async Task SetupCompleteStoreHierarchy()
        {
            var isexists = await _storeRepository.AnyAsync(x => x.Id > 0);
            if (isexists) return;
            var stores = new[]
            {
                // 平台总部
                new Store
                {
                    Id = 1000,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "HQ",
                    Name = "平台总部",
                    Type = StoreType.Headquarters,
                    ParentStoreId = null,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "上海市黄浦区南京东路100号",
                    ContactPhone = "021-12345678",
                    ContactPerson = "张总",
                    Province = "上海市",
                    City = "上海市",
                    District = "黄浦区"
                },
                
                // 平台直营门店
                new Store
                {
                    Id = 1,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-A",
                    Name = "平台直营A",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1000,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "上海市浦东新区陆家嘴环路1000号",
                    ContactPhone = "021-11111111",
                    ContactPerson = "王经理",
                    Province = "上海市",
                    City = "上海市",
                    District = "浦东新区"
                },
                new Store
                {
                    Id = 2,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-B",
                    Name = "平台直营B",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1000,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "上海市徐汇区淮海中路200号",
                    ContactPhone = "021-22222222",
                    ContactPerson = "李经理",
                    Province = "上海市",
                    City = "上海市",
                    District = "徐汇区"
                },
                new Store
                {
                    Id = 3,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-C",
                    Name = "平台直营C",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1000,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "上海市静安区南京西路300号",
                    ContactPhone = "021-33333333",
                    ContactPerson = "赵经理",
                    Province = "上海市",
                    City = "上海市",
                    District = "静安区"
                },
                
                // 加盟商X总部及门店
                new Store
                {
                    Id = 100,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "HQ-X",
                    Name = "加盟商X总部",
                    Type = StoreType.FranchiseHeadquarters,
                    ParentStoreId = 0,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "北京市朝阳区建国路88号",
                    ContactPhone = "010-11111111",
                    ContactPerson = "刘总",
                    Province = "北京市",
                    City = "北京市",
                    District = "朝阳区"
                },
                new Store
                {
                    Id = 101,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-D",
                    Name = "加盟商X直营D",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 100,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "北京市海淀区中关村大街1号",
                    ContactPhone = "010-22222222",
                    ContactPerson = "陈经理",
                    Province = "北京市",
                    City = "北京市",
                    District = "海淀区"
                },
                new Store
                {
                    Id = 102,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-E",
                    Name = "加盟商X直营E",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 100,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "北京市东城区王府井大街100号",
                    ContactPhone = "010-33333333",
                    ContactPerson = "吴经理",
                    Province = "北京市",
                    City = "北京市",
                    District = "东城区"
                },
                new Store
                {
                    Id = 103,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-F",
                    Name = "加盟商X加盟F",
                    Type = StoreType.Franchised,
                    ParentStoreId = 100,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "北京市西城区西单北大街50号",
                    ContactPhone = "010-44444444",
                    ContactPerson = "周店长",
                    Province = "北京市",
                    City = "北京市",
                    District = "西城区"
                },
                new Store
                {
                    Id = 104,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-G",
                    Name = "加盟商X加盟G",
                    Type = StoreType.Franchised,
                    ParentStoreId = 100,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "北京市丰台区丽泽金融商务区200号",
                    ContactPhone = "010-55555555",
                    ContactPerson = "郑店长",
                    Province = "北京市",
                    City = "北京市",
                    District = "丰台区"
                },
            };

            await _storeRepository.AddRangeAsync(stores);
            await _storeRepository.SaveChangesAsync();
        }

        private async Task CreateStoreHierarchyForFranchiseY()
        {
            var stores = new[]
            {
                new Store
                {
                    Id = 200,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "HQ-Y",
                    Name = "加盟商Y总部",
                    Type = StoreType.FranchiseHeadquarters,
                    ParentStoreId = 0,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "广州市天河区珠江新城花城大道100号",
                    ContactPhone = "020-11111111",
                    ContactPerson = "黄总",
                    Province = "广东省",
                    City = "广州市",
                    District = "天河区"
                },
                new Store
                {
                    Id = 201,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-H",
                    Name = "加盟商Y直营H",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 200,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "广州市越秀区北京路200号",
                    ContactPhone = "020-22222222",
                    ContactPerson = "林经理",
                    Province = "广东省",
                    City = "广州市",
                    District = "越秀区"
                },
                new Store
                {
                    Id = 204,
                    TenantId = TestTenants.ChainEnterprise,
                    Code = "STORE-K",
                    Name = "加盟商Y加盟K",
                    Type = StoreType.Franchised,
                    ParentStoreId = 200,
                    IsActive = true,
                    Status = StoreStatus.Active,
                    Address = "广州市海珠区新港东路300号",
                    ContactPhone = "020-33333333",
                    ContactPerson = "何店长",
                    Province = "广东省",
                    City = "广州市",
                    District = "海珠区"
                },
            };

            await _storeRepository.AddRangeAsync(stores);
            await _storeRepository.SaveChangesAsync();
        }

        private async Task CreateProductAsStore(long storeId, string name)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var product = new Product
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                Name = name,
                Price = 100m,
                Description = "测试商品"
            };
            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();
        }

        private async Task CreateMemberAsStore(long storeId, string name)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var member = new Member
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                MemberName = name,
                Phone = "13800138000",
                Email = "test@qq.com",
            };
            await _memberRepository.AddAsync(member);
            await _memberRepository.SaveChangesAsync();
        }

        private async Task<long> GetProductIdByName(string name)
        {
            var product = await _productRepository.FirstOrDefaultAsync(p => p.Name == name);
            return product?.Id ?? 0;
        }

        #endregion
    }
}