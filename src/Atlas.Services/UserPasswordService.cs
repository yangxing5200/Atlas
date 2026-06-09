using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public interface IUserPasswordService
{
    Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request);

    Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request);
}

public sealed class UserPasswordService : ServiceBase, IUserPasswordService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLoginLog> _userLoginLogRepository;
    private readonly ITokenCacheService _tokenCacheService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IOperationLogService _operationLogService;
    private readonly ILogger<UserPasswordService> _logger;

    public UserPasswordService(
        IRepository<User> userRepository,
        IRepository<UserLoginLog> userLoginLogRepository,
        IUnitOfWork unitOfWork,
        ITokenCacheService tokenCacheService,
        IRefreshTokenService refreshTokenService,
        IOperationLogService operationLogService,
        ILogger<UserPasswordService> logger)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userLoginLogRepository = userLoginLogRepository;
        _tokenCacheService = tokenCacheService;
        _refreshTokenService = refreshTokenService;
        _operationLogService = operationLogService;
        _logger = logger;
    }

    public async Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request)
    {
        User? user = null;
        try
        {
            var queryBuilder = await _userRepository.QueryTrackingAsync();
            user = await queryBuilder
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return OperationResult.Failed("用户不存在");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                return OperationResult.Failed("旧密码不正确");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.TokenVersion++;
            user.MustChangePassword = false;

            await CommitAsync();
            await InvalidateActiveSessionsAsync(user, userId, "PasswordChanged");

            await _operationLogService.LogOperationAsync(new LogOperationRequest
            {
                TenantId = user.TenantId,
                UserId = userId,
                Module = "User",
                OperationType = "ChangePassword",
                Description = $"用户 {user.UserName} 修改了密码",
                EntityId = userId,
                IsSuccess = true
            });

            _logger.LogInformation("Password changed - UserId: {UserId}", userId);
            return OperationResult.Succeed("密码修改成功，所有会话已失效，请重新登录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change password failed - UserId: {UserId}", userId);
            if (user != null)
            {
                await _operationLogService.LogOperationAsync(new LogOperationRequest
                {
                    TenantId = user.TenantId,
                    UserId = userId,
                    Module = "User",
                    OperationType = "ChangePassword",
                    Description = $"用户 {user.UserName} 修改密码失败",
                    EntityId = userId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }

            return OperationResult.Failed("修改密码失败");
        }
    }

    public async Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        User? user = null;
        try
        {
            var queryBuilder = await _userRepository.QueryTrackingAsync();
            user = await queryBuilder
                .Where(u => u.Id == request.UserId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return OperationResult.Failed("用户不存在");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.TokenVersion++;
            user.MustChangePassword = request.MustChangePassword;

            await CommitAsync();
            await InvalidateActiveSessionsAsync(user, request.UserId, "PasswordReset");

            await _operationLogService.LogOperationAsync(new LogOperationRequest
            {
                TenantId = user.TenantId,
                UserId = request.UserId,
                Module = "User",
                OperationType = "ResetPassword",
                Description = $"用户 {user.UserName} 的密码被重置",
                EntityId = request.UserId,
                IsSuccess = true
            });

            _logger.LogInformation("Password reset - UserId: {UserId}", request.UserId);
            return OperationResult.Succeed("密码重置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password failed - UserId: {UserId}", request.UserId);
            if (user != null)
            {
                await _operationLogService.LogOperationAsync(new LogOperationRequest
                {
                    TenantId = user.TenantId,
                    UserId = request.UserId,
                    Module = "User",
                    OperationType = "ResetPassword",
                    Description = $"用户 {user.UserName} 的密码重置失败",
                    EntityId = request.UserId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }

            return OperationResult.Failed("重置密码失败");
        }
    }

    private async Task InvalidateActiveSessionsAsync(User user, long userId, string logoutType)
    {
        await _tokenCacheService.SetUserTokenVersionAsync(userId, user.TokenVersion);

        var userLoginQueryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
        var activeSessions = await userLoginQueryBuilder
            .Where(l => l.UserId == userId && l.LogoutAt == null)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.LogoutAt = DateTime.UtcNow;
            session.LogoutType = logoutType;
            if (!string.IsNullOrEmpty(session.SessionId))
            {
                await _tokenCacheService.InvalidateSessionAsync(session.SessionId);
            }
        }

        await _refreshTokenService.RevokeUserAsync(user.TenantId, userId, logoutType);
        await CommitAsync();

        _logger.LogInformation(
            "Invalidated active sessions - UserId: {UserId}, LogoutType: {LogoutType}, Count: {Count}",
            userId,
            logoutType,
            activeSessions.Count);
    }
}
