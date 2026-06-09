using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class EnterpriseSecurityModelTests
{
    [Fact]
    public void TenantModel_ConfiguresRbacUniqueIndexesWithTenantId()
    {
        using var dbContext = CreateTenantDbContext();

        AssertHasUniqueIndex<Role>(dbContext, nameof(Role.TenantId), nameof(Role.Code));
        AssertHasUniqueIndex<Permission>(dbContext, nameof(Permission.TenantId), nameof(Permission.Code));
        AssertHasUniqueIndex<RolePermission>(dbContext, nameof(RolePermission.TenantId), nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId));
        AssertHasUniqueIndex<UserRole>(dbContext, nameof(UserRole.TenantId), nameof(UserRole.UserId), nameof(UserRole.RoleId), nameof(UserRole.StoreId));
    }

    [Fact]
    public void TenantModel_ConfiguresRefreshTokenAndAuditTenantIndexes()
    {
        using var dbContext = CreateTenantDbContext();

        AssertHasUniqueIndex<RefreshToken>(dbContext, nameof(RefreshToken.TenantId), nameof(RefreshToken.TokenHash));
        AssertHasIndex<AuditEvent>(dbContext, nameof(AuditEvent.TenantId), nameof(AuditEvent.CreatedAt));
        AssertHasIndex<AuditEvent>(dbContext, nameof(AuditEvent.TenantId), nameof(AuditEvent.UserId), nameof(AuditEvent.CreatedAt));
    }

    [Fact]
    public void TenantModel_LoadsConfigurationsFromTenantDataAssembly()
    {
        using var dbContext = CreateTenantDbContext();

        Assert.Equal(typeof(AtlasTenantDbContext).Assembly, typeof(UserConfiguration).Assembly);
        AssertHasIndex<User>(dbContext, nameof(User.TenantId), nameof(User.UserName));
    }

    [Fact]
    public void TenantModel_PreservesConfiguredForeignKeyDeleteBehavior()
    {
        using var dbContext = CreateTenantDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(UserStore));

        Assert.NotNull(entityType);
        var storeForeignKey = entityType!.GetForeignKeys()
            .Single(foreignKey => foreignKey.Properties
                .Select(property => property.Name)
                .SequenceEqual(new[] { nameof(UserStore.StoreId) }));

        Assert.Equal(DeleteBehavior.Restrict, storeForeignKey.DeleteBehavior);
    }

    private static AtlasTenantDbContext CreateTenantDbContext()
    {
        var options = new DbContextOptionsBuilder<AtlasTenantDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        return new AtlasTenantDbContext(options);
    }

    private static void AssertHasUniqueIndex<TEntity>(
        DbContext dbContext,
        params string[] propertyNames)
        where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entityType);
        Assert.Contains(
            entityType!.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(x => x.Name).SequenceEqual(propertyNames));
    }

    private static void AssertHasIndex<TEntity>(
        DbContext dbContext,
        params string[] propertyNames)
        where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entityType);
        Assert.Contains(
            entityType!.GetIndexes(),
            index => index.Properties.Select(x => x.Name).SequenceEqual(propertyNames));
    }
}
