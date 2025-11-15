using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Atlas.Data.Common
{
    /// <summary>
    /// DbContext 基类，提供通用功能
    /// </summary>
    public abstract class DbContextBase : DbContext
    {
        protected DbContextBase(DbContextOptions options) : base(options)
        {
        }

        /// <summary>
        /// 配置模型（应用全局查询过滤器）
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 应用全局查询过滤器
            ApplyGlobalFilters(modelBuilder);

            // 应用所有配置
            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        }

        /// <summary>
        /// 应用全局查询过滤器
        /// </summary>
        protected virtual void ApplyGlobalFilters(ModelBuilder modelBuilder)
        {
         
        }

        /// <summary>
        /// 配置约定
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // 配置字符串默认长度
            configurationBuilder.Properties<string>()
                .HaveMaxLength(256);

            // 配置 decimal 精度
            configurationBuilder.Properties<decimal>()
                .HavePrecision(18, 2);

            // 配置 DateTime 类型（使用 UTC）
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<long>();
        }
    }
}