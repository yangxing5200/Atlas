using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services;
using Atlas.Services.Abstractions;
using AutoMapper;
using Moq;

namespace Atlas.Services.Tests;

public class UserServiceFacadeTests
{
    [Fact]
    public async Task LoginAsync_DelegatesToAuthService()
    {
        var service = CreateService(out var auth, out _, out _, out _);
        var request = new LoginRequest();
        var expected = new LoginResponse { Success = true };
        auth.Setup(x => x.LoginAsync(request, "127.0.0.1", "agent")).ReturnsAsync(expected);

        var actual = await service.LoginAsync(request, "127.0.0.1", "agent");

        Assert.Same(expected, actual);
        auth.Verify(x => x.LoginAsync(request, "127.0.0.1", "agent"), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_DelegatesToAuthService()
    {
        var service = CreateService(out var auth, out _, out _, out _);
        var request = new RefreshTokenRequest();
        var expected = new LoginResponse { Success = true };
        auth.Setup(x => x.RefreshTokenAsync(request, "127.0.0.1", null)).ReturnsAsync(expected);

        var actual = await service.RefreshTokenAsync(request, "127.0.0.1", null);

        Assert.Same(expected, actual);
        auth.Verify(x => x.RefreshTokenAsync(request, "127.0.0.1", null), Times.Once);
    }

    [Fact]
    public async Task SwitchStoreAsync_DelegatesToAuthService()
    {
        var service = CreateService(out var auth, out _, out _, out _);
        var request = new SwitchStoreRequest { StoreId = 12 };
        var expected = new SwitchStoreResponse { Success = true };
        auth.Setup(x => x.SwitchStoreAsync(7, request)).ReturnsAsync(expected);

        var actual = await service.SwitchStoreAsync(7, request);

        Assert.Same(expected, actual);
        auth.Verify(x => x.SwitchStoreAsync(7, request), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_DelegatesToManagementService()
    {
        var service = CreateService(out _, out var management, out _, out _);
        var request = new CreateUserRequest { UserName = "alice", Password = "P@ssw0rd" };
        var expected = OperationResult<UserDto>.Succeed(new UserDto { UserName = "alice" });
        management.Setup(x => x.CreateUserAsync(request)).ReturnsAsync(expected);

        var actual = await service.CreateUserAsync(request);

        Assert.Same(expected, actual);
        management.Verify(x => x.CreateUserAsync(request), Times.Once);
    }

    [Fact]
    public async Task AssignRolesAsync_DelegatesToAssignmentService()
    {
        var service = CreateService(out _, out _, out var assignment, out _);
        var request = new AssignRolesRequest { UserId = 10, RoleIds = new List<long> { 1, 2 } };
        var expected = OperationResult.Succeed("角色分配成功");
        assignment.Setup(x => x.AssignRolesAsync(request)).ReturnsAsync(expected);

        var actual = await service.AssignRolesAsync(request);

        Assert.Same(expected, actual);
        assignment.Verify(x => x.AssignRolesAsync(request), Times.Once);
    }

    [Fact]
    public async Task ForceLogoutAllAsync_DelegatesToSessionService()
    {
        var service = CreateService(out _, out _, out _, out var session);
        var expected = OperationResult.Succeed("已强制用户下线");
        session.Setup(x => x.ForceLogoutAllAsync(15)).ReturnsAsync(expected);

        var actual = await service.ForceLogoutAllAsync(15);

        Assert.Same(expected, actual);
        session.Verify(x => x.ForceLogoutAllAsync(15), Times.Once);
    }

    [Fact]
    public void SplitUserServices_HaveBoundedConstructorDependencyCounts()
    {
        var splitServices = new[]
        {
            typeof(UserAuthService),
            typeof(UserManagementService),
            typeof(UserAssignmentService),
            typeof(UserSessionService),
            typeof(UserPasswordService)
        };

        foreach (var serviceType in splitServices)
        {
            var dependencyCount = serviceType.GetConstructors().Single().GetParameters().Length;
            Assert.True(dependencyCount < 15, $"{serviceType.Name} has {dependencyCount} constructor dependencies.");
        }
    }

    private static UserService CreateService(
        out Mock<IUserAuthService> auth,
        out Mock<IUserManagementService> management,
        out Mock<IUserAssignmentService> assignment,
        out Mock<IUserSessionService> session)
    {
        auth = new Mock<IUserAuthService>(MockBehavior.Strict);
        management = new Mock<IUserManagementService>(MockBehavior.Strict);
        assignment = new Mock<IUserAssignmentService>(MockBehavior.Strict);
        session = new Mock<IUserSessionService>(MockBehavior.Strict);

        return new UserService(
            new Mock<IRepository<User>>().Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<IMapper>().Object,
            auth.Object,
            management.Object,
            assignment.Object,
            session.Object);
    }
}
