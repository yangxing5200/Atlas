// Atlas.Integration.Tests/GlobalDatabase/TenantTests.cs
using Atlas.Data.Global;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Integration.Tests.GlobalDatabase
{
    public class TenantTests : GlobalDatabaseTestBase
    {
        [Fact]
        public async Task Should_Have_Seeded_Tenants()
        {
            // Arrange
            var context = GetService<AtlasGlobalDbContext>();

            // Act
            var tenants = await context.Tenants.ToListAsync();

            // Assert
            tenants.Should().HaveCount(3);
            tenants.Should().Contain(t => t.Name == "演示公司");
        }

        [Fact]
        public async Task Should_Query_Tenant_By_Domain()
        {
            // Arrange
            var context = GetService<AtlasGlobalDbContext>();

            // Act
            var tenant = await context.Tenants
                .FirstOrDefaultAsync(t => t.Domain == "demo");

            // Assert
            tenant.Should().NotBeNull();
            tenant!.Name.Should().Be("演示公司");
            tenant.TenantType.Should().Be(Core.Enums.TenantType.Enterprise);
        }
    }
}