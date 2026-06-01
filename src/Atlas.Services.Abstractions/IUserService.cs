using Atlas.Core.Entities.Tenant;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services.Abstractions
{
    public interface IUserService : IServiceBase<User, UserDto>
    {
        /// <summary>
        /// 用户登录
        /// </summary>
        Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent);

        Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string? userAgent);

        /// <summary>
        /// 用户登出
        /// </summary>
        Task<OperationResult> LogoutAsync(string sessionId);

        /// <summary>
        /// 创建用户
        /// </summary>
        Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequest request);

        /// <summary>
        /// 更新用户
        /// </summary>
        Task<OperationResult<UserDto>> UpdateUserAsync(UpdateUserRequest request);

        /// <summary>
        /// 删除用户（软删除）
        /// </summary>
        Task<OperationResult> DeleteUserAsync(long userId);

        /// <summary>
        /// 获取用户详情
        /// </summary>
        Task<UserDetailDto?> GetUserByIdAsync(long userId);

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        Task<UserDto?> GetUserByUserNameAsync(string userName);

        /// <summary>
        /// 分页查询用户
        /// </summary>
        Task<UserPagedResponse> GetUsersAsync(UserQueryRequest request);

        /// <summary>
        /// 修改密码
        /// </summary>
        Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request);

        /// <summary>
        /// 重置密码（管理员操作）
        /// </summary>
        Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request);

        /// <summary>
        /// 分配门店
        /// </summary>
        Task<OperationResult> AssignStoresAsync(AssignStoresRequest request);

        Task<OperationResult> AssignRolesAsync(AssignRolesRequest request);

        /// <summary>
        /// 启用/禁用用户
        /// </summary>
        Task<OperationResult> SetUserStatusAsync(long userId, bool isActive);

        /// <summary>
        /// 解锁用户
        /// </summary>
        Task<OperationResult> UnlockUserAsync(long userId);

        /// <summary>
        /// 获取用户登录日志
        /// </summary>
        Task<LoginLogPagedResponse> GetLoginLogsAsync(LoginLogQueryRequest request);

        /// <summary>
        /// 强制用户下线（撤销所有Token）
        /// </summary>
        Task<OperationResult> ForceLogoutAllAsync(long userId);

        /// <summary>
        /// 获取在线用户会话
        /// </summary>
        Task<List<UserLoginLogDto>> GetActiveSessionsAsync(long userId);

        /// <summary>
        /// 切换门店
        /// </summary>
        Task<SwitchStoreResponse> SwitchStoreAsync(long userId, SwitchStoreRequest request);

        /// <summary>
        /// 获取用户可访问的门店列表
        /// </summary>
        Task<List<StoreInfoDto>> GetAccessibleStoresAsync(long userId);
    }
}
