// Infrastructure/IntegrationTestBase.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Integration.Tests.Infrastructure
{
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        protected IServiceScope Scope { get; private set; } = null!;
        protected IConfiguration Configuration { get; private set; } = null!;

        public virtual async Task InitializeAsync()
        {
            var services = new ServiceCollection();
            Configuration = BuildConfiguration();
            ConfigureServices(services, Configuration);
            ServiceProvider = services.BuildServiceProvider();
            Scope = ServiceProvider.CreateScope();

            // 初始化数据库
            await InitializeDatabaseAsync();

            await OnInitializeAsync();
        }

        public virtual async Task DisposeAsync()
        {
            await OnDisposeAsync();

            // 清理数据库
            await CleanupDatabaseAsync();

            Scope?.Dispose();

            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        protected virtual IConfiguration BuildConfiguration()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            return configBuilder.Build();
        }

        protected abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// 初始化数据库 - 确保数据库已创建并应用所有迁移
        /// </summary>
        protected virtual async Task InitializeDatabaseAsync()
        {
            // 可以在子类中重写此方法来初始化特定的DbContext
            await Task.CompletedTask;
        }

        /// <summary>
        /// 清理数据库 - 测试结束后清理数据
        /// </summary>
        protected virtual async Task CleanupDatabaseAsync()
        {
            // 可以在子类中重写此方法来清理数据库
            await Task.CompletedTask;
        }

        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        protected virtual Task OnDisposeAsync() => Task.CompletedTask;

        protected T GetService<T>() where T : notnull
        {
            return Scope.ServiceProvider.GetRequiredService<T>();
        }

        protected T? GetOptionalService<T>() where T : class
        {
            return Scope.ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// 确保数据库已创建（如果不存在则创建）
        /// </summary>
        protected async Task EnsureDatabaseCreatedAsync<TContext>() where TContext : DbContext
        {
            var context = GetService<TContext>();
            await context.Database.EnsureCreatedAsync();
        }

        /// <summary>
        /// 应用所有待处理的迁移
        /// </summary>
        protected async Task MigrateDatabaseAsync<TContext>() where TContext : DbContext
        {
            var context = GetService<TContext>();
            await context.Database.MigrateAsync();
        }

        /// <summary>
        /// 删除并重新创建数据库（用于测试环境）
        /// </summary>
        protected async Task ResetDatabaseAsync<TContext>() where TContext : DbContext
        {
            var context = GetService<TContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        /// <summary>
        /// 清空指定表的所有数据
        /// </summary>
        protected async Task TruncateTableAsync<TContext>(string tableName) where TContext : DbContext
        {
            var context = GetService<TContext>();
            await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tableName}");
        }

        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        protected string GetConnectionString(string name)
        {
            return Configuration.GetConnectionString(name)
                ?? throw new InvalidOperationException($"Connection string '{name}' not found");
        }

        /// <summary>
        /// 验证数据库是否可访问
        /// </summary>
        protected async Task<bool> CanConnectToDatabaseAsync<TContext>() where TContext : DbContext
        {
            try
            {
                var context = GetService<TContext>();
                return await context.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }
    }
}