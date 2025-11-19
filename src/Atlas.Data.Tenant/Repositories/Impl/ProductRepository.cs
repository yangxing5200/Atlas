using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class ProductRepository : RepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
    public class MemberRepository : RepositoryBase<Member>, IMemberRepository
    {
        public MemberRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
    public class PromotionRepository : RepositoryBase<Promotion>, IPromotionRepository
    {
        public PromotionRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
    public class OrderRepository : RepositoryBase<Order>, IOrderRepository
    {
        public OrderRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
    public class InventoryRepository : RepositoryBase<Inventory>, IInventoryRepository
    {
        public InventoryRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
    public class CashierRecordRepository : RepositoryBase<CashierRecord>, ICashierRecordRepository
    {
        public CashierRecordRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }

}
