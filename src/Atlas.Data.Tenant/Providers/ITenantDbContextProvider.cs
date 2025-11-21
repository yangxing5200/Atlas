using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Data.Tenant.Context;

namespace Atlas.Data.Tenant.Providers
{
    public interface ITenantDbContextProvider
    {
        Task<AtlasTenantDbContext> GetWriteDbContext();
        Task<AtlasTenantDbContext> GetReadDbContext();
    }
}
