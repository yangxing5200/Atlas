using Atlas.Core.Entities.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations
{
    /// <summary>
    /// 数据库服务器配置
    /// </summary>
    public class DatabaseServerConfigConfiguration : IEntityTypeConfiguration<DatabaseServerConfig>
    {
        public void Configure(EntityTypeBuilder<DatabaseServerConfig> builder)
        {
            // 表名
            builder.ToTable("DatabaseServerConfigs");

            // 主键
            builder.HasKey(e => e.Id);

            // 唯一索引（同一ServerCode在同一NetworkEnv和DbType下只能有一条配置）
            builder.HasIndex(e => new { e.ServerCode, e.NetworkEnvCode, e.DbType })
                .IsUnique()
                .HasDatabaseName("UX_DatabaseServerConfigs_ServerCode_NetworkEnvCode_DbType");

            // 普通索引
            builder.HasIndex(e => e.ServerCode)
                .HasDatabaseName("IX_DatabaseServerConfigs_ServerCode");

            builder.HasIndex(e => e.NetworkEnvCode)
                .HasDatabaseName("IX_DatabaseServerConfigs_NetworkEnvCode");

            // 属性配置
            builder.Property(e => e.ServerCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("服务器编码（关联MasterServer或ReadonlyServer）");

            builder.Property(e => e.NetworkEnvCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(NetworkEnvCodes.Default)
                .HasComment("网络环境编码：default, classic, vpc");

            builder.Property(e => e.DbType)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("数据库类型：SqlServer, MySQL, PostgreSQL");

            builder.Property(e => e.ConnString)
                .IsRequired()
                .HasMaxLength(500)
                .HasComment("数据库连接串");

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasComment("创建时间");

            builder.Property(e => e.UpdatedAt)
                .HasComment("更新时间");
        }
    }
}
