using Atlas.Core.IdGenerators;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Tests.Mocks;
using Atlas.Data.Tests.TestEntities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class AuditInterceptorTests
{
    [Fact]
    public async Task AuditInterceptor_UsesLocalTimeForAuditFields()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var identity = new MockCurrentUserService(userId: 10001, tenantId: 1);
        var interceptor = new AuditInterceptor(new SnowflakeIdGenerator(1, 1), identity);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options, identity);
        await db.Database.EnsureCreatedAsync();

        var beforeCreate = DateTime.Now.AddSeconds(-1);
        var tenant = new TestTenant
        {
            Name = "Local audit time tenant",
            Code = "LOCAL_AUDIT",
            Status = 1
        };

        db.TestTenants.Add(tenant);
        await db.SaveChangesAsync();
        var afterCreate = DateTime.Now.AddSeconds(1);

        Assert.Equal(DateTimeKind.Local, tenant.CreatedAt.Kind);
        Assert.InRange(tenant.CreatedAt, beforeCreate, afterCreate);

        var beforeUpdate = DateTime.Now.AddSeconds(-1);
        tenant.Remark = "Updated";
        await db.SaveChangesAsync();
        var afterUpdate = DateTime.Now.AddSeconds(1);

        Assert.NotNull(tenant.UpdatedAt);
        Assert.Equal(DateTimeKind.Local, tenant.UpdatedAt.Value.Kind);
        Assert.InRange(tenant.UpdatedAt.Value, beforeUpdate, afterUpdate);
    }
}
