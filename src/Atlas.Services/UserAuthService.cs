using Atlas.Core.Entities.Global;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Global.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public sealed class UserAuthService : ServiceBase, IUserAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLoginLog> _userLoginLogRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITokenService _tokenService;
    private readonly ITokenCacheService _tokenCacheService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IOperationLogService _operationLogService;
    private readonly IUserStoreAccessService _userStoreAccessService;
    private readonly IUserSecurityAuditWriter _securityAuditWriter;
    private readonly IUserLoginAuditWriter _loginAuditWriter;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly ILogger<UserAuthService> _logger;

    public UserAuthService(
        IRepository<User> userRepository,
        IRepository<UserLoginLog> userLoginLogRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ITokenCacheService tokenCacheService,
        IRefreshTokenService refreshTokenService,
        IOperationLogService operationLogService,
        IUserStoreAccessService userStoreAccessService,
        IUserSecurityAuditWriter securityAuditWriter,
        IUserLoginAuditWriter loginAuditWriter,
        ICurrentIdentity currentIdentity,
        ILogger<UserAuthService> logger)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userLoginLogRepository = userLoginLogRepository;
        _tenantRepository = tenantRepository;
        _tokenService = tokenService;
        _tokenCacheService = tokenCacheService;
        _refreshTokenService = refreshTokenService;
        _operationLogService = operationLogService;
        _userStoreAccessService = userStoreAccessService;
        _securityAuditWriter = securityAuditWriter;
        _loginAuditWriter = loginAuditWriter;
        _currentIdentity = currentIdentity;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent)
    {
        try
        {
            var tenantResult = await ValidateTenantAsync(request.Domain);
            if (!tenantResult.Success)
            {
                return new LoginResponse { Success = false, Message = tenantResult.Message };
            }

            var tenant = tenantResult.Data!;
            var userQueryBuilder = await _userRepository.QueryTrackingAsync(tenant.Id);
            var user = await userQueryBuilder
                .Where(x => x.UserName == request.UserName
                            && x.TenantId == tenant.Id
                            && x.IsDeleted == false)
                .Include(u => u.DefaultStore)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                await _loginAuditWriter.LogFailureAsync(0, tenant.Id, null, ipAddress, userAgent, "用户不存在");
                await _securityAuditWriter.WriteAsync(tenant.Id, null, null, "Login", AuditEventOutcome.Failed, ipAddress, userAgent, "用户不存在");
                return new LoginResponse { Success = false, Message = "用户名或密码错误" };
            }

            if (!user.CanLogin())
            {
                var reason = GetLoginFailureReason(user);
                await _loginAuditWriter.LogFailureAsync(user.Id, tenant.Id, null, ipAddress, userAgent, reason);
                await _securityAuditWriter.WriteAsync(tenant.Id, user.Id, null, "Login", AuditEventOutcome.Failed, ipAddress, userAgent, reason);
                return new LoginResponse { Success = false, Message = reason };
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.RecordLoginFailure();
                await UnitOfWork.SaveChangesAsync(tenant.Id);
                await _loginAuditWriter.LogFailureAsync(user.Id, tenant.Id, null, ipAddress, userAgent, "密码错误");
                await _securityAuditWriter.WriteAsync(tenant.Id, user.Id, null, "Login", AuditEventOutcome.Failed, ipAddress, userAgent, "密码错误");
                return new LoginResponse { Success = false, Message = "用户名或密码错误" };
            }

            var accessibleStores = await _userStoreAccessService.GetAccessibleStoresAsync(user.Id, tenant.Id);
            if (!accessibleStores.Any())
            {
                return new LoginResponse { Success = false, Message = "用户没有可访问的门店" };
            }

            var loginStore = _userStoreAccessService.DetermineLoginStore(user, accessibleStores, requestedStoreId: null);
            if (loginStore == null)
            {
                return new LoginResponse { Success = false, Message = "指定的门店不在可访问范围内" };
            }

            await _tokenCacheService.SetUserTokenVersionAsync(user.Id, user.TokenVersion);

            var expirationMinutes = request.RememberMe ? 10080 : 1440;
            var tokenInfo = TokenInfo.Create(
                new UserTokenIdentity
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    TenantId = tenant.Id,
                    StoreId = loginStore.Id
                },
                expirationMinutes,
                user.TokenVersion);

            var token = _tokenService.GenerateToken(tokenInfo);
            var refreshToken = await _refreshTokenService.IssueAsync(tokenInfo, ipAddress, userAgent);

            user.ResetLoginFailedCount();
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            await UnitOfWork.SaveChangesAsync(tenant.Id);

            await _loginAuditWriter.LogSuccessAsync(user, tenant.Id, loginStore.Id, tokenInfo.SessionId, ipAddress, userAgent, tokenInfo.ExpiresAt);
            await _securityAuditWriter.WriteAsync(
                tenant.Id,
                user.Id,
                loginStore.Id,
                "Login",
                AuditEventOutcome.Succeeded,
                ipAddress,
                userAgent,
                sessionId: tokenInfo.SessionId);

            return new LoginResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken.Token,
                User = UserDtoMapper.ToDto(user),
                ExpiresIn = expirationMinutes * 60,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
                CurrentStore = _userStoreAccessService.MapToStoreInfo(loginStore, accessibleStores),
                AccessibleStores = accessibleStores
                    .Select(x => _userStoreAccessService.MapToStoreInfo(x.Store, accessibleStores))
                    .ToList(),
                RequirePasswordChange = user.MustChangePassword || user.IsPasswordExpired()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败: {UserName}", request.UserName);
            return new LoginResponse { Success = false, Message = "登录失败，请稍后重试" };
        }
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string? userAgent)
    {
        var result = await _refreshTokenService.ExchangeAsync(request.RefreshToken, ipAddress, userAgent);
        if (!result.Success)
        {
            return new LoginResponse
            {
                Success = false,
                Message = result.Message ?? "刷新令牌无效"
            };
        }

        await _securityAuditWriter.WriteAsync(
            result.TenantId.GetValueOrDefault(),
            result.UserId,
            result.StoreId,
            "RefreshToken",
            AuditEventOutcome.Succeeded,
            ipAddress,
            userAgent);

        return new LoginResponse
        {
            Success = true,
            Token = result.AccessToken ?? string.Empty,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresIn,
            ExpiresAt = result.AccessTokenExpiresAtUtc
        };
    }

    public async Task<SwitchStoreResponse> SwitchStoreAsync(long userId, SwitchStoreRequest request)
    {
        try
        {
            var userQueryBuilder = await _userRepository.QueryAsync();
            var user = await userQueryBuilder
                .Where(x => x.Id == userId && x.IsDeleted == false)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return new SwitchStoreResponse { Success = false, Message = "用户不存在" };
            }

            var accessibleStores = await _userStoreAccessService.GetAccessibleStoresAsync(userId, user.TenantId);
            var targetStore = accessibleStores.FirstOrDefault(s => s.Store.Id == request.StoreId);

            if (targetStore == null)
            {
                return new SwitchStoreResponse { Success = false, Message = "您没有权限访问该门店" };
            }

            var expirationMinutes = 1440;
            var previousSessionId = _currentIdentity.SessionId;
            var tokenInfo = TokenInfo.Create(
                new UserTokenIdentity
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    TenantId = user.TenantId,
                    StoreId = targetStore.Store.Id
                },
                expirationMinutes,
                user.TokenVersion);

            var token = _tokenService.GenerateToken(tokenInfo);
            var refreshToken = await _refreshTokenService.IssueAsync(tokenInfo, null, null);

            await _operationLogService.LogOperationAsync(new LogOperationRequest
            {
                TenantId = user.TenantId,
                UserId = userId,
                StoreId = targetStore.Store.Id,
                Module = "User",
                OperationType = "SwitchStore",
                Description = $"用户 {user.UserName} 切换到门店 {targetStore.Store.Name}",
                EntityId = targetStore.Store.Id,
                SessionId = previousSessionId,
                IsSuccess = true
            });

            if (!string.IsNullOrWhiteSpace(previousSessionId))
            {
                await _tokenCacheService.InvalidateSessionAsync(previousSessionId);
                await _refreshTokenService.RevokeSessionAsync(user.TenantId, previousSessionId, "SwitchStore");
            }

            return new SwitchStoreResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken.Token,
                ExpiresIn = expirationMinutes * 60,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
                CurrentStore = _userStoreAccessService.MapToStoreInfo(targetStore.Store, accessibleStores)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换门店失败: UserId={UserId}, StoreId={StoreId}", userId, request.StoreId);
            return new SwitchStoreResponse { Success = false, Message = "切换门店失败，请稍后重试" };
        }
    }

    public async Task<List<StoreInfoDto>> GetAccessibleStoresAsync(long userId)
    {
        var userQueryBuilder = await _userRepository.QueryAsync();
        var user = await userQueryBuilder
            .Where(x => x.Id == userId && x.IsDeleted == false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new List<StoreInfoDto>();
        }

        var accessibleStores = await _userStoreAccessService.GetAccessibleStoresAsync(userId, user.TenantId);
        return accessibleStores
            .Select(s => _userStoreAccessService.MapToStoreInfo(s.Store, accessibleStores))
            .ToList();
    }

    public async Task<OperationResult> LogoutAsync(string sessionId)
    {
        try
        {
            var queryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
            var loginLog = await queryBuilder
                .Where(x => x.SessionId == sessionId)
                .FirstOrDefaultAsync();

            if (loginLog != null)
            {
                loginLog.LogoutAt = DateTime.UtcNow;
                loginLog.LogoutType = "Manual";
                await CommitAsync();

                try
                {
                    await _tokenCacheService.InvalidateSessionAsync(sessionId);
                    await _refreshTokenService.RevokeSessionAsync(loginLog.TenantId, sessionId, "Logout");
                    await _securityAuditWriter.WriteAsync(
                        loginLog.TenantId,
                        loginLog.UserId,
                        loginLog.StoreId,
                        "Logout",
                        AuditEventOutcome.Succeeded,
                        sessionId: sessionId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogError(cacheEx, "Cache invalidation failed but logout succeeded in DB");
                }
            }

            return OperationResult.Succeed("登出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed - SessionId: {SessionId}", sessionId);
            return OperationResult.Failed("登出失败");
        }
    }

    private async Task<OperationResult<Tenant>> ValidateTenantAsync(string domain)
    {
        var tenantQueryBuilder = await _tenantRepository.QueryAsync();
        var tenant = await tenantQueryBuilder
            .Where(t => t.Domain == domain && t.IsDeleted == false)
            .FirstOrDefaultAsync();

        if (tenant == null)
        {
            return OperationResult<Tenant>.Failed("租户不存在");
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            return OperationResult<Tenant>.Failed("租户已禁用");
        }

        if (tenant.Status == TenantStatus.Expired)
        {
            return OperationResult<Tenant>.Failed("租户已过期");
        }

        return OperationResult<Tenant>.Succeed(tenant);
    }

    private static string GetLoginFailureReason(User user)
    {
        if (user.IsLockedOut())
        {
            return "账号已被锁定，请稍后再试";
        }

        if (user.Status == UserStatus.Disabled)
        {
            return "账号已被禁用";
        }

        if (!user.IsActivated)
        {
            return "账号未激活";
        }

        if (user.IsDeleted)
        {
            return "账号不存在";
        }

        return "无法登录";
    }

}
