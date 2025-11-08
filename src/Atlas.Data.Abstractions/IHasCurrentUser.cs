namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// 标记DbContext支持获取当前用户
    /// </summary>
    public interface IHasCurrentUser
    {
        /// <summary>
        /// 当前用户ID
        /// </summary>
        long? CurrentUserId { get; }

        /// <summary>
        /// 当前租户ID
        /// </summary>
        long? CurrentTenantId { get; }
    }
}