using Atlas.Core.Entities.Global;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Data.Global.Repositories;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Atlas.Services.Abstractions.Base;
using AutoMapper;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services
{
    public class UserService : ServiceBase<User, UserDto>, IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly ITokenService _tokenService;
        private readonly ITokenCacheService _tokenCacheService;
        private readonly IRepository<UserLoginLog> _userLoginLogRepository;
        private readonly IRepository<UserStore> _userStoreRepository;
        private readonly IStoreRepository _storeRepository;
        private readonly ITenantRepository _tenantRepository;

        public UserService(
          IRepository<User> repository,
          IUnitOfWork unitOfWork,
          IMapper mapper,
          IRepository<UserLoginLog> userLoginLogRepository,
          IRepository<UserStore> userStoreRepository,
          IStoreRepository storeRepository,
          ITenantRepository tenantRepository,
          ITokenService tokenService,
          ITokenCacheService tokenCacheService,
          ILogger<UserService> logger)
          : base(repository, unitOfWork, mapper)
        {
            _userLoginLogRepository = userLoginLogRepository;
            _userStoreRepository = userStoreRepository;
            _storeRepository = storeRepository;
            _tenantRepository = tenantRepository;
            _tokenService = tokenService;
            _tokenCacheService = tokenCacheService;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent)
        {
            try
            {
                // ========== 第一步：租户验证 ==========
                var tenantResult = await ValidateTenantAsync(request.Domain);
                if (!tenantResult.Success)
                {
                    return new LoginResponse { Success = false, Message = tenantResult.Message };
                }
                var tenant = tenantResult.Data!;

                // ========== 第二步：用户验证 ==========
                var userQueryBuilder = await _repository.QueryAsync();
                var user = await userQueryBuilder
                    .Where(x => x.UserName == request.UserName
                                && x.TenantId == tenant.Id
                                && x.IsDeleted == false)
                    .Include(u => u.DefaultStore)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    await LogLoginFailureAsync(0, tenant.Id, null, ipAddress, userAgent, "用户不存在");
                    return new LoginResponse { Success = false, Message = "用户名或密码错误" };
                }

                if (!user.CanLogin())
                {
                    var reason = GetLoginFailureReason(user);
                    await LogLoginFailureAsync(user.Id, tenant.Id, null, ipAddress, userAgent, reason);
                    return new LoginResponse { Success = false, Message = reason };
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    user.RecordLoginFailure();
                    await CommitAsync();
                    await LogLoginFailureAsync(user.Id, tenant.Id, null, ipAddress, userAgent, "密码错误");
                    return new LoginResponse { Success = false, Message = "用户名或密码错误" };
                }

                // ========== 第三步：获取可访问门店列表 ==========
                var accessibleStores = await GetUserAccessibleStoresAsync(user.Id);
                if (!accessibleStores.Any())
                {
                    return new LoginResponse { Success = false, Message = "用户没有可访问的门店" };
                }

                // ========== 第四步：确定登录门店 ==========
                var loginStore = DetermineLoginStore(user, accessibleStores, request.StoreId);
                if (loginStore == null)
                {
                    return new LoginResponse { Success = false, Message = "指定的门店不在可访问范围内" };
                }

                // ========== 第五步：生成Token ==========
                _tokenCacheService.SetUserTokenVersion(user.Id, user.TokenVersion);

                var expirationMinutes = request.RememberMe ? 10080 : 1440;
                var tokenInfo = TokenInfo.Create(
                    new CurrentIdentityImpl
                    {
                        UserId = user.Id,
                        UserName = user.UserName,
                        TenantId = tenant.Id,
                        StoreId = loginStore.Id
                    },
                    expirationMinutes,
                    user.TokenVersion
                );

                var token = _tokenService.GenerateToken(tokenInfo);

                // ========== 第六步：更新用户登录信息 ==========
                user.ResetLoginFailedCount();
                user.LastLoginAt = DateTime.UtcNow;
                user.LastLoginIp = ipAddress;
                await CommitAsync();

                await LogLoginSuccessAsync(user, loginStore.Id, tokenInfo.SessionId, ipAddress, userAgent, tokenInfo.ExpiresAt);

                // ========== 第七步：返回结果 ==========
                return new LoginResponse
                {
                    Success = true,
                    Token = token,
                    User = MapToDto(user),
                    ExpiresIn = expirationMinutes * 60,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    CurrentStore = MapToStoreInfo(loginStore, accessibleStores),
                    AccessibleStores = accessibleStores
                    .Select(x => MapToStoreInfo(x.Store, accessibleStores))  // 传入 x.Store
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

        public async Task<SwitchStoreResponse> SwitchStoreAsync(long userId, SwitchStoreRequest request)
        {
            try
            {
                // 获取用户
                var userQueryBuilder = await _repository.QueryAsync();
                var user = await userQueryBuilder
                    .Where(x => x.Id == userId && x.IsDeleted == false)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return new SwitchStoreResponse { Success = false, Message = "用户不存在" };
                }

                // 验证目标门店访问权限
                var accessibleStores = await GetUserAccessibleStoresAsync(userId);
                var targetStore = accessibleStores.FirstOrDefault(s => s.Store.Id == request.StoreId);

                if (targetStore.UserStore==null)
                {
                    return new SwitchStoreResponse { Success = false, Message = "您没有权限访问该门店" };
                }

                // 生成新Token
                var expirationMinutes = 1440;
                var tokenInfo = TokenInfo.Create(
                    new CurrentIdentityImpl
                    {
                        UserId = user.Id,
                        UserName = user.UserName,
                        TenantId = user.TenantId,
                        StoreId = targetStore.Store.Id
                    },
                    expirationMinutes,
                    user.TokenVersion
                );

                var token = _tokenService.GenerateToken(tokenInfo);

                return new SwitchStoreResponse
                {
                    Success = true,
                    Token = token,
                    ExpiresIn = expirationMinutes * 60,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    CurrentStore = MapToStoreInfo(targetStore.Store, accessibleStores)
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
            var accessibleStores = await GetUserAccessibleStoresAsync(userId);
            return accessibleStores.Select(s => MapToStoreInfo(s.Store, accessibleStores)).ToList();
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

                    // Cache invalidation after DB commit
                    try
                    {
                        _tokenCacheService.InvalidateSession(sessionId);
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

        public async Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequest request)
        {
            try
            {
                var queryBuilder = await _repository.QueryAsync();
                // 检查用户名是否存在
                var exists = await queryBuilder
                        .Where(x => x.UserName == request.UserName && x.IsDeleted == false)
                        .AnyAsync();

                if (exists)
                {
                    return OperationResult<UserDto>.Failed("用户名已存在");
                }

                // 创建用户
                var user = new User
                {
                    UserName = request.UserName,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    RealName = request.RealName,
                    NickName = request.NickName,
                    Phone = request.Phone,
                    Email = request.Email,
                    Gender = request.Gender,
                    Type = request.Type,
                    Status = UserStatus.Active,
                    IsActivated = true,
                    DefaultStoreId = request.DefaultStoreId,
                    EmployeeNo = request.EmployeeNo,
                    DepartmentId = request.DepartmentId,
                    Position = request.Position,
                    HireDate = request.HireDate,
                    Remark = request.Remark,
                    MustChangePassword = request.MustChangePassword,
                };
                await _repository.AddAsync(user);
                await CommitAsync();

                // 分配门店
                if (request.StoreIds?.Any() == true)
                {
                    var userStoreList = request.StoreIds.Select(storeId => new UserStore
                    {
                        UserId = user.Id,
                        StoreId = storeId,
                        IsPrimary = storeId == request.DefaultStoreId
                    }).ToList();

                    await _userStoreRepository.AddRangeAsync(userStoreList);
                    await CommitAsync();
                }

                return OperationResult<UserDto>.Succeed(MapToDto(user), "用户创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建用户失败: {UserName}", request.UserName);
                return OperationResult<UserDto>.Failed("创建用户失败");
            }
        }

        public async Task<OperationResult<UserDto>> UpdateUserAsync(UpdateUserRequest request)
        {
            try
            {
                var queryBuilder = await _repository.QueryAsync();
                var user = await queryBuilder
                    .Where(u => u.Id == request.Id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return OperationResult<UserDto>.Failed("用户不存在");
                }

                // 更新字段
                user.RealName = request.RealName;
                user.NickName = request.NickName;
                user.Phone = request.Phone;
                user.Email = request.Email;
                user.Gender = request.Gender;
                user.Type = request.Type;
                user.Status = request.Status;
                user.DefaultStoreId = request.DefaultStoreId;
                user.EmployeeNo = request.EmployeeNo;
                user.DepartmentId = request.DepartmentId;
                user.Position = request.Position;
                user.HireDate = request.HireDate;
                user.Remark = request.Remark;

                await CommitAsync();

                return OperationResult<UserDto>.Succeed(MapToDto(user), "用户更新成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新用户失败: UserId={UserId}", request.Id);
                return OperationResult<UserDto>.Failed("更新用户失败");
            }
        }

        public async Task<OperationResult> DeleteUserAsync(long userId)
        {
            try
            {
                var builder = await _repository.QueryAsync();
                var user = await builder
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return OperationResult.Failed("用户不存在");
                }

                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;

                await CommitAsync();

                return OperationResult.Succeed("用户删除成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除用户失败: UserId={UserId}", userId);
                return OperationResult.Failed("删除用户失败");
            }
        }

        public async Task<UserDetailDto?> GetUserByIdAsync(long userId)
        {
            var queryBuilder = await _repository.QueryAsync();
            var user = await queryBuilder
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Include(u => u.DefaultStore)
                .Include(u => u.UserStores).ThenInclude<UserStore, Store>(us => us.Store)
                .FirstOrDefaultAsync();

            if (user == null) return null;

            return new UserDetailDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                UserName = user.UserName,
                RealName = user.RealName,
                NickName = user.NickName,
                Phone = user.Phone,
                Email = user.Email,
                Avatar = user.Avatar,
                Gender = user.Gender,
                Type = user.Type,
                Status = user.Status,
                IsActivated = user.IsActivated,
                DefaultStoreId = user.DefaultStoreId,
                DefaultStoreName = user.DefaultStore?.Name,
                EmployeeNo = user.EmployeeNo,
                DepartmentId = user.DepartmentId,
                Position = user.Position,
                HireDate = user.HireDate,
                Remark = user.Remark,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                TokenVersion = user.TokenVersion,
                LoginFailedCount = user.LoginFailedCount,
                LockoutEndAt = user.LockoutEndAt,
                MustChangePassword = user.MustChangePassword,
                UserStores = user.UserStores.Select(us => new UserStoreDto
                {
                    StoreId = us.StoreId,
                    StoreName = us.Store.Name,
                    IsPrimary = us.IsPrimary,
                    Permission = us.Permission,
                    EffectiveFrom = us.EffectiveFrom,
                    EffectiveTo = us.EffectiveTo
                }).ToList()
            };
        }

        public async Task<UserDto?> GetUserByUserNameAsync(string userName)
        {
            var queryBuilder = await _repository.QueryAsync();
            var user = await queryBuilder
                .Where(u => u.UserName == userName && !u.IsDeleted)
                .Include(u => u.DefaultStore)
                .FirstOrDefaultAsync();

            return user == null ? null : MapToDto(user);
        }

        public async Task<UserPagedResponse> GetUsersAsync(UserQueryRequest request)
        {
            var queryBuilder = await _repository.QueryAsync();
            var query = queryBuilder.Where(u => !u.IsDeleted);

            // 关键字搜索
            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(u =>
                    u.UserName.Contains(request.Keyword) ||
                    u.RealName.Contains(request.Keyword) ||
                    (u.Phone != null && u.Phone.Contains(request.Keyword)));
            }

            // 类型筛选
            if (request.Type.HasValue)
            {
                query = query.Where(u => u.Type == request.Type.Value);
            }

            // 状态筛选
            if (request.Status.HasValue)
            {
                query = query.Where(u => u.Status == request.Status.Value);
            }

            // 门店筛选
            if (request.StoreId.HasValue)
            {
                query = query.Where(u => u.UserStores.Any(us => us.StoreId == request.StoreId.Value));
            }

            // 部门筛选
            if (request.DepartmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == request.DepartmentId.Value);
            }

            // 激活状态筛选
            if (request.IsActivated.HasValue)
            {
                query = query.Where(u => u.IsActivated == request.IsActivated.Value);
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .Include(u => u.DefaultStore)
                .OrderByDescending(u => u.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new UserPagedResponse
            {
                Items = users.Select(MapToDto).ToList(),
                Total = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            };
        }
        public async Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request)
        {
            try
            {
                var queryBuilder = await _repository.QueryTrackingAsync();
                var user = await queryBuilder
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return OperationResult.Failed("用户不存在");

                if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
                    return OperationResult.Failed("旧密码不正确");

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.TokenVersion++;
                user.MustChangePassword = false;

                await CommitAsync();

                // ✅ 关键修复：先更新缓存中的TokenVersion，再清除token缓存
                _tokenCacheService.SetUserTokenVersion(userId, user.TokenVersion);

                // ✅ 强制所有相关Session失效
                var userLoginQueryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
                var activeSessions = await userLoginQueryBuilder
                    .Where(l => l.UserId == userId && l.LogoutAt == null)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.LogoutAt = DateTime.UtcNow;
                    session.LogoutType = "PasswordChanged";
                    _tokenCacheService.InvalidateSession(session.SessionId);
                }

                await CommitAsync();

                _logger.LogInformation("Password changed - UserId: {UserId}, invalidated {Count} sessions",
                    userId, activeSessions.Count);

                return OperationResult.Succeed("密码修改成功，所有会话已失效，请重新登录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Change password failed - UserId: {UserId}", userId);
                return OperationResult.Failed("修改密码失败");
            }
        }

        public async Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var user = await _repository.TrackingFirstOrDefaultAsync(
                    q => q.Where(u => u.Id == request.UserId && !u.IsDeleted),
                    ct: default);

                if (user == null)
                    return OperationResult.Failed("用户不存在");

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.TokenVersion++;
                user.MustChangePassword = request.MustChangePassword;

                await CommitAsync();

                // ✅ 同样的修复
                _tokenCacheService.SetUserTokenVersion(request.UserId, user.TokenVersion);

                var userLoginQueryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
                var activeSessions = await userLoginQueryBuilder
                    .Where(l => l.UserId == request.UserId && l.LogoutAt == null)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.LogoutAt = DateTime.UtcNow;
                    session.LogoutType = "PasswordReset";
                    _tokenCacheService.InvalidateSession(session.SessionId);
                }

                await CommitAsync();

                _logger.LogInformation("Password reset - UserId: {UserId}, invalidated {Count} sessions",
                    request.UserId, activeSessions.Count);

                return OperationResult.Succeed("密码重置成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reset password failed - UserId: {UserId}", request.UserId);
                return OperationResult.Failed("重置密码失败");
            }
        }


        public async Task<OperationResult> AssignStoresAsync(AssignStoresRequest request)
        {
            try
            {
                var queryBuilder = await _userStoreRepository.QueryTrackingAsync();
                // 删除旧的门店关联
                var oldStores = await queryBuilder
                    .Where(us => us.UserId == request.UserId)
                    .ToListAsync();

                await _userStoreRepository.RemoveRangeAsync(oldStores);

                // 添加新的门店关联
                var newStores = request.Stores.Select(store => new UserStore
                {
                    UserId = request.UserId,
                    StoreId = store.StoreId,
                    IsPrimary = store.IsPrimary,
                    Permission = store.Permission,
                    EffectiveFrom = store.EffectiveFrom,
                    EffectiveTo = store.EffectiveTo
                }).ToList();

                await _userStoreRepository.AddRangeAsync(newStores);
                await CommitAsync();

                return OperationResult.Succeed("门店分配成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分配门店失败: UserId={UserId}", request.UserId);
                return OperationResult.Failed("分配门店失败");
            }
        }

        public async Task<OperationResult> SetUserStatusAsync(long userId, bool isActive)
        {
            try
            {
                var queryBuilder = await _repository.QueryTrackingAsync();
                var user = await queryBuilder
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return OperationResult.Failed("用户不存在");
                }

                user.Status = isActive ? UserStatus.Active : UserStatus.Disabled;

                await CommitAsync();

                return OperationResult.Succeed($"用户已{(isActive ? "启用" : "禁用")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置用户状态失败: UserId={UserId}", userId);
                return OperationResult.Failed("操作失败");
            }
        }

        public async Task<OperationResult> UnlockUserAsync(long userId)
        {
            try
            {
                var queryBuilder = await _repository.QueryTrackingAsync();
                var user = await queryBuilder
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return OperationResult.Failed("用户不存在");
                }

                user.ResetLoginFailedCount();

                await CommitAsync();

                return OperationResult.Succeed("用户已解锁");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解锁用户失败: UserId={UserId}", userId);
                return OperationResult.Failed("解锁失败");
            }
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
            try
            {
                var queryBuilder = await _repository.QueryTrackingAsync();
                var user = await queryBuilder
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return OperationResult.Failed("用户不存在");

                user.InvalidateAllTokens();
                await CommitAsync();

                // ✅ 先更新TokenVersion缓存
                _tokenCacheService.SetUserTokenVersion(userId, user.TokenVersion);

                var userLoginQueryBuilder = await _userLoginLogRepository.QueryTrackingAsync();
                var activeSessions = await userLoginQueryBuilder
                    .Where(l => l.UserId == userId && l.LogoutAt == null)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.LogoutAt = DateTime.UtcNow;
                    session.LogoutType = "ForceLogout";
                    _tokenCacheService.InvalidateSession(session.SessionId);
                }

                await CommitAsync();

                _logger.LogInformation("Force logout - UserId: {UserId}, TokenVersion: {Version}, invalidated {Count} sessions",
                    userId, user.TokenVersion, activeSessions.Count);

                return OperationResult.Succeed("已强制用户下线");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force logout failed - UserId: {UserId}", userId);
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

        #region Private Methods

        private UserDto MapToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                UserName = user.UserName,
                RealName = user.RealName,
                NickName = user.NickName,
                Phone = user.Phone,
                Email = user.Email,
                Avatar = user.Avatar,
                Gender = user.Gender,
                Type = user.Type,
                Status = user.Status,
                IsActivated = user.IsActivated,
                DefaultStoreId = user.DefaultStoreId,
                DefaultStoreName = user.DefaultStore?.Name,
                EmployeeNo = user.EmployeeNo,
                Position = user.Position,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt
            };
        }

        private string GetLoginFailureReason(User user)
        {
            if (user.IsLockedOut())
                return "账号已被锁定，请稍后再试";
            if (user.Status == UserStatus.Disabled)
                return "账号已被禁用";
            if (!user.IsActivated)
                return "账号未激活";
            if (user.IsDeleted)
                return "账号不存在";
            return "无法登录";
        }

        private async Task LogLoginSuccessAsync(User user, string sessionId, string ipAddress, string? userAgent, long expiresAt)
        {
            var loginLog = new UserLoginLog
            {
                UserId = user.Id,
                SessionId = sessionId,
                TokenVersion = user.TokenVersion,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceType = ParseDeviceType(userAgent),
                Browser = ParseBrowser(userAgent),
                StoreId = user.DefaultStoreId,
                LoginMethod = "Password",
                IsSuccess = true,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime
            };

            await _userLoginLogRepository.AddAsync(loginLog);
            await CommitAsync();
        }

        private async Task LogLoginFailureAsync(long userId, string ipAddress, string? userAgent, string reason)
        {
            var loginLog = new UserLoginLog
            {
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceType = ParseDeviceType(userAgent),
                Browser = ParseBrowser(userAgent),
                LoginMethod = "Password",
                IsSuccess = false,
                FailureReason = reason
            };

            await _userLoginLogRepository.AddAsync(loginLog);
            await CommitAsync();
        }

        private string? ParseDeviceType(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return null;

            if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
                return "Mobile";
            if (userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
                return "Tablet";
            return "Desktop";
        }

        private string? ParseBrowser(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return null;

            if (userAgent.Contains("Chrome")) return "Chrome";
            if (userAgent.Contains("Firefox")) return "Firefox";
            if (userAgent.Contains("Safari")) return "Safari";
            if (userAgent.Contains("Edge")) return "Edge";
            return "Unknown";
        }
        /// <summary>
        /// 验证租户
        /// </summary>
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

            // 可根据需要添加更多状态检查
            if (tenant.Status == TenantStatus.Expired)
            {
                return OperationResult<Tenant>.Failed("租户已过期");
            }

            return OperationResult<Tenant>.Succeed(tenant);
        }

        /// <summary>
        /// 获取用户可访问的门店列表
        /// </summary>
        private async Task<List<(UserStore UserStore, Store Store)>> GetUserAccessibleStoresAsync(long userId)
        {
            var userStoreQueryBuilder = await _userStoreRepository.QueryAsync();
            var now = DateTime.UtcNow;

            var userStores = await userStoreQueryBuilder
                .Where(us => us.UserId == userId
                             && (us.EffectiveFrom == null || us.EffectiveFrom <= now)
                             && (us.EffectiveTo == null || us.EffectiveTo >= now))
                .ToListAsync();

            if (!userStores.Any())
            {
                return new List<(UserStore, Store)>();
            }

            var storeIds = userStores.Select(us => us.StoreId).ToList();
            var storeQueryBuilder = await _storeRepository.QueryAsync();
            var stores = await storeQueryBuilder
                .Where(s => storeIds.Contains(s.Id) && s.IsActive)
                .ToListAsync();

            return userStores
                .Join(stores, us => us.StoreId, s => s.Id, (us, s) => (us, s))
                .ToList();
        }

        /// <summary>
        /// 确定登录门店
        /// </summary>
        private Store? DetermineLoginStore(User user, List<(UserStore UserStore, Store Store)> accessibleStores, long? requestedStoreId)
        {
            if (requestedStoreId.HasValue)
            {
                // 用户指定了门店
                var requested = accessibleStores.FirstOrDefault(s => s.Store.Id == requestedStoreId.Value);
                return requested.Store;
            }

            // 优先使用主门店
            var primary = accessibleStores.FirstOrDefault(s => s.UserStore.IsPrimary);
            if (primary.Store != null)
            {
                return primary.Store;
            }

            // 其次使用默认门店
            if (user.DefaultStoreId.HasValue)
            {
                var defaultStore = accessibleStores.FirstOrDefault(s => s.Store.Id == user.DefaultStoreId.Value);
                if (defaultStore.Store != null)
                {
                    return defaultStore.Store;
                }
            }

            // 最后使用第一个可访问的门店
            return accessibleStores.FirstOrDefault().Store;
        }

        /// <summary>
        /// 映射门店信息
        /// </summary>
        private StoreInfoDto MapToStoreInfo(Store store, List<(UserStore UserStore, Store Store)> accessibleStores)
        {
            var userStore = accessibleStores.FirstOrDefault(s => s.Store.Id == store.Id).UserStore;
            return new StoreInfoDto
            {
                Id = store.Id,
                Code = store.Code,
                Name = store.Name,
                Type = (int)store.Type,
                TypeName = store.Type.ToString(),
                ParentStoreId = store.ParentStoreId,
                IsPrimary = userStore?.IsPrimary ?? false
            };
        }

        private async Task LogLoginSuccessAsync(User user, long storeId, string sessionId, string ipAddress, string? userAgent, long expiresAt)
        {
            var loginLog = new UserLoginLog
            {
                UserId = user.Id,
                SessionId = sessionId,
                TokenVersion = user.TokenVersion,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceType = ParseDeviceType(userAgent),
                Browser = ParseBrowser(userAgent),
                StoreId = storeId,
                LoginMethod = "Password",
                IsSuccess = true,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime
            };

            await _userLoginLogRepository.AddAsync(loginLog);
            await CommitAsync();
        }

        private async Task LogLoginFailureAsync(long userId, long tenantId, long? storeId, string ipAddress, string? userAgent, string reason)
        {
            var loginLog = new UserLoginLog
            {
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceType = ParseDeviceType(userAgent),
                Browser = ParseBrowser(userAgent),
                StoreId = storeId,
                LoginMethod = "Password",
                IsSuccess = false,
                FailureReason = reason
            };

            await _userLoginLogRepository.AddAsync(loginLog);
            await CommitAsync();
        }
        internal class CurrentIdentityImpl : ICurrentIdentity
        {
            public long? UserId { get; set; }
            public string? UserName { get; set; }
            public long? TenantId { get; set; }
            public long? StoreId { get; set; }
            public bool IsAuthenticated => UserId.HasValue;
            public string? SessionId { get; set; }
        }

        #endregion
    }
}