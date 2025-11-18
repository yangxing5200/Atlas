using Atlas.Core.Enums;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.Tenant.Entities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.Repositories
{
    /// <summary>
    /// 门店独享数据(IStoreOnlyEntity)隔离测试
    /// 测试订单、库存、收银记录等独享数据的完全隔离
    /// </summary>
    public class StoreOnlyEntityIsolationTests : TenantTestBase
    {
        private IStoreRepository _storeRepository = null!;
        private IOrderRepository _orderRepository = null!;
        private IInventoryRepository _inventoryRepository = null!;
        private ICashierRecordRepository _cashierRepository = null!;

        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
            _storeRepository = GetService<IStoreRepository>();
            _orderRepository = GetService<IOrderRepository>();
            _inventoryRepository = GetService<IInventoryRepository>();
            _cashierRepository = GetService<ICashierRecordRepository>();
            var factory = GetService<ITenantDbContextFactory>();
            await factory.CreateReadonlyDbContextAsync();
            await SetupCompleteStoreHierarchy();
        }

        #region 测试场景1：订单完全隔离

        [Fact]
        public async Task Order_EachStore_CompletelyIsolated()
        {
            // Arrange - 不同门店创建订单
            await CreateOrderAsStore(1000, "ORDER-HQ-001");
            await CreateOrderAsStore(1, "ORDER-A-001");
            await CreateOrderAsStore(2, "ORDER-B-001");
            await CreateOrderAsStore(100, "ORDER-X-001");
            await CreateOrderAsStore(103, "ORDER-F-001");

            // Act & Assert - 平台总部只能看到自己的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1000);
            var ordersHQ = await _orderRepository.GetAllAsync();
            ordersHQ.Should().HaveCount(1);
            ordersHQ.First().OrderNo.Should().Be("ORDER-HQ-001");

            // Act & Assert - 平台直营A只能看到自己的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var ordersA = await _orderRepository.GetAllAsync();
            ordersA.Should().HaveCount(1);
            ordersA.First().OrderNo.Should().Be("ORDER-A-001");

            // Act & Assert - 加盟商X总部只能看到自己的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var ordersX = await _orderRepository.GetAllAsync();
            ordersX.Should().HaveCount(1);
            ordersX.First().OrderNo.Should().Be("ORDER-X-001");
        }

        [Fact]
        public async Task Order_DirectStores_CannotSeeEachOther()
        {
            // Arrange
            await CreateOrderAsStore(1, "ORDER-A-001");
            await CreateOrderAsStore(2, "ORDER-B-001");

            // Act - 门店A查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var ordersA = await _orderRepository.GetAllAsync();

            // Assert
            ordersA.Should().HaveCount(1);
            ordersA.First().OrderNo.Should().Be("ORDER-A-001");

            // Act - 门店B查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var ordersB = await _orderRepository.GetAllAsync();

            // Assert
            ordersB.Should().HaveCount(1);
            ordersB.First().OrderNo.Should().Be("ORDER-B-001");
        }

        [Fact]
        public async Task Order_Headquarters_CannotSee_DirectStoreOrders()
        {
            // Arrange
            await CreateOrderAsStore(1000, "ORDER-HQ-001");
            await CreateOrderAsStore(1, "ORDER-A-001");

            // Act - 平台总部查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1000);
            var orders = await _orderRepository.GetAllAsync();

            // Assert - 只能看到总部自己的订单
            orders.Should().HaveCount(1);
            orders.First().OrderNo.Should().Be("ORDER-HQ-001");
        }

        [Fact]
        public async Task Order_FranchiseHeadquarters_CannotSee_FranchiseStoreOrders()
        {
            // Arrange
            await CreateOrderAsStore(100, "ORDER-X-HQ-001");
            await CreateOrderAsStore(101, "ORDER-D-001");
            await CreateOrderAsStore(103, "ORDER-F-001");

            // Act - 加盟商X总部查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            var orders = await _orderRepository.GetAllAsync();

            // Assert - 只能看到总部自己的订单
            orders.Should().HaveCount(1);
            orders.First().OrderNo.Should().Be("ORDER-X-HQ-001");
        }

        #endregion

        #region 测试场景2：库存完全隔离

        [Fact]
        public async Task Inventory_EachStore_CompletelyIsolated()
        {
            // Arrange - 不同门店设置库存
            await CreateInventoryAsStore(1, productId: 1001, quantity: 100);
            await CreateInventoryAsStore(2, productId: 1001, quantity: 200);
            await CreateInventoryAsStore(101, productId: 1001, quantity: 50);

            // Act & Assert - 门店A只能看到自己的库存
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var inventoryA = await _inventoryRepository.GetAllAsync();
            inventoryA.Should().HaveCount(1);
            inventoryA.First().Quantity.Should().Be(100);

            // Act & Assert - 门店B只能看到自己的库存
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var inventoryB = await _inventoryRepository.GetAllAsync();
            inventoryB.Should().HaveCount(1);
            inventoryB.First().Quantity.Should().Be(200);

            // Act & Assert - 门店D只能看到自己的库存
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 101);
            var inventoryD = await _inventoryRepository.GetAllAsync();
            inventoryD.Should().HaveCount(1);
            inventoryD.First().Quantity.Should().Be(50);
        }

        [Fact]
        public async Task Inventory_SameProduct_IsolatedByStore()
        {
            // Arrange - 多个门店对同一商品设置库存
            const long productId = 2001;
            await CreateInventoryAsStore(1, productId, 100);
            await CreateInventoryAsStore(2, productId, 200);
            await CreateInventoryAsStore(3, productId, 300);

            // Act & Assert - 门店A
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var inventoryA = await _inventoryRepository.FirstOrDefaultAsync(
                i => i.ProductId == productId);
            inventoryA.Should().NotBeNull();
            inventoryA!.Quantity.Should().Be(100);
            inventoryA.StoreId.Should().Be(1);

            // Act & Assert - 门店B
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var inventoryB = await _inventoryRepository.FirstOrDefaultAsync(
                i => i.ProductId == productId);
            inventoryB.Should().NotBeNull();
            inventoryB!.Quantity.Should().Be(200);
            inventoryB.StoreId.Should().Be(2);
        }

        #endregion

        #region 测试场景3：收银记录完全隔离

        [Fact]
        public async Task CashierRecord_EachStore_CompletelyIsolated()
        {
            // Arrange
            await CreateCashierRecordAsStore(1, amount: 100m);
            await CreateCashierRecordAsStore(2, amount: 200m);
            await CreateCashierRecordAsStore(3, amount: 300m);

            // Act & Assert - 门店A
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var recordsA = await _cashierRepository.GetAllAsync();
            recordsA.Should().HaveCount(1);
            recordsA.First().Amount.Should().Be(100m);

            // Act & Assert - 门店B
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var recordsB = await _cashierRepository.GetAllAsync();
            recordsB.Should().HaveCount(1);
            recordsB.First().Amount.Should().Be(200m);
        }

        [Fact]
        public async Task CashierRecord_Statistics_IsolatedByStore()
        {
            // Arrange - 门店A的多笔交易
            await CreateCashierRecordAsStore(1, 100m);
            await CreateCashierRecordAsStore(1, 200m);
            await CreateCashierRecordAsStore(1, 300m);

            // 门店B的交易
            await CreateCashierRecordAsStore(2, 500m);

            // Act - 门店A统计
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var totalA = await _cashierRepository.SumAsync(c => c.Amount);
            var countA = await _cashierRepository.CountAsync();

            // Assert
            totalA.Should().Be(600m);
            countA.Should().Be(3);

            // Act - 门店B统计
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var totalB = await _cashierRepository.SumAsync(c => c.Amount);
            var countB = await _cashierRepository.CountAsync();

            // Assert
            totalB.Should().Be(500m);
            countB.Should().Be(1);
        }

        #endregion

        #region 测试场景4：CRUD操作权限

        [Fact]
        public async Task StoreOnly_CannotQuery_OtherStoreData()
        {
            // Arrange
            await CreateOrderAsStore(1, "ORDER-A-001");
            var orderIdA = await GetOrderIdByOrderNo("ORDER-A-001");

            // Act - 用门店B尝试查询门店A的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var order = await _orderRepository.GetByIdAsync(orderIdA);

            // Assert - 应该查询不到
            order.Should().BeNull();
        }

        [Fact]
        public async Task StoreOnly_CannotUpdate_OtherStoreData()
        {
            // Arrange
            await CreateOrderAsStore(1, "ORDER-A-001");
            var orderIdA = await GetOrderIdByOrderNo("ORDER-A-001");

            // Act - 用门店B尝试获取门店A的订单进行更新
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var order = await _orderRepository.GetForUpdateAsync(orderIdA);

            // Assert
            order.Should().BeNull();
        }

        [Fact]
        public async Task StoreOnly_CannotDelete_OtherStoreData()
        {
            // Arrange
            await CreateOrderAsStore(1, "ORDER-A-001");
            var orderIdA = await GetOrderIdByOrderNo("ORDER-A-001");

            // Act - 用门店B尝试删除门店A的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var deleted = await _orderRepository.DeleteAsync(orderIdA);

            // Assert
            deleted.Should().BeFalse();

            // 验证订单仍然存在
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var order = await _orderRepository.GetByIdAsync(orderIdA);
            order.Should().NotBeNull();
        }

        [Fact]
        public async Task StoreOnly_CanUpdate_OwnData()
        {
            // Arrange
            await CreateOrderAsStore(1, "ORDER-A-001");
            var orderId = await GetOrderIdByOrderNo("ORDER-A-001");

            // Act - 门店A更新自己的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var order = await _orderRepository.GetForUpdateAsync(orderId);
            order.Should().NotBeNull();

            order!.TotalAmount = 999m;
            order.Status = OrderStatus.Comfirm;
            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            // Assert
            var updated = await _orderRepository.GetByIdAsync(orderId);
            updated.Should().NotBeNull();
            updated!.TotalAmount.Should().Be(999m);
            updated.Status.Should().Be(OrderStatus.Comfirm);
        }

        #endregion

        #region 测试场景5：不同类型门店的独享隔离

        [Fact]
        public async Task Headquarters_DirectStore_Franchised_AllIsolated()
        {
            // Arrange - 不同类型门店创建订单
            await CreateOrderAsStore(0, "ORDER-HQ-001");        // 平台总部
            await CreateOrderAsStore(1, "ORDER-A-001");         // 平台直营
            await CreateOrderAsStore(100, "ORDER-X-HQ-001");    // 加盟商总部
            await CreateOrderAsStore(101, "ORDER-D-001");       // 加盟商直营
            await CreateOrderAsStore(103, "ORDER-F-001");       // 加盟门店

            // Act & Assert - 每个门店只能看到自己的订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 0);
            (await _orderRepository.CountAsync()).Should().Be(1);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            (await _orderRepository.CountAsync()).Should().Be(1);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 100);
            (await _orderRepository.CountAsync()).Should().Be(1);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 101);
            (await _orderRepository.CountAsync()).Should().Be(1);

            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 103);
            (await _orderRepository.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task SiblingStores_CannotSee_EachOtherOrders()
        {
            // Arrange - 同级直营门店创建订单
            await CreateOrderAsStore(1, "ORDER-A-001");
            await CreateOrderAsStore(2, "ORDER-B-001");
            await CreateOrderAsStore(3, "ORDER-C-001");

            // Act & Assert - 虽然是同级共享门店,但订单仍然独享
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var orders = await _orderRepository.GetAllAsync();
            orders.Should().HaveCount(1);
            orders.First().OrderNo.Should().Be("ORDER-A-001");
        }

        #endregion

        #region 测试场景6：批量操作隔离

        [Fact]
        public async Task BatchCreate_OnlyVisibleToOwnStore()
        {
            // Arrange - 门店A批量创建订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var orders = Enumerable.Range(1, 10).Select(i => new Order
            {
                StoreId = 1,
                TenantId = TestTenants.ChainEnterprise,
                OrderNo = $"ORDER-A-{i:D3}",
                TotalAmount = i * 100m,
                Status = OrderStatus.Pending
            });

            await _orderRepository.AddRangeAsync(orders);
            await _orderRepository.SaveChangesAsync();

            // Act & Assert - 门店A可以看到所有订单
            var ordersA = await _orderRepository.GetAllAsync();
            ordersA.Should().HaveCount(10);

            // Act & Assert - 门店B看不到任何订单
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var ordersB = await _orderRepository.GetAllAsync();
            ordersB.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPaged_OnlyReturnOwnStoreData()
        {
            // Arrange - 多个门店创建数据
            await CreateMultipleOrdersAsStore(1, 15);
            await CreateMultipleOrdersAsStore(2, 20);

            // Act - 门店A分页查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 1);
            var (items, total) = await _orderRepository.GetPagedAsync(1, 10);

            // Assert
            items.Should().HaveCount(10);
            total.Should().Be(15);
            items.Should().AllSatisfy(o => o.StoreId.Should().Be(1));

            // Act - 门店B分页查询
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, 2);
            var (itemsB, totalB) = await _orderRepository.GetPagedAsync(1, 10);

            // Assert
            itemsB.Should().HaveCount(10);
            totalB.Should().Be(20);
            itemsB.Should().AllSatisfy(o => o.StoreId.Should().Be(2));
        }

        #endregion

        #region 辅助方法

        private async Task SetupCompleteStoreHierarchy()
        {

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

        private async Task CreateOrderAsStore(long storeId, string orderNo)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var order = new Order
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                OrderNo = orderNo,
                TotalAmount = 100m,
                Status = OrderStatus.Pending
            };
            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();
        }

        private async Task CreateMultipleOrdersAsStore(long storeId, int count)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var orders = Enumerable.Range(1, count).Select(i => new Order
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                OrderNo = $"ORDER-{storeId}-{i:D3}",
                TotalAmount = i * 100m,
                Status = OrderStatus.Pending
            });
            await _orderRepository.AddRangeAsync(orders);
            await _orderRepository.SaveChangesAsync();
        }

        private async Task CreateInventoryAsStore(long storeId, long productId, int quantity)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var inventory = new Inventory
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                ProductId = productId,
                Quantity = quantity,
                SafetyStock = 10
            };
            await _inventoryRepository.AddAsync(inventory);
            await _inventoryRepository.SaveChangesAsync();
        }

        private async Task CreateCashierRecordAsStore(long storeId, decimal amount)
        {
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.AdminUser, storeId);
            var record = new CashierRecord
            {
                StoreId = storeId,
                TenantId = TestTenants.ChainEnterprise,
                OrderId = 1,
                Amount = amount,
                PaymentMethod = PaymentMethod.Cash,
                PaidAt = DateTime.Now
            };
            await _cashierRepository.AddAsync(record);
            await _cashierRepository.SaveChangesAsync();
        }

        private async Task<long> GetOrderIdByOrderNo(string orderNo)
        {
            var order = await _orderRepository.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            return order?.Id ?? 0;
        }

        #endregion
    }
}