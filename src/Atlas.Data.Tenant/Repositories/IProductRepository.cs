using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Entities;

namespace Atlas.Data.Tenant.Repositories
{
    // 商品 - 共享数据
    public interface IProductRepository : IRepository<Product>
    { }

    // 会员 - 共享数据
    public interface IMemberRepository : IRepository<Member>
    { }

    // 促销活动 - 共享数据
    public interface IPromotionRepository : IRepository<Promotion>
    { }

    // 订单 - 门店独享
    public interface IOrderRepository : IRepository<Order>
    { }

    // 库存 - 门店独享
    public interface IInventoryRepository : IRepository<Inventory>
    { }

    // 收银记录 - 门店独享
    public interface ICashierRecordRepository : IRepository<CashierRecord>
    { }

}
