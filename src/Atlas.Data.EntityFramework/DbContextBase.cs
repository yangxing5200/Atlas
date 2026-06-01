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
    /// DbContext 基类，统一承载所有数据库上下文的模型约定。
    /// </summary>
    /// <remarks>
    /// 这里定义跨模块一致的列类型、精度和时间精度。具体实体映射仍放在各自模块的 Configuration 中。
    /// </remarks>
    public abstract class DbContextBase : DbContext
    {
        protected DbContextBase(DbContextOptions options) : base(options)
        {
        }

        /// <summary>
        /// 配置模型，并为派生上下文预留全局过滤器入口。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 应用全局查询过滤器
            ApplyGlobalFilters(modelBuilder);
        }

        /// <summary>
        /// 应用全局查询过滤器。
        /// </summary>
        /// <remarks>
        /// 派生上下文可在这里集中配置软删除等 EF 全局过滤；租户/门店动态范围当前由仓储查询层处理。
        /// </remarks>
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
