// Infrastructure/MySql56DesignTimeDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Atlas.Integration.Tests.Infrastructure
{
    /// <summary>
    /// 用于生成迁移的设计时工厂
    /// </summary>
    public class MySql56DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
    {
        public TestDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

            var serverVersion = new MySqlServerVersion(new Version(5, 6, 51));

            optionsBuilder.UseMySql(
                "Server=localhost;Database=testdb;User=root;Password=root;",
                serverVersion,
                options =>
                {
                    options.SchemaBehavior(MySqlSchemaBehavior.Ignore);
                }
            );

            return new TestDbContext(optionsBuilder.Options);
        }
    }
}