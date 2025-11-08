// ============================================================
// 文件路径: Atlas.Integration.Tests/SmartBatchMySqlTests.cs
// ============================================================

using Atlas.Core.IdGenerators;
using Atlas.Data.Common.Extensions;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Tests;
using Atlas.Data.Tests.Mocks;
using Atlas.Data.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Atlas.Integration.Tests
{
    /// <summary>
    /// MySQL集成测试（需要真实的MySQL数据库）
    /// 运行前请确保：
    /// 1. appsettings.Test.json中配置了MySQL连接字符串
    /// 2. 数据库可访问
    /// 3. 有足够权限创建表
    /// </summary>
    [Collection("MySQL Integration Tests")]
    public class BatchMySqlTests : IDisposable
    {
        private readonly TestDbContext _context;
        private readonly MockCurrentUserService _mockUserService;
        private readonly ITestOutputHelper _output;
        private readonly IIdGenerator _idGenerator= new SnowflakeIdGeneratorFactory(1, 1).GetGenerator();

        public BatchMySqlTests(ITestOutputHelper output)
        {
            _output = output;

            // 读取配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Test.json", optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString("MySqlTest")
                ?? "Server=localhost;Database=atlas_test;User=root;Password=123456;";

            _output.WriteLine($"📝 使用连接字符串: {MaskPassword(connectionString)}");

            // 创建Mock用户服务
            _mockUserService = new MockCurrentUserService(userId: 10001, tenantId: 1);

            // 创建AuditInterceptor
            var auditInterceptor = new AuditInterceptor(_mockUserService);

            // 配置DbContext（真实MySQL）
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mysqlOptions => mysqlOptions.CommandTimeout(30))
                .AddInterceptors(auditInterceptor) // ⭐ 添加审计拦截器
                .EnableSensitiveDataLogging() // 测试环境启用详细日志
                .Options;

            _context = new TestDbContext(options, _mockUserService);

            // 创建ID生成器
            _idGenerator = new SnowflakeIdGenerator(1, 1);

            // 初始化数据库
            try
            {
                _context.Database.EnsureDeleted(); // 删除旧表
                _context.Database.EnsureCreated(); // 创建新表
                _output.WriteLine("✅ 数据库初始化成功");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ 数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 真实MySQL测试

        [Fact]
        public async Task MySQL_SmartBatchSave_Insert_ShouldFillAutoIncrementId()
        {
            // Arrange
            var tenants = new List<TestTenant>
            {
                new TestTenant { Name = "测试租户1", Code = "MT001", Status = 1, Remark = "批量插入测试" },
                new TestTenant { Name = "测试租户2", Code = "MT002", Status = 1, Remark = "批量插入测试" },
                new TestTenant { Name = "测试租户3", Code = "MT003", Status = 1, Remark = "批量插入测试" },
                new TestTenant { Name = "测试租户4", Code = "MT004", Status = 1, Remark = "批量插入测试" },
                new TestTenant { Name = "测试租户5", Code = "MT005", Status = 1, Remark = "批量插入测试" },
            };

            _output.WriteLine($"🚀 MySQL测试：批量插入{tenants.Count}条数据");

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cnt = await _context.BatchInsertAsync(tenants);
            sw.Stop();

            // Assert
            Assert.Equal(5, cnt);

            _output.WriteLine($"✅ 插入成功，耗时: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine("📋 回填的ID:");

            foreach (var tenant in tenants)
            {
                Assert.True(tenant.Id > 0);
                Assert.Equal(_mockUserService.UserId, tenant.CreatedBy);
                Assert.True(tenant.CreatedAt > DateTime.MinValue);
                Assert.Equal(0, tenant.Version);

                _output.WriteLine($"  ID: {tenant.Id}, Name: {tenant.Name}, CreatedBy: {tenant.CreatedBy}, CreatedAt: {tenant.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            }

            // 验证数据确实插入到数据库
            var count = await _context.TestTenants.CountAsync();
            Assert.Equal(5, count);
            _output.WriteLine($"✅ 数据库验证：共{count}条记录");
        }

        [Fact]
        public async Task MySQL_SmartBatchSave_Update_ShouldIncrementVersion()
        {
            // Arrange - 先插入
            var tenants = new List<TestTenant>
            {
                new TestTenant { Name = "租户A", Code = "MA", Status = 1 },
                new TestTenant { Name = "租户B", Code = "MB", Status = 1 },
                new TestTenant { Name      = "租户C", Code = "MC", Status = 1 },
            };

            await _context.BatchInsertAsync(tenants);
            var originalVersions = tenants.Select(t => t.Version).ToList();
            var originalCreatedAts = tenants.Select(t => t.CreatedAt).ToList();

            _output.WriteLine($"🚀 MySQL测试：批量更新{tenants.Count}条数据");
            _output.WriteLine($"  原始版本号: [{string.Join(", ", originalVersions)}]");

            // 等待确保时间戳不同
            await Task.Delay(1000);

            // 修改数据
            foreach (var tenant in tenants)
            {
                tenant.Status = 2;
                tenant.Remark = "已更新";
            }

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cnt = await _context.BatchUpdateAsync(tenants);
            sw.Stop();

            // Assert
            Assert.Equal(3, cnt);

            _output.WriteLine($"✅ 更新成功，耗时: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine("📋 更新结果:");

            for (int i = 0; i < tenants.Count; i++)
            {
                Assert.Equal(originalVersions[i] + 1, tenants[i].Version);
                Assert.Equal(_mockUserService.UserId, tenants[i].UpdatedBy);
                Assert.NotNull(tenants[i].UpdatedAt);
                Assert.True(tenants[i].UpdatedAt > originalCreatedAts[i]);

                _output.WriteLine($"  {tenants[i].Name}: Version {originalVersions[i]} → {tenants[i].Version}, " +
                                $"UpdatedAt: {tenants[i].UpdatedAt:yyyy-MM-dd HH:mm:ss}");
            }

            // 从数据库验证
            var dbTenants = await _context.TestTenants
                .Where(t => tenants.Select(x => x.Id).Contains(t.Id))
                .ToListAsync();

            Assert.All(dbTenants, t =>
            {
                Assert.Equal(2, t.Status);
                Assert.Equal("已更新", t.Remark);
                Assert.Equal(1, t.Version); // 第一次更新，版本号应该是1
            });

            _output.WriteLine($"✅ 数据库验证：版本号和字段更新正确");
        }

        [Fact]
        public async Task MySQL_SmartBatchSave_LargeBatch_PerformanceTest()
        {
            // Arrange
            const int totalCount = 10000;
            const int batchSize = 1000;

            var tenants = Enumerable.Range(1, totalCount)
                .Select(i => new TestTenant
                {
                    Name = $"大批量租户{i}",
                    Code = $"LB{i:D6}",
                    Status = 1,
                    Remark = "性能测试"
                })
                .ToList();

            _output.WriteLine($"🚀 MySQL性能测试：插入{totalCount}条数据，批次大小{batchSize}");

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cnt = await _context.BatchInsertAsync(tenants, batchSize: batchSize);
            sw.Stop();

            // Assert
            Assert.Equal(totalCount, cnt);
            Assert.All(tenants, t => Assert.True(t.Id > 0));

            var throughput = (double)totalCount / sw.ElapsedMilliseconds * 1000;

            _output.WriteLine($"✅ 测试完成");
            _output.WriteLine($"📊 性能数据:");
            _output.WriteLine($"   总耗时: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"   吞吐量: {throughput:F2} 条/秒");
            _output.WriteLine($"   平均每条: {(double)sw.ElapsedMilliseconds / totalCount:F3}ms");

            // 验证数据库
            var dbCount = await _context.TestTenants.CountAsync();
            Assert.True(dbCount >= totalCount);
            _output.WriteLine($"✅ 数据库验证：共{dbCount}条记录");
        }

        [Fact]
        public async Task MySQL_SmartBatchSave_WithSnowflakeId_ShouldWork()
        {
            // Arrange
            var users = Enumerable.Range(1, 100)
                .Select(i => new TestUser
                {
                    Id = _idGenerator.NextId(),
                    UserName = $"user{i}",
                    Email = $"user{i}@test.com",
                    Phone = $"1380000{i:D4}",
                    TenantId = 1
                })
                .ToList();

            _output.WriteLine($"🚀 MySQL测试：批量插入{users.Count}条数据（Snowflake ID）");

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cnt = await _context.BatchInsertAsync(users);
            sw.Stop();

            // Assert
            Assert.Equal(100, cnt);

            _output.WriteLine($"✅ 插入成功，耗时: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"📋 ID范围: {users.Min(u => u.Id)} ~ {users.Max(u => u.Id)}");

            // 验证数据库
            var dbUsers = await _context.TestUsers.ToListAsync();
            Assert.Equal(100, dbUsers.Count);
            Assert.All(dbUsers, u =>
            {
                Assert.Equal(_mockUserService.UserId, u.CreatedBy);
                Assert.Equal(0, u.Version);
            });

            _output.WriteLine($"✅ 数据库验证：所有用户已正确插入");
        }

        [Fact]
        public async Task MySQL_ComparePerformance_SaveChanges_vs_SmartBatch()
        {
            const int testCount = 1000;

            _output.WriteLine($"🚀 性能对比测试：SaveChanges vs SmartBatchSave ({testCount}条数据)");

            // 测试1: 使用SaveChanges
            var tenants1 = Enumerable.Range(1, testCount)
                .Select(i => new TestTenant
                {
                    Name = $"SaveChanges租户{i}",
                    Code = $"SC{i:D4}",
                    Status = 1
                })
                .ToList();

            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            _context.TestTenants.AddRange(tenants1);
            await _context.SaveChangesAsync(); // ⭐ 使用SaveChanges
            sw1.Stop();

            _output.WriteLine($"📊 SaveChanges: {sw1.ElapsedMilliseconds}ms");

            // 清理
            _context.TestTenants.RemoveRange(tenants1);
            await _context.SaveChangesAsync();

            // 测试2: 使用SmartBatchSave
            var tenants2 = Enumerable.Range(1, testCount)
                .Select(i => new TestTenant
                {
                    Name = $"SmartBatch租户{i}",
                    Code = $"SB{i:D4}",
                    Status = 1
                })
                .ToList();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            await _context.BatchInsertAsync(tenants2);
            sw2.Stop();

            _output.WriteLine($"📊 SmartBatchSave: {sw2.ElapsedMilliseconds}ms");

            // 对比
            var improvement = (double)sw1.ElapsedMilliseconds / sw2.ElapsedMilliseconds;
            _output.WriteLine($"");
            _output.WriteLine($"✅ 性能提升: {improvement:F2}x");
            _output.WriteLine($"   SaveChanges:    {sw1.ElapsedMilliseconds}ms ({(double)testCount / sw1.ElapsedMilliseconds * 1000:F2} 条/秒)");
            _output.WriteLine($"   SmartBatchSave: {sw2.ElapsedMilliseconds}ms ({(double)testCount / sw2.ElapsedMilliseconds * 1000:F2} 条/秒)");

            Assert.True(improvement > 1, "SmartBatchSave应该比SaveChanges更快");
        }

        #endregion

        private static string MaskPassword(string connectionString)
        {
            var parts = connectionString.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim().StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                {
                    parts[i] = "Password=****";
                }
            }
            return string.Join(";", parts);
        }

        public void Dispose()
        {
            try
            {
                // 清理测试数据
                _context.Database.EnsureDeleted();
                _output.WriteLine("🧹 测试数据已清理");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"⚠️ 清理失败: {ex.Message}");
            }
            finally
            {
                _context?.Dispose();
            }
        }
    }
}