// Performance/CachePerformanceTests.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Atlas.Integration.Tests.Performance
{
    [Collection("Redis")]
    public class CachePerformanceTests : IntegrationTestBase
    {
        private readonly RedisFixture _redisFixture;
        private readonly ITestOutputHelper _output;
        private ICacheService _cacheService = null!;

        public CachePerformanceTests(RedisFixture redisFixture, ITestOutputHelper output)
        {
            _redisFixture = redisFixture;
            _output = output;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddAtlasCaching();
            services.AddRedisCaching(_redisFixture.ConnectionString, "perf-test");
        }

        protected override Task OnInitializeAsync()
        {
            _cacheService = GetService<ICacheService>();
            return _redisFixture.ClearAllAsync();
        }

        [Fact]
        public async Task Sequential_Write_Performance()
        {
            // Arrange
            const int iterations = 1000;
            var sw = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                await _cacheService.SetAsync($"perf:write:{i}", new TestData { Id = i, Name = $"Item {i}" });
            }

            sw.Stop();

            // Assert & Log
            var avgTime = sw.ElapsedMilliseconds / (double)iterations;
            _output.WriteLine($"Sequential Writes: {iterations} items in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {avgTime:F2}ms per write");

            avgTime.Should().BeLessThan(10); // Should be fast
        }

        [Fact]
        public async Task Sequential_Read_Performance()
        {
            // Arrange
            const int iterations = 1000;

            // Pre-populate
            for (int i = 0; i < iterations; i++)
            {
                await _cacheService.SetAsync($"perf:read:{i}", new TestData { Id = i, Name = $"Item {i}" });
            }

            var sw = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                await _cacheService.GetAsync<TestData>($"perf:read:{i}");
            }

            sw.Stop();

            // Assert & Log
            var avgTime = sw.ElapsedMilliseconds / (double)iterations;
            _output.WriteLine($"Sequential Reads: {iterations} items in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {avgTime:F2}ms per read");

            avgTime.Should().BeLessThan(10);
        }

        [Fact]
        public async Task Batch_Operations_Performance()
        {
            // Arrange
            const int batchSize = 100;
            var items = Enumerable.Range(0, batchSize)
                .ToDictionary(
                    i => $"perf:batch:{i}",
                    i => new TestData { Id = i, Name = $"Batch Item {i}" }
                );

            var sw = Stopwatch.StartNew();

            // Act - Batch Write
            await _cacheService.SetManyAsync(items);
            var writeTime = sw.ElapsedMilliseconds;

            sw.Restart();

            // Act - Batch Read
            var results = await _cacheService.GetManyAsync<TestData>(items.Keys);
            var readTime = sw.ElapsedMilliseconds;

            sw.Stop();

            // Assert & Log
            _output.WriteLine($"Batch Write: {batchSize} items in {writeTime}ms ({writeTime / (double)batchSize:F2}ms avg)");
            _output.WriteLine($"Batch Read: {batchSize} items in {readTime}ms ({readTime / (double)batchSize:F2}ms avg)");

            results.Should().HaveCount(batchSize);
            writeTime.Should().BeLessThan(5000);
            readTime.Should().BeLessThan(5000);
        }

        [Fact]
        public async Task Cache_Hit_Rate_Test()
        {
            // Arrange
            const int uniqueKeys = 100;
            const int totalAccesses = 1000;

            // Pre-populate half the keys
            for (int i = 0; i < uniqueKeys / 2; i++)
            {
                await _cacheService.SetAsync($"hitrate:{i}", new TestData { Id = i });
            }

            var random = new Random(42);
            var hits = 0;
            var misses = 0;

            // Act
            for (int i = 0; i < totalAccesses; i++)
            {
                var keyIndex = random.Next(uniqueKeys);
                var result = await _cacheService.GetAsync<TestData>($"hitrate:{keyIndex}");

                if (result != null)
                    hits++;
                else
                    misses++;
            }

            var hitRate = hits / (double)totalAccesses;

            // Assert & Log
            _output.WriteLine($"Total Accesses: {totalAccesses}");
            _output.WriteLine($"Hits: {hits}");
            _output.WriteLine($"Misses: {misses}");
            _output.WriteLine($"Hit Rate: {hitRate:P2}");

            hitRate.Should().BeGreaterThan(0.3); // At least 30% hit rate
        }

        [Fact]
        public async Task Concurrent_Access_Test()
        {
            // Arrange
            const int concurrentTasks = 50;
            const int operationsPerTask = 20;

            var sw = Stopwatch.StartNew();

            // Act
            var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    var key = $"concurrent:{taskId}:{i}";
                    await _cacheService.SetAsync(key, new TestData { Id = taskId * 1000 + i });
                    await _cacheService.GetAsync<TestData>(key);
                }
            });

            await Task.WhenAll(tasks);
            sw.Stop();

            var totalOperations = concurrentTasks * operationsPerTask * 2; // Set + Get

            // Assert & Log
            _output.WriteLine($"Concurrent Operations: {totalOperations} in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {totalOperations / (sw.ElapsedMilliseconds / 1000.0):F0} ops/sec");

            sw.ElapsedMilliseconds.Should().BeLessThan(30000); // Should complete in 30 seconds
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}