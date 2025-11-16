using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Data.Common;
using Atlas.Data.Tenant.Repositories;
using Atlas.Integration.Tests.Infrastructure;
using Atlas.Models.Tenant.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Integration.Tests.Tenant
{
    /// <summary>
    /// Store 实体的 CRUD 集成测试
    /// 验证多租户数据库隔离性
    /// </summary>
    public class StoreRepositoryTests : TenantTestBase
    {
        // 记录测试创建的数据，用于清理
        private readonly List<(long TenantId, long StoreId)> _createdStores = new();

        #region Create Tests

        [Fact]
        public async Task Create_WithSnowflakeId_ShouldInsertIntoCorrectTenantDatabase()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("DEMO001", "演示门店001");

            // Act
            await repository.AddAsync(store);
            var savedCount = await repository.SaveChangesAsync();

            // Assert
            savedCount.Should().Be(1);
            store.Id.Should().BeGreaterThan(0, "雪花ID应自动生成");
            store.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // 验证数据确实在租户1的数据库中
            var dbName = await GetCurrentDatabaseNameAsync();
            dbName.Should().Contain("atlas", "应连接到租户数据库");

            // 记录用于清理
            _createdStores.Add((TestTenants.DemoCompany, store.Id));
        }

        [Fact]
        public async Task Create_WithManualId_ShouldUseSpecifiedId()
        {
            // Arrange
            SwitchToTenant(TestTenants.ChainEnterprise);
            var repository = GetService<IStoreRepository>();

            var manualId = new SnowflakeIdGenerator(1, 1).NextId();
            var store = CreateTestStore("CHAIN001", "连锁门店001");
            store.Id = manualId;

            // Act
            await repository.AddAsync(store);
            var savedCount = await repository.SaveChangesAsync();

            // Assert
            savedCount.Should().Be(1);
            store.Id.Should().Be(manualId, "应使用手动指定的ID");

            _createdStores.Add((TestTenants.ChainEnterprise, store.Id));
        }

        [Fact]
        public async Task Create_MultipleStoresInSameTenant_ShouldAllPersist()
        {
            // Arrange
            SwitchToTenant(TestTenants.PersonalTenant);
            var repository = GetService<IStoreRepository>();

            var stores = new[]
            {
                CreateTestStore("PERSONAL001", "个人门店001"),
                CreateTestStore("PERSONAL002", "个人门店002"),
                CreateTestStore("PERSONAL003", "个人门店003")
            };

            // Act
            await repository.AddRangeAsync(stores);
            var savedCount = await repository.SaveChangesAsync();

            // Assert
            savedCount.Should().Be(3);
            stores.Should().AllSatisfy(s => s.Id.Should().BeGreaterThan(0));
            stores.Select(s => s.Id).Should().OnlyHaveUniqueItems();

            foreach (var store in stores)
            {
                _createdStores.Add((TestTenants.PersonalTenant, store.Id));
            }
        }

        #endregion

        #region Read Tests

        [Fact]
        public async Task Read_ByIdInSameTenant_ShouldReturnStore()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("DEMO_READ", "读取测试门店");
            await repository.AddAsync(store);
            await repository.SaveChangesAsync();
            _createdStores.Add((TestTenants.DemoCompany, store.Id));

            // 重新创建 Repository（模拟新请求）
            repository = GetService<IStoreRepository>();

            // Act
            var retrieved = await repository.GetByIdAsync(store.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.Code.Should().Be("DEMO_READ");
            retrieved.Name.Should().Be("读取测试门店");
        }

        [Fact]
        public async Task Read_AllStoresInTenant_ShouldReturnOnlyTenantData()
        {
            // Arrange - 在不同租户创建数据
            SwitchToTenant(TestTenants.DemoCompany);
            var repo1 = GetService<IStoreRepository>();
            var store1 = CreateTestStore("ISOLATION_DEMO", "隔离测试-演示");
            await repo1.AddAsync(store1);
            await repo1.SaveChangesAsync();
            _createdStores.Add((TestTenants.DemoCompany, store1.Id));

            SwitchToTenant(TestTenants.ChainEnterprise);
            var repo2 = GetService<IStoreRepository>();
            var store2 = CreateTestStore("ISOLATION_CHAIN", "隔离测试-连锁");
            await repo2.AddAsync(store2);
            await repo2.SaveChangesAsync();
            _createdStores.Add((TestTenants.ChainEnterprise, store2.Id));

            // Act - 在租户1查询
            SwitchToTenant(TestTenants.DemoCompany);
            var queryRepo = GetService<IStoreRepository>();
            var allStores = await queryRepo.FindAsync(s => s.Code.StartsWith("ISOLATION_"));

            // Assert
            allStores.Should().HaveCount(1);
            allStores[0].Code.Should().Be("ISOLATION_DEMO");
            allStores.Should().NotContain(s => s.Code == "ISOLATION_CHAIN");
        }

        [Fact]
        public async Task Read_NonExistentId_ShouldReturnNull()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();
            var nonExistentId = new SnowflakeIdGenerator(1, 1).NextId(); ;

            // Act
            var result = await repository.GetByIdAsync(nonExistentId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Update Tests

        [Fact]
        public async Task Update_ExistingStore_ShouldPersistChanges()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("UPDATE_TEST", "更新前名称");
            await repository.AddAsync(store);
            await repository.SaveChangesAsync();
            _createdStores.Add((TestTenants.DemoCompany, store.Id));

            var originalVersion = store.Version;
            var originalUpdatedAt = store.UpdatedAt;

            // Act
            store.Name = "更新后名称";
            store.Address = "新地址";
            store.Version++;
            store.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(store);
            var savedCount = await repository.SaveChangesAsync();

            // Assert
            savedCount.Should().Be(1);

            // 重新查询验证
            var freshRepo = GetService<IStoreRepository>();
            var updated = await freshRepo.GetByIdAsync(store.Id);

            updated.Should().NotBeNull();
            updated!.Name.Should().Be("更新后名称");
            updated.Address.Should().Be("新地址");
            updated.Version.Should().Be(originalVersion + 1);
            updated.UpdatedAt.Should().NotBe(originalUpdatedAt);
        }

        [Fact]
        public async Task Update_ShouldSetUpdatedByAndUpdatedAt()
        {
            // Arrange
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.NormalUser);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("AUDIT_UPDATE", "审计更新测试");
            await repository.AddAsync(store);
            await repository.SaveChangesAsync();
            _createdStores.Add((TestTenants.ChainEnterprise, store.Id));

            // Act
            store.ContactPerson = "新联系人";
            store.UpdatedBy = TestUsers.NormalUser;
            store.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(store);
            await repository.SaveChangesAsync();

            // Assert
            var freshRepo = GetService<IStoreRepository>();
            var updated = await freshRepo.GetByIdAsync(store.Id);

            updated!.UpdatedBy.Should().Be(TestUsers.NormalUser);
            updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task Delete_ById_ShouldRemoveFromDatabase()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("DELETE_TEST", "删除测试");
            await repository.AddAsync(store);
            await repository.SaveChangesAsync();
            // 不记录到清理列表，因为会被删除

            // Act
            var deleted = await repository.DeleteAsync(store.Id);
            var savedCount = await repository.SaveChangesAsync();

            // Assert
            deleted.Should().BeTrue();
            savedCount.Should().Be(1);

            var freshRepo = GetService<IStoreRepository>();
            var result = await freshRepo.GetByIdAsync(store.Id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task Delete_NonExistentId_ShouldReturnFalse()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repository = GetService<IStoreRepository>();
            var nonExistentId = new SnowflakeIdGenerator(1, 1).NextId(); ;

            // Act
            var deleted = await repository.DeleteAsync(nonExistentId);

            // Assert
            deleted.Should().BeFalse();
        }

        [Fact]
        public async Task SoftDelete_ShouldSetDeletedFlags()
        {
            // Arrange
            SwitchToTenant(TestTenants.PersonalTenant, TestUsers.TestUser);
            var repository = GetService<IStoreRepository>();

            var store = CreateTestStore("SOFT_DELETE", "软删除测试");
            await repository.AddAsync(store);
            await repository.SaveChangesAsync();
            _createdStores.Add((TestTenants.PersonalTenant, store.Id));

            // Act - 模拟软删除
            store.IsDeleted = true;
            store.DeletedAt = DateTime.UtcNow;
            store.DeletedBy = TestUsers.TestUser;

            await repository.UpdateAsync(store);
            await repository.SaveChangesAsync();

            // Assert - 直接查询数据库验证（绕过软删除过滤）
            var context = await GetTenantDbContextAsync();
            var softDeleted = await context.Stores
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == store.Id);

            softDeleted.Should().NotBeNull();
            softDeleted!.IsDeleted.Should().BeTrue();
            softDeleted.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            softDeleted.DeletedBy.Should().Be(TestUsers.TestUser);
        }

        #endregion

        #region Tenant Isolation Tests

        [Fact]
        public async Task TenantIsolation_SameCodeInDifferentTenants_ShouldNotConflict()
        {
            // Arrange & Act - 三个租户创建同名门店
            var storeCode = "SAME_CODE";
            var storeName = "同名门店";

            SwitchToTenant(TestTenants.DemoCompany);
            var repo1 = GetService<IStoreRepository>();
            var store1 = CreateTestStore(storeCode, $"{storeName}-租户1");
            await repo1.AddAsync(store1);
            await repo1.SaveChangesAsync();
            _createdStores.Add((TestTenants.DemoCompany, store1.Id));

            SwitchToTenant(TestTenants.ChainEnterprise);
            var repo2 = GetService<IStoreRepository>();
            var store2 = CreateTestStore(storeCode, $"{storeName}-租户2");
            await repo2.AddAsync(store2);
            await repo2.SaveChangesAsync();
            _createdStores.Add((TestTenants.ChainEnterprise, store2.Id));

            SwitchToTenant(TestTenants.PersonalTenant);
            var repo3 = GetService<IStoreRepository>();
            var store3 = CreateTestStore(storeCode, $"{storeName}-租户3");
            await repo3.AddAsync(store3);
            await repo3.SaveChangesAsync();
            _createdStores.Add((TestTenants.PersonalTenant, store3.Id));

            // Assert - 每个租户只能看到自己的数据
            SwitchToTenant(TestTenants.DemoCompany);
            var query1 = GetService<IStoreRepository>();
            var result1 = await query1.FindAsync(s => s.Code == storeCode);
            result1.Should().HaveCount(1);
            result1[0].Name.Should().Contain("租户1");

            SwitchToTenant(TestTenants.ChainEnterprise);
            var query2 = GetService<IStoreRepository>();
            var result2 = await query2.FindAsync(s => s.Code == storeCode);
            result2.Should().HaveCount(1);
            result2[0].Name.Should().Contain("租户2");

            SwitchToTenant(TestTenants.PersonalTenant);
            var query3 = GetService<IStoreRepository>();
            var result3 = await query3.FindAsync(s => s.Code == storeCode);
            result3.Should().HaveCount(1);
            result3[0].Name.Should().Contain("租户3");
        }

        [Fact]
        public async Task TenantIsolation_CannotAccessOtherTenantDataById()
        {
            // Arrange - 在租户1创建数据
            SwitchToTenant(TestTenants.DemoCompany);
            var repo1 = GetService<IStoreRepository>();
            var store = CreateTestStore("CROSS_TENANT", "跨租户访问测试");
            await repo1.AddAsync(store);
            await repo1.SaveChangesAsync();
            _createdStores.Add((TestTenants.DemoCompany, store.Id));

            var storeId = store.Id;

            // Act - 尝试从租户2访问
            SwitchToTenant(TestTenants.ChainEnterprise);
            var repo2 = GetService<IStoreRepository>();
            var result = await repo2.GetByIdAsync(storeId);

            // Assert - 应该无法访问（因为连接的是不同的数据库）
            result.Should().BeNull("租户2无法访问租户1的数据");
        }

        [Fact]
        public async Task TenantIsolation_CountShouldBePerTenant()
        {
            // Arrange
            SwitchToTenant(TestTenants.DemoCompany);
            var repo1 = GetService<IStoreRepository>();
            var initialCount1 = await repo1.CountAsync(s => s.Code.StartsWith("COUNT_TEST_"));

            SwitchToTenant(TestTenants.ChainEnterprise);
            var repo2 = GetService<IStoreRepository>();
            var initialCount2 = await repo2.CountAsync(s => s.Code.StartsWith("COUNT_TEST_"));

            // Act - 在租户1添加3条，租户2添加2条
            SwitchToTenant(TestTenants.DemoCompany);
            var addRepo1 = GetService<IStoreRepository>();
            for (int i = 1; i <= 3; i++)
            {
                var store = CreateTestStore($"COUNT_TEST_{i}", $"计数测试{i}");
                await addRepo1.AddAsync(store);
                _createdStores.Add((TestTenants.DemoCompany, store.Id));
            }
            await addRepo1.SaveChangesAsync();

            SwitchToTenant(TestTenants.ChainEnterprise);
            var addRepo2 = GetService<IStoreRepository>();
            for (int i = 1; i <= 2; i++)
            {
                var store = CreateTestStore($"COUNT_TEST_{i}", $"计数测试{i}");
                await addRepo2.AddAsync(store);
                _createdStores.Add((TestTenants.ChainEnterprise, store.Id));
            }
            await addRepo2.SaveChangesAsync();

            // Assert
            SwitchToTenant(TestTenants.DemoCompany);
            var countRepo1 = GetService<IStoreRepository>();
            var finalCount1 = await countRepo1.CountAsync(s => s.Code.StartsWith("COUNT_TEST_"));
            finalCount1.Should().Be(initialCount1 + 3);

            SwitchToTenant(TestTenants.ChainEnterprise);
            var countRepo2 = GetService<IStoreRepository>();
            var finalCount2 = await countRepo2.CountAsync(s => s.Code.StartsWith("COUNT_TEST_"));
            finalCount2.Should().Be(initialCount2 + 2);
        }

        [Fact]
        public async Task AddRangeStore()
        {
            SwitchToTenant(TestTenants.ChainEnterprise);
            var _tenantId = TestTenants.DemoCompany;
            var stores = new[]
        {
                // 1. 总部
                new Store
                {
                    Code = "HQ001",
                    Name = "总部",
                    Type = StoreType.Headquarters,
                    ParentStoreId = null,
                    Province = "上海",
                    City = "上海",
                    District = "浦东新区",
                    Address = "张江高科技园区 999 号",
                    ContactPerson = "总部经理",
                    ContactPhone = "021-12345678",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                
                // 2. 直营门店
                new Store
                {
                    Code = "ZY001",
                    Name = "浦东直营店",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "浦东新区",
                    Address = "世纪大道 888 号",
                    ContactPerson = "李经理",
                    ContactPhone = "021-88888888",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Store
                {
                    TenantId = _tenantId,
                    Code = "ZY002",
                    Name = "徐汇直营店",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "徐汇区",
                    Address = "徐家汇 666 号",
                    ContactPerson = "王经理",
                    ContactPhone = "021-66666666",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                
                // 3. 加盟门店
                new Store
                {
                    TenantId = _tenantId,
                    Code = "JM001",
                    Name = "虹口加盟店",
                    Type = StoreType.Franchised,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "虹口区",
                    Address = "四川北路 555 号",
                    ContactPerson = "张老板",
                    ContactPhone = "021-55555555",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Store
                {
                    TenantId = _tenantId,
                    Code = "JM002",
                    Name = "杨浦加盟店",
                    Type = StoreType.Franchised,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "杨浦区",
                    Address = "五角场 333 号",
                    ContactPerson = "赵老板",
                    ContactPhone = "021-33333333",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                }
            };

            var repo1 = GetService<IStoreRepository>();
            await repo1.AddRangeAsync(stores);
            await repo1.SaveChangesAsync();
        }
        #endregion

        #region Helper Methods

        private Store CreateTestStore(string code, string name)
        {
            return new Store
            {
                TenantId = FakeIdentity.TenantId ?? 0,
                Code = code,
                Name = name,
                Type = StoreType.DirectOperated,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "测试地址",
                ContactPhone = "13800138000",
                ContactPerson = "测试联系人",
                Province = "广东省",
                City = "深圳市",
                District = "南山区",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = FakeIdentity.UserId,
                Version = 1,
                IsDeleted = false
            };
        }

        /// <summary>
        /// 清理测试数据（存在但不执行）
        /// </summary>
        private async Task CleanupTestDataAsync()
        {
            foreach (var (tenantId, storeId) in _createdStores)
            {
                try
                {
                    SwitchToTenant(tenantId);
                    var repository = GetService<IStoreRepository>();
                    await repository.DeleteAsync(storeId);
                    await repository.SaveChangesAsync();
                }
                catch
                {
                    // 忽略清理错误
                }
            }

            _createdStores.Clear();
        }

        protected override async Task OnDisposeAsync()
        {
            //await CleanupTestDataAsync();

            await base.OnDisposeAsync();
        }

        #endregion
    }
}