using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Core.Entities.Tenant
{
    /// <summary>
    /// 业务操作日志
    /// </summary>
    public class OperationLog : BaseEntity, ITenantEntity, ISnowflakeId
    {
        public long TenantId { get; set; }
        public long? UserId { get; set; }
        public long? StoreId { get; set; }

        /// <summary>
        /// 会话ID（关联到UserLoginLog）
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// 操作模块（Order/Product/Inventory等）
        /// </summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型（Create/Update/Delete/Query等）
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
        /// 变更数据（JSON格式，业务层自行封装）
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
