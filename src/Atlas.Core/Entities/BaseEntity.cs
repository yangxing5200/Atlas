using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.IdGenerators;

namespace Atlas.Core.Entities
{
    /// <summary>
    /// 所有实体的基类
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// 主键ID（Snowflake生成的long型）
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 从ID中解析创建时间（用于验证）
        /// </summary>
        public DateTime GetIdTimestamp()
        {
            return SnowflakeIdGenerator.ParseTimestamp(Id);
        }
    }


    /// <summary>
    /// 租户实体基类
    /// </summary>
    public abstract class TenantEntity : BaseEntity
    {
        /// <summary>
        /// 租户ID（也使用 Snowflake）
        /// </summary>
        public long TenantId { get; set; }
    }

    /// <summary>
    /// 可审计实体基类
    /// </summary>
    public abstract class AuditableEntity : BaseEntity
    {
        /// <summary>
        /// 创建人ID
        /// </summary>
        public long? CreatedBy { get; set; }

        /// <summary>
        /// 更新人ID
        /// </summary>
        public long? UpdatedBy { get; set; }

        /// <summary>
        /// 是否已删除
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// 删除人ID
        /// </summary>
        public long? DeletedBy { get; set; }
    }

    public abstract class VersionedEntity : AuditableEntity
    {
        /// <summary>
        /// 版本号（用于乐观锁）
        /// </summary>
        public int Version { get; set; }
    }

    public abstract class TenantVersionedEntity : VersionedEntity
    {
        public long TenantId { get; set; }
    }
}
