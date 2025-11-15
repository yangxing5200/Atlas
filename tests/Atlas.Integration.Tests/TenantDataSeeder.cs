using Atlas.Core.Enums;
using Atlas.Data.Common;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Data.Tenant.Seeds
{
    /// <summary>
    /// Tenant 数据库种子数据
    /// </summary>
    public class TenantDataSeeder
    {
        private readonly AtlasTenantDbContext _context;
        private readonly long _tenantId;
        private readonly ILogger<TenantDataSeeder>? _logger;

        public TenantDataSeeder(
            AtlasTenantDbContext context,
            long tenantId,
            ILogger<TenantDataSeeder>? logger = null)
        {
            _context = context;
            _tenantId = tenantId;
            _logger = logger;
        }

        /// <summary>
        /// 执行种子数据初始化
        /// </summary>
        public async Task SeedAsync()
        {
            _logger?.LogInformation("开始初始化租户 {TenantId} 的数据库种子数据...", _tenantId);

            // 1. 种子门店数据
            await SeedStoresAsync();

            // 2. 种子其他数据...
            // await SeedProductsAsync();
            // await SeedCustomersAsync();

            _logger?.LogInformation("租户 {TenantId} 数据库种子数据初始化完成！", _tenantId);
        }

        /// <summary>
        /// 种子门店数据
        /// </summary>
        private async Task SeedStoresAsync()
        {
            _logger?.LogInformation("初始化门店数据...");

            // 检查是否已存在
            if (await _context.Stores.AnyAsync())
            {
                _logger?.LogInformation("门店数据已存在，跳过初始化");
                return;
            }

            var stores = new[]
            {
                // 1. 总部
                new Store
                {
                    Id = 1,
                    TenantId = _tenantId,
                    Code = "HQ001",
                    Name = "总部",
                    Type = StoreType.Headquarters,
                    ParentStoreId = null,
                    Province = "上海",
                    City = "上海",
                    District = "浦东新区",
                    Address = "张江高科技园区 999 号",
                    ContactPerson = "总部经理",
                    ContactPhone = "021-12345678",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                
                // 2. 直营门店
                new Store
                {
                    Id = 2,
                    TenantId = _tenantId,
                    Code = "ZY001",
                    Name = "浦东直营店",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "浦东新区",
                    Address = "世纪大道 888 号",
                    ContactPerson = "李经理",
                    ContactPhone = "021-88888888",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Store
                {
                    Id = 3,
                    TenantId = _tenantId,
                    Code = "ZY002",
                    Name = "徐汇直营店",
                    Type = StoreType.DirectOperated,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "徐汇区",
                    Address = "徐家汇 666 号",
                    ContactPerson = "王经理",
                    ContactPhone = "021-66666666",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                
                // 3. 加盟门店
                new Store
                {
                    Id = 4,
                    TenantId = _tenantId,
                    Code = "JM001",
                    Name = "虹口加盟店",
                    Type = StoreType.Franchised,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "虹口区",
                    Address = "四川北路 555 号",
                    ContactPerson = "张老板",
                    ContactPhone = "021-55555555",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                },
                new Store
                {
                    Id = 5,
                    TenantId = _tenantId,
                    Code = "JM002",
                    Name = "杨浦加盟店",
                    Type = StoreType.Franchised,
                    ParentStoreId = 1,
                    Province = "上海",
                    City = "上海",
                    District = "杨浦区",
                    Address = "五角场 333 号",
                    ContactPerson = "赵老板",
                    ContactPhone = "021-33333333",
                    Status = StoreStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SystemIdentity.Seed.UserId
                }
            };

            await _context.Stores.AddRangeAsync(stores);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("门店数据初始化完成：{Count} 个门店", stores.Length);
        }

        /// <summary>
        /// 种子产品数据（示例）
        /// </summary>
        private async Task SeedProductsAsync()
        {
            _logger?.LogInformation("初始化产品数据...");

            // TODO: 添加产品种子数据

            _logger?.LogInformation("产品数据初始化完成");
        }

        /// <summary>
        /// 种子客户数据（示例）
        /// </summary>
        private async Task SeedCustomersAsync()
        {
            _logger?.LogInformation("初始化客户数据...");

            // TODO: 添加客户种子数据

            _logger?.LogInformation("客户数据初始化完成");
        }
    }
}