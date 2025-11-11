// Infrastructure/LocalRedisFixture.cs
using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;

namespace Atlas.Integration.Tests.Infrastructure
{
    /// <summary>
    /// 使用本地 Redis 实例的 Fixture（无需 Docker）
    /// </summary>
    public class RedisFixture : IAsyncLifetime
    {
        private IConnectionMultiplexer? _redis;

        // 修改为你本地的 Redis 连接字符串
        public string ConnectionString { get; } = "localhost:6379,allowAdmin=true";

        public IConnectionMultiplexer Redis => _redis ?? throw new InvalidOperationException("Redis not initialized");

        public async Task InitializeAsync()
        {
            try
            {
                _redis = await ConnectionMultiplexer.ConnectAsync(ConnectionString);

                // 测试连接
                var db = _redis.GetDatabase();
                await db.PingAsync();

                // 清空测试数据库
                await ClearAllAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法连接到本地 Redis。请确保 Redis 正在运行。\n" +
                    $"当前连接字符串: {ConnectionString}\n" +
                    $"错误: {ex.Message}", ex);
            }
        }

        public async Task DisposeAsync()
        {
            if (_redis != null)
            {
                await ClearAllAsync();
                _redis.Dispose();
            }
        }

        public async Task ClearAllAsync()
        {
            if (_redis != null)
            {
                var endpoints = _redis.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    await server.FlushDatabaseAsync();
                }
            }
        }
    }
}