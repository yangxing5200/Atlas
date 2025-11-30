using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Responses;

namespace Atlas.Models.Responses
{
    /// <summary>
    /// 登录响应
    /// </summary>
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string Token { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// 当前登录门店
        /// </summary>
        public StoreInfoDto? CurrentStore { get; set; }

        /// <summary>
        /// 用户可访问的门店列表
        /// </summary>
        public List<StoreInfoDto>? AccessibleStores { get; set; }

        /// <summary>
        /// 是否需要修改密码
        /// </summary>
        public bool RequirePasswordChange { get; set; }
    }



    /// <summary>
    /// 用户分页响应
    /// </summary>
    public class UserPagedResponse : PagedResponse<UserDto>
    {
    }

    /// <summary>
    /// 登录日志分页响应
    /// </summary>
    public class LoginLogPagedResponse : PagedResponse<UserLoginLogDto>
    {
    }

    /// <summary>
    /// 操作结果响应
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }

        public static OperationResult Succeed(string? message = null, object? data = null)
        {
            return new OperationResult
            {
                Success = true,
                Message = message ?? "操作成功",
                Data = data
            };
        }

        public static OperationResult Failed(string message, object? data = null)
        {
            return new OperationResult
            {
                Success = false,
                Message = message,
                Data = data
            };
        }
    }

    /// <summary>
    /// 操作结果响应（泛型）
    /// </summary>
    public class OperationResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static OperationResult<T> Succeed(T data, string? message = null)
        {
            return new OperationResult<T>
            {
                Success = true,
                Message = message ?? "操作成功",
                Data = data
            };
        }

        public static OperationResult<T> Failed(string message)
        {
            return new OperationResult<T>
            {
                Success = false,
                Message = message
            };
        }
    }
}