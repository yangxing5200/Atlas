using System.Security.Claims;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Core.Tests;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task MissingPermission_DoesNotSucceedRequirement()
    {
        var handler = new PermissionAuthorizationHandler(
            new FakePermissionChecker(false),
            NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement(AtlasPermissionCodes.UsersManage);
        var context = new AuthorizationHandlerContext([requirement], CreateUser(1, 10, 100), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ExistingPermission_SucceedsRequirement()
    {
        var handler = new PermissionAuthorizationHandler(
            new FakePermissionChecker(true),
            NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement(AtlasPermissionCodes.UsersManage);
        var context = new AuthorizationHandlerContext([requirement], CreateUser(1, 10, 100), null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task CrossTenantPermission_IsNotVisible()
    {
        var checker = new TenantBoundPermissionChecker(allowedTenantId: 1);
        var handler = new PermissionAuthorizationHandler(
            checker,
            NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement(AtlasPermissionCodes.UsersManage);
        var context = new AuthorizationHandlerContext([requirement], CreateUser(2, 10, 100), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal(2, checker.LastContext?.TenantId);
    }

    private static ClaimsPrincipal CreateUser(long tenantId, long userId, long storeId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim("uid", userId.ToString()),
            new Claim("sid", storeId.ToString())
        ], "Test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FakePermissionChecker : IPermissionChecker
    {
        private readonly bool _allowed;

        public FakePermissionChecker(bool allowed)
        {
            _allowed = allowed;
        }

        public Task<bool> HasPermissionAsync(PermissionCheckContext context, CancellationToken ct = default)
        {
            return Task.FromResult(_allowed);
        }

        public Task InvalidateUserPermissionsAsync(long tenantId, long userId, long? storeId = null, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TenantBoundPermissionChecker : IPermissionChecker
    {
        private readonly long _allowedTenantId;

        public TenantBoundPermissionChecker(long allowedTenantId)
        {
            _allowedTenantId = allowedTenantId;
        }

        public PermissionCheckContext? LastContext { get; private set; }

        public Task<bool> HasPermissionAsync(PermissionCheckContext context, CancellationToken ct = default)
        {
            LastContext = context;
            return Task.FromResult(context.TenantId == _allowedTenantId);
        }

        public Task InvalidateUserPermissionsAsync(long tenantId, long userId, long? storeId = null, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
