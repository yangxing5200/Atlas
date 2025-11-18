using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Migrations
{
    public class AtlasTenantDbContextFactory : IDesignTimeDbContextFactory<AtlasTenantDbContext>
    {
        public AtlasTenantDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();
            optionsBuilder.UseMySql("Server=localhost;Database=atlas;User=root;Password=root;",
             new MySqlServerVersion(new Version(5, 7, 32)),
             options =>
             {
                 // 指定迁移程序集
                 options.MigrationsAssembly("Atlas.Data.Tenant.Migrations");
             });

            var context = new AtlasTenantDbContext(optionsBuilder.Options, SystemIdentity.Migration);
            return context;
        }
    }
}
