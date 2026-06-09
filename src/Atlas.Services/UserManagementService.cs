using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public sealed class UserManagementService : ServiceBase, IUserManagementService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserStore> _userStoreRepository;
    private readonly IUserPasswordService _userPasswordService;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IRepository<User> userRepository,
        IRepository<UserStore> userStoreRepository,
        IUnitOfWork unitOfWork,
        IUserPasswordService userPasswordService,
        ILogger<UserManagementService> logger)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userStoreRepository = userStoreRepository;
        _userPasswordService = userPasswordService;
        _logger = logger;
    }

    public async Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var queryBuilder = await _userRepository.QueryAsync();
            var exists = await queryBuilder
                .Where(x => x.UserName == request.UserName && x.IsDeleted == false)
                .AnyAsync();

            if (exists)
            {
                return OperationResult<UserDto>.Failed("用户名已存在");
            }

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
            await _userRepository.AddAsync(user);
            await CommitAsync();

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

            return OperationResult<UserDto>.Succeed(UserDtoMapper.ToDto(user), "用户创建成功");
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
            var queryBuilder = await _userRepository.QueryTrackingAsync();
            var user = await queryBuilder
                .Where(u => u.Id == request.Id && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return OperationResult<UserDto>.Failed("用户不存在");
            }

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
            return OperationResult<UserDto>.Succeed(UserDtoMapper.ToDto(user), "用户更新成功");
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
            var builder = await _userRepository.QueryTrackingAsync();
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
        var queryBuilder = await _userRepository.QueryAsync();
        var user = await queryBuilder
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Include(u => u.DefaultStore)
            .Include(u => u.UserStores)
            .ThenInclude<UserStore, Store>(us => us.Store)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return null;
        }

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
        var queryBuilder = await _userRepository.QueryAsync();
        var user = await queryBuilder
            .Where(u => u.UserName == userName && !u.IsDeleted)
            .Include(u => u.DefaultStore)
            .FirstOrDefaultAsync();

        return user == null ? null : UserDtoMapper.ToDto(user);
    }

    public async Task<UserPagedResponse> GetUsersAsync(UserQueryRequest request)
    {
        var queryBuilder = await _userRepository.QueryAsync();
        var query = queryBuilder.Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(u =>
                u.UserName.Contains(request.Keyword) ||
                u.RealName.Contains(request.Keyword) ||
                (u.Phone != null && u.Phone.Contains(request.Keyword)));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(u => u.Type == request.Type.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(u => u.Status == request.Status.Value);
        }

        if (request.StoreId.HasValue)
        {
            query = query.Where(u => u.UserStores.Any(us => us.StoreId == request.StoreId.Value));
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(u => u.DepartmentId == request.DepartmentId.Value);
        }

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
            Items = users.Select(UserDtoMapper.ToDto).ToList(),
            Total = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request)
    {
        return _userPasswordService.ChangePasswordAsync(userId, request);
    }

    public Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _userPasswordService.ResetPasswordAsync(request);
    }

    public async Task<OperationResult> SetUserStatusAsync(long userId, bool isActive)
    {
        try
        {
            var queryBuilder = await _userRepository.QueryTrackingAsync();
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
            var queryBuilder = await _userRepository.QueryTrackingAsync();
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
}
