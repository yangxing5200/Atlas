using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Security.Permissions;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public sealed class UserAssignmentService : ServiceBase, IUserAssignmentService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserStore> _userStoreRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly ITokenCacheService _tokenCacheService;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IUserSecurityAuditWriter _securityAuditWriter;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly ILogger<UserAssignmentService> _logger;

    public UserAssignmentService(
        IRepository<User> userRepository,
        IRepository<UserStore> userStoreRepository,
        IRepository<UserRole> userRoleRepository,
        IUnitOfWork unitOfWork,
        ITokenCacheService tokenCacheService,
        IPermissionChecker permissionChecker,
        IUserSecurityAuditWriter securityAuditWriter,
        ICurrentIdentity currentIdentity,
        ILogger<UserAssignmentService> logger)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userStoreRepository = userStoreRepository;
        _userRoleRepository = userRoleRepository;
        _tokenCacheService = tokenCacheService;
        _permissionChecker = permissionChecker;
        _securityAuditWriter = securityAuditWriter;
        _currentIdentity = currentIdentity;
        _logger = logger;
    }

    public async Task<OperationResult> AssignStoresAsync(AssignStoresRequest request)
    {
        try
        {
            var userBuilder = await _userRepository.QueryTrackingAsync();
            var user = await userBuilder
                .Where(u => u.Id == request.UserId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return OperationResult.Failed("用户不存在");
            }

            var queryBuilder = await _userStoreRepository.QueryTrackingAsync(user.TenantId);
            var oldStores = await queryBuilder
                .Where(us => us.UserId == request.UserId)
                .ToListAsync();

            await _userStoreRepository.RemoveRangeAsync(oldStores, user.TenantId);

            var newStores = request.Stores.Select(store => new UserStore
            {
                TenantId = user.TenantId,
                UserId = request.UserId,
                StoreId = store.StoreId,
                IsPrimary = store.IsPrimary,
                Permission = store.Permission,
                EffectiveFrom = store.EffectiveFrom,
                EffectiveTo = store.EffectiveTo
            }).ToList();

            await _userStoreRepository.AddRangeAsync(newStores, user.TenantId);
            user.InvalidateAllTokens();
            await UnitOfWork.SaveChangesAsync(user.TenantId);
            await _tokenCacheService.SetUserTokenVersionAsync(request.UserId, user.TokenVersion);
            await _permissionChecker.InvalidateUserPermissionsAsync(user.TenantId, user.Id);
            await _securityAuditWriter.WriteAsync(
                user.TenantId,
                user.Id,
                null,
                "AssignStores",
                AuditEventOutcome.Succeeded,
                metadata: $"StoreCount={newStores.Count}");

            return OperationResult.Succeed("门店分配成功，用户需要重新登录或重新获取Token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分配门店失败: UserId={UserId}", request.UserId);
            return OperationResult.Failed("分配门店失败");
        }
    }

    public async Task<OperationResult> AssignRolesAsync(AssignRolesRequest request)
    {
        try
        {
            var userBuilder = await _userRepository.QueryTrackingAsync();
            var user = await userBuilder
                .Where(u => u.Id == request.UserId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return OperationResult.Failed("用户不存在");
            }

            var roleBuilder = await _userRoleRepository.QueryTrackingAsync(user.TenantId);
            var existingRoles = await roleBuilder
                .Where(x => x.TenantId == user.TenantId
                            && x.UserId == request.UserId
                            && x.StoreId == request.StoreId)
                .ToListAsync();

            await _userRoleRepository.RemoveRangeAsync(existingRoles, user.TenantId);

            var newRoles = request.RoleIds
                .Distinct()
                .Select(roleId => new UserRole
                {
                    TenantId = user.TenantId,
                    UserId = request.UserId,
                    RoleId = roleId,
                    StoreId = request.StoreId,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = _currentIdentity.UserId ?? 0
                })
                .ToList();

            if (newRoles.Count > 0)
            {
                await _userRoleRepository.AddRangeAsync(newRoles, user.TenantId);
            }

            await UnitOfWork.SaveChangesAsync(user.TenantId);
            await _permissionChecker.InvalidateUserPermissionsAsync(
                user.TenantId,
                user.Id,
                request.StoreId == 0 ? null : request.StoreId);
            await _securityAuditWriter.WriteAsync(
                user.TenantId,
                user.Id,
                request.StoreId == 0 ? null : request.StoreId,
                "AssignRoles",
                AuditEventOutcome.Succeeded,
                metadata: $"RoleCount={newRoles.Count}");

            return OperationResult.Succeed("角色分配成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分配角色失败: UserId={UserId}", request.UserId);
            await _securityAuditWriter.WriteAsync(
                _currentIdentity.TenantId.GetValueOrDefault(),
                request.UserId,
                request.StoreId == 0 ? null : request.StoreId,
                "AssignRoles",
                AuditEventOutcome.Failed,
                errorMessage: ex.Message);
            return OperationResult.Failed("分配角色失败");
        }
    }
}
