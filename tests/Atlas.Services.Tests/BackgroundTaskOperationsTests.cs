using Atlas.BackgroundTasks;
using Atlas.BackgroundTasks.Operations;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.Services.Tests;

public sealed class BackgroundTaskOperationsTests
{
    [Fact]
    public void SensitiveJsonMasker_MasksSensitiveJsonFields()
    {
        var masker = new SensitiveJsonMasker();
        const string json = """
{
  "channelId": 123,
  "accessToken": "secret-token",
  "profile": {
    "mobile": "13800000000",
    "email": "ops@example.local"
  }
}
""";

        var masked = masker.MaskJson(json);

        Assert.Contains("\"channelId\": 123", masked);
        Assert.Contains("\"accessToken\": \"***\"", masked);
        Assert.Contains("\"mobile\": \"***\"", masked);
        Assert.Contains("\"email\": \"***\"", masked);
        Assert.DoesNotContain("secret-token", masked);
        Assert.DoesNotContain("13800000000", masked);
        Assert.DoesNotContain("ops@example.local", masked);
    }

    [Fact]
    public void SensitiveJsonMasker_MasksPlainTextAssignments()
    {
        var masker = new SensitiveJsonMasker();

        var masked = masker.MaskText("authorization=Bearer abc token:xyz jobId=42");

        Assert.Contains("authorization=***", masked);
        Assert.Contains("token:***", masked);
        Assert.DoesNotContain("Bearer abc", masked);
        Assert.DoesNotContain("xyz", masked);
        Assert.Contains("jobId=42", masked);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMillisecondRunDurationForCompletedShortJobs()
    {
        await using var fixture = await CreateFixtureAsync();
        var startedAt = new DateTime(2026, 6, 13, 1, 2, 3, 100, DateTimeKind.Utc);
        var completedAt = startedAt.AddMilliseconds(450);

        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1001,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.test",
            JobName = "Short completed job",
            Payload = "{}",
            Status = BackgroundJobStatus.Succeeded,
            CreatedAt = startedAt.AddSeconds(-1),
            AvailableAtUtc = startedAt.AddSeconds(-1),
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            AttemptCount = 1,
            MaxAttempts = 5
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.SearchAsync(new BackgroundJobSearchQuery
        {
            Queue = "bidops",
            PageSize = 10
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(450, item.RunMilliseconds);
        Assert.Equal(0, item.RunSeconds);
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AtlasGlobalDbContext>()
            .UseSqlite(connection)
            .Options;
        var identity = new FixedIdentity();
        var dbContext = new AtlasGlobalDbContext(options, identity);
        await dbContext.Database.EnsureCreatedAsync();

        var service = new BackgroundJobOperationsService(
            dbContext,
            identity,
            new IncrementingIdGenerator(2000),
            new SensitiveJsonMasker(),
            Options.Create(new BackgroundJobWorkerOptions()));

        return new TestFixture(connection, dbContext, service);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestFixture(
            SqliteConnection connection,
            AtlasGlobalDbContext dbContext,
            BackgroundJobOperationsService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public AtlasGlobalDbContext DbContext { get; }

        public BackgroundJobOperationsService Service { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class IncrementingIdGenerator : IIdGenerator
    {
        private long _next;

        public IncrementingIdGenerator(long start)
        {
            _next = start;
        }

        public long NextId()
        {
            return Interlocked.Increment(ref _next);
        }

        public long[] NextIds(int count)
        {
            return Enumerable.Range(0, count).Select(_ => NextId()).ToArray();
        }
    }

    private sealed class FixedIdentity : ICurrentIdentity
    {
        public const long TestTenantId = 300001;

        public long? TenantId => TestTenantId;

        public long? StoreId => 320001;

        public long? UserId => 320101;

        public bool IsAuthenticated => true;

        public string? SessionId => null;

        public string UserName => "bidops_admin";
    }
}
