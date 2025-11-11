// Infrastructure/LocalMySqlFixture.cs
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace Atlas.Integration.Tests.Infrastructure
{
    /// <summary>
    /// 使用本地 MySQL 实例的 Fixture（无需 Docker）
    /// </summary>
    public class MySqlFixture : IAsyncLifetime
    {
        // 修改为你本地的 MySQL 连接字符串
        public string ConnectionString { get; } =
            "Server=localhost;Port=3306;Database=atlas_cache_test;User=root;Password=root;CharSet=utf8mb4;";

        public async Task InitializeAsync()
        {
            // 测试连接
            try
            {
                using var context = new TestDbContext(CreateDbContextOptions<TestDbContext>());
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法连接到本地 MySQL。请确保 MySQL 正在运行并且连接字符串正确。\n" +
                    $"当前连接字符串: {ConnectionString}\n" +
                    $"错误: {ex.Message}", ex);
            }
        }

        public async Task DisposeAsync()
        {
            try
            {
                using var context = new TestDbContext(CreateDbContextOptions<TestDbContext>());
                await context.Database.EnsureDeletedAsync();
            }
            catch
            {
                // 忽略清理错误
            }
        }

        public DbContextOptions<T> CreateDbContextOptions<T>() where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();

            var serverVersion = new MySqlServerVersion(new Version(5, 6, 51));

            optionsBuilder.UseMySql(
                ConnectionString,
                serverVersion,
                options =>
                {
                    options.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null
                    );
                    options.SchemaBehavior(MySqlSchemaBehavior.Ignore);
                }
            );

            return optionsBuilder.Options;
        }
    }
}