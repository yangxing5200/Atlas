using Atlas.Core.Entities;
using Atlas.Core.Enums;

namespace Atlas.Data.Tenant.Entities
{
    public class Store : VersionedEntity, ITenantEntity
    {
        public long TenantId { get; set; }
        public string Name { get; set; }
        public StoreType Type { get; set; }

        /// <summary>
        /// 上级门店ID
        /// NULL: 根节点（平台总部）
        /// 0: 平台直营门店的上级是平台总部
        /// 具体值: 加盟商门店的上级是加盟商总部
        /// </summary>
        public long? ParentStoreId { get; set; }

        public bool IsActive { get; set; }
        public string Address { get; set; }
        public string ContactPhone { get; set; }
        public string ContactPerson { get; set; }

        // 导航属性
        public virtual Store ParentStore { get; set; }
        public virtual ICollection<Store> ChildStores { get; set; }
    }
}
