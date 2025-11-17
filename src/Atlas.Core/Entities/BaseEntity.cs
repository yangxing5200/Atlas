using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.IdGenerators;

namespace Atlas.Core.Entities
{
    /// <summary>
    /// 基础实体（默认使用数据库自增ID）
    /// 如需使用雪花算法生成分布式ID，请在具体实体类上实现 ISnowflakeId 接口
    /// </summary>
    public abstract class BaseEntity : IBaseEntity
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 可审计实体（默认使用数据库自增ID）
    /// </summary>
    public abstract class AuditableEntity : BaseEntity, IAuditable, ISoftDelete
    {
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }

    /// <summary>
    /// 版本化实体（默认使用数据库自增ID）
    /// </summary>
    public abstract class VersionedEntity : AuditableEntity, IVersioned
    {
        public int Version { get; set; }
    }

    /// <summary>
    /// 共享数据实体基类（默认使用数据库自增ID）
    /// 适用场景：商品、会员、促销、优惠券等需要在门店间共享的数据
    /// </summary>
    public abstract class SharedEntity : AuditableEntity, ISharedEntity
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
    }

    /// <summary>
    /// 共享数据实体基类（带版本控制，默认使用数据库自增ID）
    /// </summary>
    public abstract class SharedVersionedEntity : VersionedEntity, ISharedEntity
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
    }

    /// <summary>
    /// 门店独享数据实体基类（默认使用数据库自增ID）
    /// 适用场景：订单、库存、收银记录等门店独立经营数据
    /// </summary>
    public abstract class StoreOnlyEntity : AuditableEntity, IStoreOnlyEntity
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
    }

    /// <summary>
    /// 门店独享数据实体基类（带版本控制，默认使用数据库自增ID）
    /// </summary>
    public abstract class StoreOnlyVersionedEntity : VersionedEntity, IStoreOnlyEntity
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
    }
}
