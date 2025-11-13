using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.IdGenerators;

namespace Atlas.Core.Entities
{
    public abstract class BaseEntity : IBaseEntity
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public DateTime GetIdTimestamp() => SnowflakeIdGenerator.ParseTimestamp(Id);
    }

    public abstract class TenantEntity : BaseEntity, ITenantEntity
    {
        public long TenantId { get; set; }
    }

    public abstract class StoreEntity : BaseEntity, IStoreEntity
    {
        public long StoreId { get; set; }
    }

    public abstract class AuditableEntity : BaseEntity, IAuditable, ISoftDelete
    {
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }

    public abstract class VersionedEntity : AuditableEntity, IVersioned
    {
        public int Version { get; set; }
    }

    public abstract class TenantVersionedEntity : VersionedEntity, ITenantEntity
    {
        public long TenantId { get; set; }
    }

    public abstract class TenantStoreVersionedEntity : VersionedEntity, ITenantEntity, IStoreEntity
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
    }
}
