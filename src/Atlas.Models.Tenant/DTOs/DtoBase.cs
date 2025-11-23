using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.DTOs
{

    /// <summary>
    /// 基础 DTO 对应 BaseEntity
    /// 包含 Id, CreatedAt, UpdatedAt
    /// </summary>
    public abstract class DtoBase
    {
        public long Id { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 可审计 DTO 对应 AuditableEntity
    /// 包含 CreatedBy / UpdatedBy
    /// </summary>
    public abstract class AuditableDto : DtoBase
    {
        public long? CreatedBy { get; set; }
        public string ? CreatedByName { get; set; }
        public long? UpdatedBy { get; set; }
        public string ? UpdatedByName { get; set; }
    }

    /// <summary>
    /// 版本化 DTO 对应 VersionedEntity
    /// </summary>
    public abstract class VersionedDto : AuditableDto
    {
        public int Version { get; set; }
    }

    /// <summary>
    /// 共享数据 DTO 对应 SharedEntity
    /// </summary>
    public abstract class SharedDto : AuditableDto
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
        public string? StoreName { get; set; }
    }

    /// <summary>
    /// 共享数据 DTO（带版本控制）对应 SharedVersionedEntity
    /// </summary>
    public abstract class SharedVersionedDto : VersionedDto
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
        public string? StoreName { get; set; }
    }

    /// <summary>
    /// 门店独享数据 DTO 对应 StoreOnlyEntity
    /// </summary>
    public abstract class StoreOnlyDto : AuditableDto
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
        public string? StoreName { get; set; }
    }

    /// <summary>
    /// 门店独享数据 DTO（带版本控制）对应 StoreOnlyVersionedEntity
    /// </summary>
    public abstract class StoreOnlyVersionedDto : VersionedDto
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
        public string? StoreName { get; set; }
    }
}


