using Atlas.Core.Services;
using Atlas.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Global.Migrations
{
    public class AtlasGlobalDbContextFactory : IDesignTimeDbContextFactory<AtlasGlobalDbContext>
    {
        public AtlasGlobalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AtlasGlobalDbContext>();
            optionsBuilder.UseMySql("Server=localhost;Database=atlas_global;User=root;Password=root;",
             new MySqlServerVersion(new Version(5, 7, 32)),
             options =>
             {
                 // 指定迁移程序集
                 options.MigrationsAssembly("Atlas.Data.Global.Migrations");
             });

            var context = new AtlasGlobalDbContext(optionsBuilder.Options, SystemIdentity.Migration);
            //context.Database.Migrate();
            return context;
        }
    }
}
