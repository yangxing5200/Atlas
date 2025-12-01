namespace Atlas.Models.Requests
{
    /// <summary>
    /// 记录操作日志请求
    /// </summary>
    public class LogOperationRequest
    {
        /// <summary>
        /// 租户ID
        /// </summary>
        public long TenantId { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public long? UserId { get; set; }

        /// <summary>
        /// 门店ID
        /// </summary>
        public long? StoreId { get; set; }

        /// <summary>
        /// 会话ID
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// 操作模块
        /// </summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// 操作描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 关联的业务实体ID
        /// </summary>
        public long? EntityId { get; set; }

        /// <summary>
        /// 变更数据（JSON格式）
        /// </summary>
        public string? Changes { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
