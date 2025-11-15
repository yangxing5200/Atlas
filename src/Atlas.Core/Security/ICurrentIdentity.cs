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
        /// 获取可访问的门店ID列表（带缓存）
        /// </summary>
        Task<List<long>> GetAccessibleStoreIdsAsync(CancellationToken ct = default);
    }
}
