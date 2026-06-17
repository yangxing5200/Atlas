using Atlas.BackgroundTasks;
using Atlas.BackgroundTasks.Operations;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

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

    [Fact]
    public async Task SearchAsync_ReturnsChineseJobTypeNameAndLocalTimeAliases()
    {
        await using var fixture = await CreateFixtureAsync();
        var createdAt = new DateTime(2026, 6, 15, 15, 12, 0, DateTimeKind.Local);
        var availableAt = createdAt.AddSeconds(5);
        var startedAt = availableAt.AddSeconds(3);
        var completedAt = startedAt.AddSeconds(8);

        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1002,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.ai.structured-parse",
            JobName = "Structured parse",
            Payload = "{}",
            Status = BackgroundJobStatus.Succeeded,
            CreatedAt = createdAt,
            AvailableAtUtc = availableAt,
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
        Assert.Equal("BidOps 公告结构化解析", item.JobTypeName);
        Assert.Equal(availableAt, item.AvailableAt);
        Assert.Equal(startedAt, item.StartedAt);
        Assert.Equal(completedAt, item.CompletedAt);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsChineseFailureJobTypeDisplayNames()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;

        fixture.DbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Id = 1003,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.outcome.supplier-extract",
                JobName = "Outcome supplier extract",
                Payload = "{}",
                Status = BackgroundJobStatus.Failed,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 5
            },
            new BackgroundJob
            {
                Id = 1004,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "default",
                JobType = "tenant.cache-warmup",
                JobName = "Tenant cache warmup",
                Payload = "{}",
                Status = BackgroundJobStatus.Dead,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 5,
                MaxAttempts = 5
            });
        await fixture.DbContext.SaveChangesAsync();

        var summary = await fixture.Service.GetSummaryAsync(new BackgroundJobSearchQuery { PageSize = 10 });

        Assert.Contains(summary.JobTypeFailureCounts, item =>
            item.Name == "bidops.outcome.supplier-extract" &&
            item.DisplayName == "BidOps 中标/候选厂家提取");
        Assert.Contains(summary.JobTypeFailureCounts, item =>
            item.Name == "tenant.cache-warmup" &&
            item.DisplayName == "租户缓存预热");
    }

    [Fact]
    public async Task GetAsync_ReturnsFullResultForBidOpsDeepSeekDiagnostics()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;
        var longRawResponse = new string('x', 25_000) + "END_MARKER";
        var resultJson = JsonSerializer.Serialize(new
        {
            rawNoticeId = 123,
            deepSeekResponses = new[]
            {
                new
                {
                    provider = "DeepSeek",
                    model = "deepseek-v4-pro",
                    rawResponseBody = longRawResponse,
                    assistantContent = "{\"records\":[]}"
                }
            }
        });

        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1005,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.outcome.supplier-extract",
            JobName = "Outcome supplier extract",
            Payload = "{\"rawNoticeId\":123}",
            Result = resultJson,
            Status = BackgroundJobStatus.Succeeded,
            CreatedAt = now,
            AvailableAtUtc = now,
            CompletedAtUtc = now,
            AttemptCount = 1,
            MaxAttempts = 5
        });
        await fixture.DbContext.SaveChangesAsync();

        var detail = await fixture.Service.GetAsync(1005);

        Assert.NotNull(detail);
        Assert.Contains("END_MARKER", detail.Result);
        Assert.Contains("deepSeekResponses", detail.Result);
    }

    [Fact]
    public async Task RetryAsync_CreatesRetryJobWithLocalLifecycleTimeAndChineseJobTypeName()
    {
        await using var fixture = await CreateFixtureAsync();
        var originalCreatedAt = DateTime.Now.AddMinutes(-5);
        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1005,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.document.attachment-process",
            JobName = "Attachment process",
            Payload = "{\"rawNoticeId\":123}",
            Status = BackgroundJobStatus.Dead,
            CreatedAt = originalCreatedAt,
            AvailableAtUtc = originalCreatedAt,
            StartedAtUtc = originalCreatedAt.AddSeconds(1),
            CompletedAtUtc = originalCreatedAt.AddSeconds(2),
            AttemptCount = 5,
            MaxAttempts = 5
        });
        await fixture.DbContext.SaveChangesAsync();

        var before = DateTime.Now.AddSeconds(-1);
        var result = await fixture.Service.RetryAsync(1005);
        var after = DateTime.Now.AddSeconds(1);

        Assert.NotNull(result);
        Assert.Equal("BidOps 附件下载与文本提取", result.JobTypeName);

        var retry = await fixture.DbContext.BackgroundJobs.SingleAsync(x => x.Id == result.NewJobId);
        Assert.InRange(retry.CreatedAt, before, after);
        Assert.InRange(retry.AvailableAtUtc, before, after);
    }

    [Fact]
    public async Task CancelAsync_RunningJobRequestsCooperativeTermination()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;
        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1006,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.ai.structured-parse",
            JobName = "Structured parse",
            Payload = "{}",
            Status = BackgroundJobStatus.Running,
            CreatedAt = now.AddSeconds(-10),
            AvailableAtUtc = now.AddSeconds(-10),
            StartedAtUtc = now.AddSeconds(-9),
            LockedAtUtc = now.AddSeconds(-9),
            LockedBy = "worker-1",
            AttemptCount = 1,
            MaxAttempts = 5
        });
        await fixture.DbContext.SaveChangesAsync();

        var before = DateTime.Now.AddSeconds(-1);
        var result = await fixture.Service.CancelAsync(
            1006,
            new BackgroundJobCancelRequest { Reason = "人工停止长时间解析" });
        var after = DateTime.Now.AddSeconds(1);

        Assert.NotNull(result);
        Assert.True(result.IsCancellationRequested);
        Assert.Equal(BackgroundJobStatus.Running, result.Status);

        var job = await fixture.DbContext.BackgroundJobs.SingleAsync(x => x.Id == 1006);
        Assert.Equal(BackgroundJobStatus.Running, job.Status);
        Assert.NotNull(job.CancellationRequestedAt);
        Assert.InRange(job.CancellationRequestedAt.Value, before, after);
        Assert.Equal("人工停止长时间解析", job.CancellationReason);
        Assert.Contains("bidops_admin", job.CancellationRequestedBy);
        Assert.Null(job.CompletedAtUtc);
    }

    [Fact]
    public async Task Worker_CancelsRunningJobWhenTerminationIsRequested()
    {
        await using var fixture = await CreateWorkerFixtureAsync();
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1007,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = BlockingCancellationHandler.Type,
            JobName = "Cancelable handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = DateTime.Now.AddSeconds(-1),
            AvailableAtUtc = DateTime.Now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processing = fixture.Worker.ProcessOnceAsync();
        await fixture.Handler.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.RequestCancellationAsync(1007, "停止测试任务");
        var processed = await processing.WaitAsync(TimeSpan.FromSeconds(8));
        var job = await fixture.GetJobAsync(1007);

        Assert.Equal(1, processed);
        Assert.True(fixture.Handler.SawCancellation);
        Assert.Equal(BackgroundJobStatus.Canceled, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Null(job.LockedAtUtc);
        Assert.Null(job.LockedBy);
        Assert.Equal("停止测试任务", job.CancellationReason);
        Assert.Contains("Canceled by operator", job.Result);
    }

    [Fact]
    public async Task Worker_StoresExplicitLargeResult()
    {
        await using var fixture = await CreateWorkerFixtureAsync();
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1008,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Long result handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = DateTime.Now.AddSeconds(-1),
            AvailableAtUtc = DateTime.Now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var job = await fixture.GetJobAsync(1008);

        Assert.Equal(1, processed);
        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        Assert.Contains("END_MARKER", job.Result);
        Assert.True(job.Result!.Length > BackgroundJobResultStorageLimits.DefaultMaxCharacters);
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

    private static async Task<WorkerFixture> CreateWorkerFixtureAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var identity = new FixedIdentity();
        var handler = new BlockingCancellationHandler();
        var options = Options.Create(new BackgroundJobWorkerOptions
        {
            Enabled = true,
            Queues = ["bidops"],
            BatchSize = 1,
            PollIntervalSeconds = 1,
            ProcessingTimeoutSeconds = 300,
            CancellationCheckIntervalSeconds = 1,
            InitialRetryDelaySeconds = 1,
            MaxRetryDelaySeconds = 1,
            DefaultMaxAttempts = 1
        });

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddDbContext<AtlasGlobalDbContext>((provider, builder) =>
            builder.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddSingleton<ICurrentIdentity>(identity);
        services.AddSingleton<IOptions<BackgroundJobWorkerOptions>>(options);
        services.AddSingleton<BackgroundWorkerHeartbeatState>();
        services.AddSingleton(handler);
        services.AddScoped<IBackgroundJobHandler>(provider => provider.GetRequiredService<BlockingCancellationHandler>());
        services.AddSingleton<LongResultHandler>();
        services.AddScoped<IBackgroundJobHandler>(provider => provider.GetRequiredService<LongResultHandler>());
        services.AddSingleton<IIdGenerator>(new IncrementingIdGenerator(3000));
        services.AddSingleton<ISensitiveJsonMasker, SensitiveJsonMasker>();
        services.AddScoped<IBackgroundJobOperationsService, BackgroundJobOperationsService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<BackgroundJobWorker>>(
            NullLogger<BackgroundJobWorker>.Instance);
        services.AddSingleton<BackgroundJobWorker>();

        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new WorkerFixture(connection, provider, handler);
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

    private sealed class WorkerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public WorkerFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            BlockingCancellationHandler handler)
        {
            _connection = connection;
            _provider = provider;
            Handler = handler;
            Worker = provider.GetRequiredService<BackgroundJobWorker>();
        }

        public BackgroundJobWorker Worker { get; }

        public BlockingCancellationHandler Handler { get; }

        public async Task SeedJobAsync(BackgroundJob job)
        {
            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            db.BackgroundJobs.Add(job);
            await db.SaveChangesAsync();
        }

        public async Task RequestCancellationAsync(long jobId, string reason)
        {
            await using var scope = _provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IBackgroundJobOperationsService>();
            await service.CancelAsync(jobId, new BackgroundJobCancelRequest { Reason = reason });
        }

        public async Task<BackgroundJob> GetJobAsync(long jobId)
        {
            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();
            return await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class BlockingCancellationHandler : IBackgroundJobHandler
    {
        public const string Type = "test.cancel-aware";

        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string JobType => Type;

        public Task Started => _started.Task;

        public bool SawCancellation { get; private set; }

        public async Task<BackgroundJobExecutionResult> HandleAsync(
            BackgroundJobExecutionContext context,
            CancellationToken ct = default)
        {
            _started.TrySetResult();

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SawCancellation = true;
                throw;
            }

            return BackgroundJobExecutionResult.Success("Should not complete.");
        }
    }

    private sealed class LongResultHandler : IBackgroundJobHandler
    {
        public const string Type = "test.long-result";

        public string JobType => Type;

        public Task<BackgroundJobExecutionResult> HandleAsync(
            BackgroundJobExecutionContext context,
            CancellationToken ct = default)
        {
            var result = new string('r', 4_500) + "END_MARKER";
            return Task.FromResult(BackgroundJobExecutionResult.Success(result, maxResultCharacters: 6_000));
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
