using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using AutoMapper;

namespace Atlas.Services;

public class UserService : ServiceBase<User, UserDto>, IUserService
{
    private readonly IUserAuthService _authService;
    private readonly IUserManagementService _managementService;
    private readonly IUserAssignmentService _assignmentService;
    private readonly IUserSessionService _sessionService;

    public UserService(
        IRepository<User> repository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IUserAuthService authService,
        IUserManagementService managementService,
        IUserAssignmentService assignmentService,
        IUserSessionService sessionService)
        : base(repository, unitOfWork, mapper)
    {
        _authService = authService;
        _managementService = managementService;
        _assignmentService = assignmentService;
        _sessionService = sessionService;
    }

    public Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent)
    {
        return _authService.LoginAsync(request, ipAddress, userAgent);
    }

    public Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string? userAgent)
    {
        return _authService.RefreshTokenAsync(request, ipAddress, userAgent);
    }

    public Task<SwitchStoreResponse> SwitchStoreAsync(long userId, SwitchStoreRequest request)
    {
        return _authService.SwitchStoreAsync(userId, request);
    }

    public Task<List<StoreInfoDto>> GetAccessibleStoresAsync(long userId)
    {
        return _authService.GetAccessibleStoresAsync(userId);
    }

    public Task<OperationResult> LogoutAsync(string sessionId)
    {
        return _authService.LogoutAsync(sessionId);
    }

    public Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        return _managementService.CreateUserAsync(request);
    }

    public Task<OperationResult<UserDto>> UpdateUserAsync(UpdateUserRequest request)
    {
        return _managementService.UpdateUserAsync(request);
    }

    public Task<OperationResult> DeleteUserAsync(long userId)
    {
        return _managementService.DeleteUserAsync(userId);
    }

    public Task<UserDetailDto?> GetUserByIdAsync(long userId)
    {
        return _managementService.GetUserByIdAsync(userId);
    }

    public Task<UserDto?> GetUserByUserNameAsync(string userName)
    {
        return _managementService.GetUserByUserNameAsync(userName);
    }

    public Task<UserPagedResponse> GetUsersAsync(UserQueryRequest request)
    {
        return _managementService.GetUsersAsync(request);
    }

    public Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request)
    {
        return _managementService.ChangePasswordAsync(userId, request);
    }

    public Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _managementService.ResetPasswordAsync(request);
    }

    public Task<OperationResult> AssignStoresAsync(AssignStoresRequest request)
    {
        return _assignmentService.AssignStoresAsync(request);
    }

    public Task<OperationResult> AssignRolesAsync(AssignRolesRequest request)
    {
        return _assignmentService.AssignRolesAsync(request);
    }

    public Task<OperationResult> SetUserStatusAsync(long userId, bool isActive)
    {
        return _managementService.SetUserStatusAsync(userId, isActive);
    }

    public Task<OperationResult> UnlockUserAsync(long userId)
    {
        return _managementService.UnlockUserAsync(userId);
    }

    public Task<LoginLogPagedResponse> GetLoginLogsAsync(LoginLogQueryRequest request)
    {
        return _sessionService.GetLoginLogsAsync(request);
    }

    public Task<OperationResult> ForceLogoutAllAsync(long userId)
    {
        return _sessionService.ForceLogoutAllAsync(userId);
    }

    public Task<List<UserLoginLogDto>> GetActiveSessionsAsync(long userId)
    {
        return _sessionService.GetActiveSessionsAsync(userId);
    }
}
