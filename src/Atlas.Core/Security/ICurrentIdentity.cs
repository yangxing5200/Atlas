namespace Atlas.Core.Services
{
    /// <summary>
    /// 当前用户服务接口
    /// </summary>
    public interface ICurrentIdentity
    {
        /// <summary>
        /// 当前用户ID
        /// </summary>
        long? UserId { get; }

        /// <summary>
        /// 当前用户名
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// 当前门店ID
        /// </summary>
        long? StoreId { get; }

        /// <summary>
        /// 当前租户ID
        /// </summary>
        long? TenantId { get; }

        /// <summary>
        /// 是否已认证
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// 会话ID（Session ID / Request ID）
        /// 用途：
        /// 1. 关联单次登录的所有请求
        /// 2. 用于查询完整的登录上下文（设备信息、IP等）
        /// 3. 分布式追踪
        /// 4. 强制登出时可以撤销整个会话
        /// </summary>
        string? SessionId { get; }
    }
}
