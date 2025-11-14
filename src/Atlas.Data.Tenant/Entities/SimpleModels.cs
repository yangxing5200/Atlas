using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.Enums;

namespace Atlas.Data.Tenant.Entities
{
        // 示例1：商品 - 共享数据
        public class Product : SharedVersionedEntity
        {
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string Description { get; set; }

            // 可选：数据溯源字段
            public long? SourceStoreId { get; set; }  // 来源门店（分发场景）
            public bool IsCustomized { get; set; }    // 是否门店自定义
        }

        // 示例2：会员 - 共享数据
        public class Member : SharedVersionedEntity
        {
            public string MemberName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public int Points { get; set; }
        }

        // 示例3：促销活动 - 共享数据
        public class Promotion : SharedVersionedEntity
        {
            public string Title { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public decimal DiscountRate { get; set; }

            // 可选：数据溯源
            public long? SourceStoreId { get; set; }
        }

        // 示例4：订单 - 门店独享
        public class Order : StoreOnlyVersionedEntity
        {
            public string OrderNo { get; set; }
            public long MemberId { get; set; }
            public decimal TotalAmount { get; set; }
            public OrderStatus Status { get; set; }
        }

        // 示例5：库存 - 门店独享
        public class Inventory : StoreOnlyEntity
        {
            public long ProductId { get; set; }
            public int Quantity { get; set; }
            public int SafetyStock { get; set; }
        }

        // 示例6：收银记录 - 门店独享
        public class CashierRecord : StoreOnlyEntity
        {
            public long OrderId { get; set; }
            public decimal Amount { get; set; }
            public PaymentMethod PaymentMethod { get; set; }
            public DateTime PaidAt { get; set; }
        }
}
