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

            // 2. 种子其他数据...
            // await SeedProductsAsync();
            // await SeedCustomersAsync();

            _logger?.LogInformation("租户 {TenantId} 数据库种子数据初始化完成！", _tenantId);
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