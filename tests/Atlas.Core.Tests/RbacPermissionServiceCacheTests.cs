using Atlas.Core.Entities.Tenant;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Atlas.Core.Tests;

public sealed class RbacPermissionServiceCacheTests
{
    private const long TenantId = 1;
    private const long UserId = 10;
    private const string VersionKey = "rbac:permissions-version:1:10";
    private const string LegacyGlobalPermissionKey = "rbac:permissions:1:10:0";

    [Fact]
    public async Task InvalidateUserPermissionsAsync_BumpsUserPermissionVersion_WhenTenantWideRolesChange()
    {
        var writtenValues = new Dictionary<string, object?>();
        var writtenExpirations = new Dictionary<string, TimeSpan?>();
        var removedKeys = new List<string>();
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        cache.Setup(x => x.Remove(It.IsAny<string>()))
            .Callback<string>(removedKeys.Add)
            .Returns(true);
        cache.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, value, expiration) =>
            {
                writtenValues[key] = value;
                writtenExpirations[key] = expiration;
            });
        var service = CreateService(cache.Object);

        await service.InvalidateUserPermissionsAsync(TenantId, UserId, storeId: null);

        Assert.Contains(VersionKey, removedKeys);
        Assert.Contains(LegacyGlobalPermissionKey, removedKeys);
        Assert.True(writtenValues.TryGetValue(VersionKey, out var version));
        var versionText = Assert.IsType<string>(version);
        Assert.False(string.IsNullOrWhiteSpace(versionText));
        Assert.Equal(TimeSpan.FromHours(24), writtenExpirations[VersionKey]);
    }

    private static RbacPermissionService CreateService(ICacheService cache)
    {
        return new RbacPermissionService(
            Mock.Of<IRepository<User>>(),
            Mock.Of<IRepository<UserRole>>(),
            Mock.Of<IRepository<Role>>(),
            Mock.Of<IRepository<Permission>>(),
            Mock.Of<IRepository<RolePermission>>(),
            cache,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IIdGenerator>(),
            NullLogger<RbacPermissionService>.Instance);
    }
}
