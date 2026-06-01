using Atlas.Core.Entities.Base;
using Atlas.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.DTOs
{
    public class StoreDto: VersionedDto
    {
        public long TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StoreType Type { get; set; }

        /// <summary>
        /// 上级门店ID
        /// NULL: 根节点（平台总部）
        /// 0: 平台直营门店的上级是平台总部
        /// 具体值: 加盟商门店的上级是加盟商总部
        /// </summary>
        public long? ParentStoreId { get; set; }

        public bool IsActive { get; set; }
        public string Address { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        /// <summary>
        /// 省份
        /// </summary>
        public string Province { get; set; } = string.Empty;

        /// <summary>
        /// 城市
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// 区县
        /// </summary>
        public string District { get; set; } = string.Empty;
        public StoreStatus Status { get; set; }
    }

    /// <summary>
    /// 门店简要信息
    /// </summary>
    public class StoreInfoDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public long? ParentStoreId { get; set; }
        public bool IsPrimary { get; set; }
    }
}
