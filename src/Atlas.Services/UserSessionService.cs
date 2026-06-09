using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public sealed class UserSessionService : ServiceBase, IUserSessionService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLoginLog> _userLoginLogRepository;
    private readonly ITokenCacheService _tokenCacheService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IOperationLogService _operationLogService;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        IRepository<User> userRepository,
        IRepository<UserLoginLog> userLoginLogRepository,
        IUnitOfWork unitOfWork,
        ITokenCacheService tokenCacheService,
        IRefreshTokenService refreshTokenService,
        IOperationLogService operationLogService,
        ILogger<UserSessionService> logger)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userLoginLogRepository = userLoginLogRepository;
        _tokenCacheService = tokenCacheService;
        _refreshTokenService = refreshTokenService;
        _operationLogService = operationLogService;
        _logger = logger;
    }

    public async Task<LoginLogPagedResponse> GetLoginLogsAsync(LoginLogQueryRequest request)
    {
        var queryBuilder = await _userLoginLogRepository.QueryAsync();
        var query = queryBuilder.Where(x => true);

        if (request.UserId.HasValue)
        {
            query = query.Where(l => l.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            query = query.Where(l => l.IpAddress.Contains(request.IpAddress));
        }

        if (request.IsSuccess.HasValue)
        {
            query = query.Where(l => l.IsSuccess == request.IsSuccess.Value);
        }

        if (request.StartTime.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= request.EndTime.Value);
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new LoginLogPagedResponse
        {
            Items = logs.Select(l => new UserLoginLogDto
            {
                Id = l.Id,
                SessionId = l.SessionId,
                IpAddress = l.IpAddress,
                DeviceType = l.DeviceType,
                Browser = l.Browser,
                OperatingSystem = l.OperatingSystem,
                LoginMethod = l.LoginMethod,
                IsSuccess = l.IsSuccess,
                FailureReason = l.FailureReason,
                CreatedAt = l.CreatedAt,
                LogoutAt = l.LogoutAt,
                LogoutType = l.LogoutType
            }).ToList(),
            Total = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<OperationResult> ForceLogoutAllAsync(long userId)
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

            user.InvalidateAllTokens();
            await CommitAsync();
            await _tokenCacheService.SetUserTokenVersionAsync(userId, user.TokenVersion);

            var userLoginQueryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
            var activeSessions = await userLoginQueryBuilder
                .Where(l => l.UserId == userId && l.LogoutAt == null)
                .ToListAsync();

            foreach (var session in activeSessions)
            {
                session.LogoutAt = DateTime.UtcNow;
                session.LogoutType = "ForceLogout";
                if (!string.IsNullOrEmpty(session.SessionId))
                {
                    await _tokenCacheService.InvalidateSessionAsync(session.SessionId);
                }
            }

            await _refreshTokenService.RevokeUserAsync(user.TenantId, userId, "ForceLogout");
            await CommitAsync();

            await _operationLogService.LogOperationAsync(new LogOperationRequest
            {
                TenantId = user.TenantId,
                UserId = userId,
                Module = "User",
                OperationType = "ForceLogout",
                Description = $"用户 {user.UserName} 被强制下线",
                EntityId = userId,
                IsSuccess = true
            });

            _logger.LogInformation(
                "Force logout - UserId: {UserId}, TokenVersion: {Version}, invalidated {Count} sessions",
                userId,
                user.TokenVersion,
                activeSessions.Count);

            return OperationResult.Succeed("已强制用户下线");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Force logout failed - UserId: {UserId}", userId);
            if (user != null)
            {
                await _operationLogService.LogOperationAsync(new LogOperationRequest
                {
                    TenantId = user.TenantId,
                    UserId = userId,
                    Module = "User",
                    OperationType = "ForceLogout",
                    Description = $"用户 {user.UserName} 强制下线失败",
                    EntityId = userId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }

            return OperationResult.Failed("操作失败");
        }
    }

    public async Task<List<UserLoginLogDto>> GetActiveSessionsAsync(long userId)
    {
        var userLoginQueryBuilder = await _userLoginLogRepository.QueryAsync();
        var sessions = await userLoginQueryBuilder
            .Where(l => l.UserId == userId &&
                        l.IsSuccess &&
                        l.LogoutAt == null &&
                        l.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return sessions.Select(l => new UserLoginLogDto
        {
            Id = l.Id,
            SessionId = l.SessionId,
            IpAddress = l.IpAddress,
            DeviceType = l.DeviceType,
            Browser = l.Browser,
            OperatingSystem = l.OperatingSystem,
            LoginMethod = l.LoginMethod,
            IsSuccess = l.IsSuccess,
            CreatedAt = l.CreatedAt
        }).ToList();
    }
}
