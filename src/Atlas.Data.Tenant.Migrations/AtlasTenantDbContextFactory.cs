using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;
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

        private const string EntityConfigurationAssembliesEnvVar = "ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES";
        
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

            var context = new AtlasTenantDbContext(
                optionsBuilder.Options,
                GetEntityConfigurationAssemblies());
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
            catch (Exception)
            {
                // Configuration files may not exist in design-time scenario (EF migrations).
                // This is expected - fall through to default connection string.
            }
            
            // 3. Fall back to default for local development
            return DefaultConnectionString;
        }

        private static IReadOnlyCollection<Assembly> GetEntityConfigurationAssemblies()
        {
            var assemblyNames = new List<string>();

            var envAssemblies = Environment.GetEnvironmentVariable(EntityConfigurationAssembliesEnvVar);
            if (!string.IsNullOrWhiteSpace(envAssemblies))
            {
                assemblyNames.AddRange(SplitAssemblyNames(envAssemblies));
            }

            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build();

                assemblyNames.AddRange(configuration
                    .GetSection("Atlas:TenantEntityConfigurationAssemblies")
                    .Get<string[]>() ?? Array.Empty<string>());
            }
            catch (Exception)
            {
                // Design-time migration runs may not have application configuration available.
            }

            return assemblyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(Assembly.Load)
                .ToArray();
        }

        private static IEnumerable<string> SplitAssemblyNames(string value)
        {
            return value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
