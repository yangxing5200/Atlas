// Atlas.Integration.Tests/TenantDatabase/StoreRepositoryTests.cs
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Impl;
using Atlas.Data.Tenant.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.TenantDatabase
{
    public class StoreRepositoryTests : TenantDatabaseTestBase
    {
        protected override void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            base.ConfigureServices(services, configuration);

            // 注册 Repository
            services.AddScoped<IStoreRepository, StoreRepository>();
        }

        [Fact]
        public async Task Should_Get_Store_By_Code()
        {
            // Arrange
            var repository = GetService<IStoreRepository>();

            // Act
            var store = await repository.FirstOrDefaultAsync(s => s.Code == "HQ001");

            // Assert
            store.Should().NotBeNull();
            store!.Name.Should().Be("总部");
        }

        [Fact]
        public async Task Should_Get_Active_Stores()
        {
            // Arrange
            var repository = GetService<IStoreRepository>();

            // Act
            var stores = await repository.FindAsync(s => s.Status == Core.Enums.StoreStatus.Active);

            // Assert
            stores.Should().HaveCount(5);
        }

        [Fact]
        public async Task Should_Support_Pagination()
        {
            // Arrange
            var repository = GetService<IStoreRepository>();

            // Act
            var (items, total) = await repository.GetPagedAsync(
                pageIndex: 1,
                pageSize: 2);

            // Assert
            total.Should().Be(5);
            items.Should().HaveCount(2);
        }
    }
}