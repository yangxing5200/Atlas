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

            configurationBuilder.Properties<string>()
                .HaveMaxLength(256)
                .HaveColumnType("varchar(256)") // 明确指定为 varchar
                .HaveAnnotation("MySql:CharSet", "utf8mb4"); // 确保支持 Emoji 😄

            configurationBuilder.Properties<decimal>()
                .HavePrecision(18, 2);

            // 确保 C# 的时间存入 MySQL 后不会丢失毫秒/微秒，避免并发冲突
            configurationBuilder.Properties<DateTime>()
                .HaveColumnType("datetime(6)");

            configurationBuilder.Properties<DateTime?>()
                .HaveColumnType("datetime(6)");

            // MySQL 使用 tinyint(1) 表示布尔值
            configurationBuilder.Properties<bool>()
                .HaveColumnType("tinyint(1)");
        }
    }
}