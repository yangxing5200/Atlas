using Atlas.Core.Entities.Global;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Global.Repositories.Impl
{
    public class TenantRepository : GlobalRepositoryBase<Tenant>, ITenantRepository
    {
        public TenantRepository(AtlasGlobalDbContext context) : base(context)
        {
        }
    }
}
