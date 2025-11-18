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
    /// 门店基础CRUD测试
    /// </summary>
    public class StoreRepositoryTests : TenantTestBase
    {
        private IStoreRepository _storeRepository = null!;
        private long _platformHQId;
        private long _platformStoreAId;
        private long _franchiseHQId;

        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
            _storeRepository = GetService<IStoreRepository>();
            var factory = GetService<ITenantDbContextFactory>();
            await factory.CreateReadonlyDbContextAsync();
        }

        [Fact]
        public async Task Create_PlatformHeadquarters_Success()
        {
            // Arrange
            var headquarters = new Store
            {
                Id = 0,
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Platform",
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
            };

            // Act
            await _storeRepository.AddAsync(headquarters);
            await _storeRepository.SaveChangesAsync();

            // Assert
            var saved = await _storeRepository.GetByIdAsync(headquarters.Id);
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("平台总部");
            saved.Type.Should().Be(StoreType.Headquarters);
            saved.ParentStoreId.Should().BeNull();
        }

        [Fact]
        public async Task Create_DirectOperatedStore_UnderPlatform_Success()
        {
            // Arrange - 先创建平台总部
            var headquarters = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Platform",
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
            };
            await _storeRepository.AddAsync(headquarters);
            await _storeRepository.SaveChangesAsync();

            var directStore = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "STORE-A",
                Name = "平台直营门店A",
                Type = StoreType.DirectOperated,
                ParentStoreId = headquarters.Id,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市浦东新区陆家嘴环路1000号",
                ContactPhone = "021-23456789",
                ContactPerson = "李经理",
                Province = "上海市",
                City = "上海市",
                District = "浦东新区"
            };

            // Act
            await _storeRepository.AddAsync(directStore);
            await _storeRepository.SaveChangesAsync();

            // Assert
            var saved = await _storeRepository.GetByIdAsync(directStore.Id);
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("平台直营门店A");
            saved.Type.Should().Be(StoreType.DirectOperated);
            saved.ParentStoreId.Should().Be(headquarters.Id);
        }

        [Fact]
        public async Task Create_FranchiseHeadquarters_Success()
        {
            // Arrange - 先创建平台总部
            var headquarters = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Platform",
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
            };
            await _storeRepository.AddAsync(headquarters);
            await _storeRepository.SaveChangesAsync();

            var franchiseHQ = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Franchise-X",
                Name = "加盟商X总部",
                Type = StoreType.FranchiseHeadquarters,
                ParentStoreId = headquarters.Id,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市徐汇区漕溪北路88号",
                ContactPhone = "021-34567890",
                ContactPerson = "王总",
                Province = "上海市",
                City = "上海市",
                District = "徐汇区"
            };

            // Act
            await _storeRepository.AddAsync(franchiseHQ);
            await _storeRepository.SaveChangesAsync();

            // Assert
            var saved = await _storeRepository.GetByIdAsync(franchiseHQ.Id);
            saved.Should().NotBeNull();
            saved!.Type.Should().Be(StoreType.FranchiseHeadquarters);
            saved.ParentStoreId.Should().Be(headquarters.Id);
        }

        [Fact]
        public async Task Create_FranchisedStore_UnderFranchiseHQ_Success()
        {
            // Arrange - 创建平台总部和加盟商总部
            var headquarters = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Platform",
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
            };
            await _storeRepository.AddAsync(headquarters);
            await _storeRepository.SaveChangesAsync();

            var franchiseHQ = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "HQ-Franchise-X",
                Name = "加盟商X总部",
                Type = StoreType.FranchiseHeadquarters,
                ParentStoreId = headquarters.Id,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市徐汇区漕溪北路88号",
                ContactPhone = "021-34567890",
                ContactPerson = "王总",
                Province = "上海市",
                City = "上海市",
                District = "徐汇区"
            };
            await _storeRepository.AddAsync(franchiseHQ);
            await _storeRepository.SaveChangesAsync();

            var franchisedStore = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "STORE-F",
                Name = "加盟商X加盟门店F",
                Type = StoreType.Franchised,
                ParentStoreId = franchiseHQ.Id,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市长宁区虹桥路1号",
                ContactPhone = "021-45678901",
                ContactPerson = "赵店长",
                Province = "上海市",
                City = "上海市",
                District = "长宁区"
            };

            // Act
            await _storeRepository.AddAsync(franchisedStore);
            await _storeRepository.SaveChangesAsync();

            // Assert
            var saved = await _storeRepository.GetByIdAsync(franchisedStore.Id);
            saved.Should().NotBeNull();
            saved!.Type.Should().Be(StoreType.Franchised);
            saved.ParentStoreId.Should().Be(franchiseHQ.Id);
        }

        [Fact]
        public async Task Update_Store_Success()
        {
            // Arrange
            var store = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "STORE-A",
                Name = "原始名称",
                Type = StoreType.DirectOperated,
                ParentStoreId = null,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市静安区南京西路500号",
                ContactPhone = "021-56789012",
                ContactPerson = "刘经理",
                Province = "上海市",
                City = "上海市",
                District = "静安区"
            };
            await _storeRepository.AddAsync(store);
            await _storeRepository.SaveChangesAsync();

            // Act
            var toUpdate = await _storeRepository.GetForUpdateAsync(store.Id);
            toUpdate!.Name = "更新后名称";
            toUpdate.Address = "上海市静安区南京西路800号";
            await _storeRepository.UpdateAsync(toUpdate);
            await _storeRepository.SaveChangesAsync();

            // Assert
            var updated = await _storeRepository.GetByIdAsync(store.Id);
            updated.Should().NotBeNull();
            updated!.Name.Should().Be("更新后名称");
            updated.Address.Should().Be("上海市静安区南京西路800号");
        }

        [Fact]
        public async Task Delete_Store_Success()
        {
            // Arrange
            var store = new Store
            {
                TenantId = TestTenants.ChainEnterprise,
                Code = "STORE-A",
                Name = "待删除门店",
                Type = StoreType.DirectOperated,
                ParentStoreId = null,
                IsActive = true,
                Status = StoreStatus.Active,
                Address = "上海市虹口区四川北路200号",
                ContactPhone = "021-67890123",
                ContactPerson = "孙经理",
                Province = "上海市",
                City = "上海市",
                District = "虹口区"
            };
            await _storeRepository.AddAsync(store);
            await _storeRepository.SaveChangesAsync();

            // Act
            var deleted = await _storeRepository.DeleteAsync(store.Id);
            await _storeRepository.SaveChangesAsync();

            // Assert
            deleted.Should().BeTrue();
            var found = await _storeRepository.GetByIdAsync(store.Id);
            found.Should().BeNull();
        }

        //[Fact]
        //public async Task GetChildDirectStores_ReturnOnlyDirectOperated()
        //{
        //    // Arrange - 创建门店层级
        //    await SetupCompleteStoreHierarchy();

        //    // Act
        //    var childStores = await _storeRepository.GetChildDirectStoresAsync(_franchiseHQId);

        //    // Assert
        //    childStores.Should().HaveCount(2);
        //    childStores.Should().AllSatisfy(s => s.Type.Should().Be(StoreType.DirectOperated));
        //    childStores.Should().AllSatisfy(s => s.ParentStoreId.Should().Be(_franchiseHQId));
        //}

        //[Fact]
        //public async Task GetSiblingDirectStores_ReturnAllSameParent()
        //{
        //    // Arrange
        //    await SetupCompleteStoreHierarchy();

        //    // Act - 使用第一个平台直营门店查找其兄弟门店
        //    var siblings = await _storeRepository.GetSiblingDirectStoresAsync(_platformStoreAId);

        //    // Assert
        //    siblings.Should().HaveCount(2); // 应该返回另外两个兄弟门店（不包括自己）
        //    siblings.Should().AllSatisfy(s => s.Type.Should().Be(StoreType.DirectOperated));
        //    siblings.Should().AllSatisfy(s => s.ParentStoreId.Should().Be(_platformHQId));
        //    siblings.Should().NotContain(s => s.Id == _platformStoreAId); // 不包括自己
        //}

    }
}