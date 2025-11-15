// Atlas.Integration.Tests/TenantDatabase/StoreTests.cs
using Atlas.Data.Tenant;
using Atlas.Models.Tenant.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Integration.Tests.TenantDatabase
{
    public class StoreTests : TenantDatabaseTestBase
    {
        [Fact]
        public async Task Should_Have_Seeded_Stores()
        {
            // Arrange
            var context = GetService<AtlasTenantDbContext>();

            // Act
            var stores = await context.Stores.ToListAsync();

            // Assert
            stores.Should().HaveCount(5);
            stores.Should().Contain(s => s.Code == "HQ001");
        }

        [Fact]
        public async Task Should_Query_Headquarters()
        {
            // Arrange
            var context = GetService<AtlasTenantDbContext>();

            // Act
            var headquarters = await context.Stores
                .FirstOrDefaultAsync(s => s.Type == Core.Enums.StoreType.Headquarters);

            // Assert
            headquarters.Should().NotBeNull();
            headquarters!.Name.Should().Be("总部");
            headquarters.ParentStoreId.Should().BeNull();
        }

        [Fact]
        public async Task Should_Query_Direct_Operated_Stores()
        {
            // Arrange
            var context = GetService<AtlasTenantDbContext>();

            // Act
            var directStores = await context.Stores
                .Where(s => s.Type == Core.Enums.StoreType.DirectOperated)
                .ToListAsync();

            // Assert
            directStores.Should().HaveCount(2);
            directStores.Should().OnlyContain(s => s.ParentStoreId == 1);
        }

        [Fact]
        public async Task Should_Query_Franchised_Stores()
        {
            // Arrange
            var context = GetService<AtlasTenantDbContext>();

            // Act
            var franchisedStores = await context.Stores
                .Where(s => s.Type == Core.Enums.StoreType.Franchised)
                .ToListAsync();

            // Assert
            franchisedStores.Should().HaveCount(2);
        }

        [Fact]
        public async Task Should_Query_Store_Hierarchy()
        {
            // Arrange
            var context = GetService<AtlasTenantDbContext>();

            // Act - 查询总部及其子门店
            var headquarters = await context.Stores
                .Include(s => s.ChildStores)
                .FirstOrDefaultAsync(s => s.Type == Core.Enums.StoreType.Headquarters);

            // Assert
            headquarters.Should().NotBeNull();
            headquarters!.ChildStores.Should().HaveCount(4);
        }
    }
}