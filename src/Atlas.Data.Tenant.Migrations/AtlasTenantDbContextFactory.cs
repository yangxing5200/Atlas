using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Migrations
{
    public class AtlasTenantDbContextFactory : IDesignTimeDbContextFactory<AtlasTenantDbContext>
    {
        /// <summary>
        /// Default connection string for local development.
        /// </summary>
        private const string DefaultConnectionString = "Server=localhost;Database=atlas;User=root;Password=root;";
        
        /// <summary>
        /// Environment variable name for tenant database connection string.
        /// </summary>
        private const string ConnectionStringEnvVar = "ATLAS_TENANT_DB_CONNECTION";
        
        public AtlasTenantDbContext CreateDbContext(string[] args)
        {
            var connectionString = GetConnectionString();
            
            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();
            optionsBuilder.UseMySql(connectionString,
             new MySqlServerVersion(new Version(5, 7, 32)),
             options =>
             {
                 // 指定迁移程序集
                 options.MigrationsAssembly("Atlas.Data.Tenant.Migrations");
             });

            var context = new AtlasTenantDbContext(optionsBuilder.Options);
            return context;
        }
        
        /// <summary>
        /// Gets connection string from environment variable, appsettings.json, or falls back to default.
        /// Priority: Environment Variable > appsettings.json > Default
        /// </summary>
        private static string GetConnectionString()
        {
            // 1. Try environment variable first
            var envConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
            if (!string.IsNullOrWhiteSpace(envConnectionString))
            {
                return envConnectionString;
            }
            
            // 2. Try appsettings.json
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build();
                    
                var configConnectionString = configuration.GetConnectionString("AtlasTenant");
                if (!string.IsNullOrWhiteSpace(configConnectionString))
                {
                    return configConnectionString;
                }
            }
            catch
            {
                // Ignore configuration errors in design-time scenario
            }
            
            // 3. Fall back to default for local development
            return DefaultConnectionString;
        }
    }
}
