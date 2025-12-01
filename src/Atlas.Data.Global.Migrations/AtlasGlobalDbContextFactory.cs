using Atlas.Core.Services;
using Atlas.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Global.Migrations
{
    public class AtlasGlobalDbContextFactory : IDesignTimeDbContextFactory<AtlasGlobalDbContext>
    {
        /// <summary>
        /// Default connection string for local development.
        /// </summary>
        private const string DefaultConnectionString = "Server=localhost;Database=atlas_global;User=root;Password=root;";
        
        /// <summary>
        /// Environment variable name for global database connection string.
        /// </summary>
        private const string ConnectionStringEnvVar = "ATLAS_GLOBAL_DB_CONNECTION";
        
        public AtlasGlobalDbContext CreateDbContext(string[] args)
        {
            var connectionString = GetConnectionString();
            
            var optionsBuilder = new DbContextOptionsBuilder<AtlasGlobalDbContext>();
            optionsBuilder.UseMySql(connectionString,
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
                    
                var configConnectionString = configuration.GetConnectionString("AtlasGlobal");
                if (!string.IsNullOrWhiteSpace(configConnectionString))
                {
                    return configConnectionString;
                }
            }
            catch (Exception)
            {
                // Configuration files may not exist in design-time scenario (EF migrations).
                // This is expected - fall through to default connection string.
            }
            
            // 3. Fall back to default for local development
            return DefaultConnectionString;
        }
    }
}
