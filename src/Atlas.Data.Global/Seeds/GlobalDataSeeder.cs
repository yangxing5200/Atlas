using Atlas.Core.Enums;
using Atlas.Data.Common;
using Atlas.Models.Global.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Data.Global.Seeds
{
    /// <summary>
    /// Global 数据库种子数据
    /// </summary>
    public class GlobalDataSeeder
    {
        private readonly AtlasGlobalDbContext _context;
        private readonly ILogger<GlobalDataSeeder>? _logger;

        public GlobalDataSeeder(
            AtlasGlobalDbContext context,
            ILogger<GlobalDataSeeder>? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 执行种子数据初始化
        /// </summary>
        public async Task SeedAsync()
        {
            _logger?.LogInformation("开始初始化 Global 数据库种子数据...");

            // 1. 种子数据库服务器配置
            await SeedDatabaseServersAsync();

            // 2. 种子数据库实例
            await SeedDatabaseInstancesAsync();

            // 3. 种子租户数据
            await SeedTenantsAsync();

            _logger?.LogInformation("Global 数据库种子数据初始化完成！");
        }

        /// <summary>
        /// 种子数据库服务器配置
        /// </summary>
        private async Task SeedDatabaseServersAsync()
        {
            _logger?.LogInformation("初始化数据库服务器配置...");

            // 检查是否已存在
            if (await _context.DatabaseMasterServers.AnyAsync())
            {
                _logger?.LogInformation("数据库服务器配置已存在，跳过初始化");
                return;
            }

            // 1. 主数据库服务器
            var masterServers = new[]
            {
                new DatabaseMasterServer
                {
                    Id = 1,
                    Code = "master-01",
                    NickName = "主数据库服务器-01（华东）",
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseMasterServer
                {
                    Id = 2,
                    Code = "master-02",
                    NickName = "主数据库服务器-02（华北）",
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseMasterServer
                {
                    Id = 3,
                    Code = "master-03",
                    NickName = "主数据库服务器-03（华南）",
                    CreatedAt = DateTime.UtcNow
                }
            };

            await _context.DatabaseMasterServers.AddRangeAsync(masterServers);

            // 2. 只读数据库服务器
            var readonlyServers = new[]
            {
                new DatabaseReadonlyServer
                {
                    Id = 1,
                    Code = "readonly-01-01",
                    NickName = "只读服务器-01-01（华东-普通）",
                    MasterServerCode = "master-01",
                    IsReport = false,
                    IsPublic = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseReadonlyServer
                {
                    Id = 2,
                    Code = "readonly-01-02",
                    NickName = "只读服务器-01-02（华东-报表）",
                    MasterServerCode = "master-01",
                    IsReport = true,
                    IsPublic = false,
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseReadonlyServer
                {
                    Id = 3,
                    Code = "readonly-02-01",
                    NickName = "只读服务器-02-01（华北-普通）",
                    MasterServerCode = "master-02",
                    IsReport = false,
                    IsPublic = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseReadonlyServer
                {
                    Id = 4,
                    Code = "readonly-02-02",
                    NickName = "只读服务器-02-02（华北-报表）",
                    MasterServerCode = "master-02",
                    IsReport = true,
                    IsPublic = false,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await _context.DatabaseReadonlyServers.AddRangeAsync(readonlyServers);

            // 3. 数据库服务器配置
            var serverConfigs = new[]
            {
                // master-01 配置
                new DatabaseServerConfig
                {
                    Id = 1,
                    ServerCode = "master-01",
                    NetworkEnvCode = NetworkEnvCodes.Default,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseServerConfig
                {
                    Id = 2,
                    ServerCode = "master-01",
                    NetworkEnvCode = NetworkEnvCodes.Vpc,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                },
                // readonly-01-01 配置
                new DatabaseServerConfig
                {
                    Id = 3,
                    ServerCode = "readonly-01-01",
                    NetworkEnvCode = NetworkEnvCodes.Default,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=readonly;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                },
                // readonly-01-02 配置（报表库）
                new DatabaseServerConfig
                {
                    Id = 4,
                    ServerCode = "readonly-01-02",
                    NetworkEnvCode = NetworkEnvCodes.Default,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=report;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                },
                // readonly-01-01 配置
                new DatabaseServerConfig
                {
                    Id = 5,
                    ServerCode = "readonly-02-01",
                    NetworkEnvCode = NetworkEnvCodes.Default,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=readonly;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                },
                // readonly-01-02 配置（报表库）
                new DatabaseServerConfig
                {
                    Id = 6,
                    ServerCode = "readonly-02-02",
                    NetworkEnvCode = NetworkEnvCodes.Default,
                    DbType = "MySQL",
                    ConnString = "Server=localhost;Port=3306;Database=atlas;User=report;Password=root;CharSet=utf8mb4;",
                    CreatedAt = DateTime.UtcNow
                }
            };

            await _context.DatabaseServerConfigs.AddRangeAsync(serverConfigs);

            await _context.SaveChangesAsync();

            _logger?.LogInformation("数据库服务器配置初始化完成：{MasterCount} 个主服务器，{ReadonlyCount} 个只读服务器",
                masterServers.Length, readonlyServers.Length);
        }

        /// <summary>
        /// 种子数据库实例
        /// </summary>
        private async Task SeedDatabaseInstancesAsync()
        {
            _logger?.LogInformation("初始化数据库实例...");

            // 检查是否已存在
            if (await _context.DatabaseInstances.AnyAsync())
            {
                _logger?.LogInformation("数据库实例已存在，跳过初始化");
                return;
            }

            var instances = new[]
            {
                new DatabaseInstance
                {
                    Id = 1,
                    Name = "华东实例-01",
                    DbType = "MySQL",
                    MasterServerCode = "master-01",
                    DbName = "atlas",
                    Version = "8.0.35",
                    Region = "华东",
                    ConnectionString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4", // 使用 ServerConfig 自动组装
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseInstance
                {
                    Id = 2,
                    Name = "华东实例-02",
                    DbType = "MySQL",
                    MasterServerCode = "master-01",
                    DbName = "atlas",
                    Version = "8.0.35",
                    Region = "华东",
                   ConnectionString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4", // 使用 ServerConfig 自动组装
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseInstance
                {
                    Id = 3,
                    Name = "华北实例-01",
                    DbType = "MySQL",
                    MasterServerCode = "master-02",
                    DbName = "atlas",
                    Version = "8.0.35",
                    Region = "华北",
                    ConnectionString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4", // 使用 ServerConfig 自动组装
                    CreatedAt = DateTime.UtcNow
                },
                new DatabaseInstance
                {
                    Id = 4,
                    Name = "测试实例（独立连接串）",
                    DbType = "MySQL",
                    MasterServerCode = "master-01",
                    DbName = "atlas",
                    Version = "8.0.35",
                    Region = "华东",
                    // 直接指定连接串（优先级高于 ServerConfig）
                    ConnectionString = "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4", // 使用 ServerConfig 自动组装
                    CreatedAt = DateTime.UtcNow
                }
            };

            await _context.DatabaseInstances.AddRangeAsync(instances);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("数据库实例初始化完成：{Count} 个实例", instances.Length);
        }

        /// <summary>
        /// 种子租户数据
        /// </summary>
        private async Task SeedTenantsAsync()
        {
            _logger?.LogInformation("初始化租户数据...");

            // 检查是否已存在
            if (await _context.Tenants.AnyAsync())
            {
                _logger?.LogInformation("租户数据已存在，跳过初始化");
                return;
            }

            var tenants = new[]
            {
                new Tenant
                {
                    Id = 1,
                    Name = "演示公司",
                    BrandName = "演示品牌",
                    Address = "上海市浦东新区张江高科技园区",
                    PhoneNumber = "021-12345678",
                    ContactName = "张三",
                    ContactPhoneNumber = "13800138000",
                    ContactEmail = "zhangsan@demo.com",
                    Domain = "demo",
                    TenantType = TenantType.Enterprise,
                    Province = "上海",
                    City = "上海",
                    Category = Tenant.TenantCategory.Trial,
                    Status = TenantStatus.Active,
                    BusinessType = BusinessType.Chain,
                    DatabaseInstanceId = 1,
                    OfficeCount = 5,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Tenant
                {
                    Id = 2,
                    Name = "测试连锁企业",
                    BrandName = "测试连锁",
                    Address = "北京市海淀区中关村",
                    PhoneNumber = "010-88888888",
                    ContactName = "李四",
                    ContactPhoneNumber = "13900139000",
                    ContactEmail = "lisi@test.com",
                    Domain = "test",
                    TenantType = TenantType.Enterprise,
                    Province = "北京",
                    City = "北京",
                    Status = TenantStatus.Active,
                    BusinessType = BusinessType.Chain,
                    DatabaseInstanceId = 3,
                    OfficeCount = 10,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Tenant
                {
                    Id = 3,
                    Name = "个人版租户",
                    BrandName = "个人诊所",
                    Address = "广州市天河区",
                    PhoneNumber = "020-66666666",
                    ContactName = "王五",
                    ContactPhoneNumber = "13700137000",
                    ContactEmail = "wangwu@mobile.com",
                    Domain = "#M000001",
                    TenantType = TenantType.Individual,
                    Province = "广东",
                    City = "广州",
                    Category = Tenant.TenantCategory.Mobile,
                    Status = TenantStatus.Active,
                    BusinessType = BusinessType.Single,
                    DatabaseInstanceId = 1,
                    OfficeCount = 1,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                }
            };

            await _context.Tenants.AddRangeAsync(tenants);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("租户数据初始化完成：{Count} 个租户", tenants.Length);
        }
    }
}