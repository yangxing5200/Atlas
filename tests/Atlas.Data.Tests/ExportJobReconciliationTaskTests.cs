using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Exporting;
using Atlas.Exporting.Reconciliation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class ExportJobReconciliationTaskTests
{
    private const string ExportTaskType = "test.export.list";
    private const string ResourceCode = "test.export";
    private const string PermissionCode = "test.export";

    [Fact]
    public async Task ExecuteAsync_RequeuesPendingExportWithoutBackgroundJob()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        var exportJob = new ExportJob
        {
            Id = 100,
            TenantId = 1,
            StoreId = 10,
            UserId = 20,
            ExportTaskType = ExportTaskType,
            ResourceCode = ResourceCode,
            PermissionCode = PermissionCode,
            Format = "csv",
            QueryJson = "{}",
            QueryHash = "hash",
            Status = ExportJobStatus.Pending,
            RequestedAtUtc = now.AddMinutes(-5),
            ExpiresAtUtc = now.AddDays(1),
            CreatedAt = now.AddMinutes(-5)
        };

        await fixture.DbContext.ExportJobs.AddAsync(exportJob);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Task.ExecuteAsync(new RecurringTaskContext(DateTimeOffset.UtcNow));

        var reconciled = await fixture.DbContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == exportJob.Id);
        var backgroundJob = await fixture.DbContext.BackgroundJobs.AsNoTracking().SingleAsync();

        Assert.Equal(ExportJobStatus.Pending, reconciled.Status);
        Assert.Equal(backgroundJob.Id, reconciled.BackgroundJobId);
        Assert.Equal(ExportBackgroundJobTypes.Generate, backgroundJob.JobType);
        Assert.Equal(ExportBackgroundJobQueues.Export, backgroundJob.Queue);
        Assert.Equal("export:generate:100", backgroundJob.DeduplicationKey);
        Assert.Equal(1, backgroundJob.TenantId);
        Assert.Equal(10, backgroundJob.StoreId);
        Assert.Contains("\"exportJobId\":100", backgroundJob.Payload);
    }

    [Fact]
    public async Task ExecuteAsync_MarksExportFailedWhenBackgroundJobIsDead()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        var backgroundJob = new BackgroundJob
        {
            Id = 200,
            TenantId = 1,
            StoreId = 10,
            JobType = ExportBackgroundJobTypes.Generate,
            Queue = ExportBackgroundJobQueues.Export,
            JobName = "Export test",
            Payload = "{}",
            Status = BackgroundJobStatus.Dead,
            AvailableAtUtc = now.AddMinutes(-5),
            MaxAttempts = 5,
            LastError = "boom",
            CreatedAt = now.AddMinutes(-5)
        };
        var exportJob = new ExportJob
        {
            Id = 101,
            BackgroundJobId = backgroundJob.Id,
            TenantId = 1,
            StoreId = 10,
            UserId = 20,
            ExportTaskType = ExportTaskType,
            ResourceCode = ResourceCode,
            PermissionCode = PermissionCode,
            Format = "csv",
            QueryJson = "{}",
            QueryHash = "hash",
            Status = ExportJobStatus.Pending,
            RequestedAtUtc = now.AddMinutes(-5),
            ExpiresAtUtc = now.AddDays(1),
            CreatedAt = now.AddMinutes(-5)
        };

        await fixture.DbContext.BackgroundJobs.AddAsync(backgroundJob);
        await fixture.DbContext.ExportJobs.AddAsync(exportJob);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Task.ExecuteAsync(new RecurringTaskContext(DateTimeOffset.UtcNow));

        var reconciled = await fixture.DbContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == exportJob.Id);

        Assert.Equal(ExportJobStatus.Failed, reconciled.Status);
        Assert.Contains("Dead", reconciled.LastError);
        Assert.Contains("boom", reconciled.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_LinksExistingBackgroundJobBeforeRequeueing()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        var backgroundJob = new BackgroundJob
        {
            Id = 201,
            TenantId = 1,
            StoreId = 10,
            JobType = ExportBackgroundJobTypes.Generate,
            Queue = ExportBackgroundJobQueues.Export,
            JobName = "Export test",
            Payload = "{\"exportJobId\":102}",
            Status = BackgroundJobStatus.Pending,
            AvailableAtUtc = now.AddMinutes(-5),
            MaxAttempts = 5,
            CreatedAt = now.AddMinutes(-5)
        };
        var exportJob = new ExportJob
        {
            Id = 102,
            TenantId = 1,
            StoreId = 10,
            UserId = 20,
            ExportTaskType = ExportTaskType,
            ResourceCode = ResourceCode,
            PermissionCode = PermissionCode,
            Format = "csv",
            QueryJson = "{}",
            QueryHash = "hash",
            Status = ExportJobStatus.Pending,
            RequestedAtUtc = now.AddMinutes(-5),
            ExpiresAtUtc = now.AddDays(1),
            CreatedAt = now.AddMinutes(-5)
        };

        await fixture.DbContext.BackgroundJobs.AddAsync(backgroundJob);
        await fixture.DbContext.ExportJobs.AddAsync(exportJob);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Task.ExecuteAsync(new RecurringTaskContext(DateTimeOffset.UtcNow));

        var reconciled = await fixture.DbContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == exportJob.Id);
        var backgroundJobCount = await fixture.DbContext.BackgroundJobs.CountAsync();

        Assert.Equal(ExportJobStatus.Pending, reconciled.Status);
        Assert.Equal(backgroundJob.Id, reconciled.BackgroundJobId);
        Assert.Equal(1, backgroundJobCount);
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AtlasGlobalDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AtlasGlobalDbContext(options, new FixedIdentity());
        await dbContext.Database.EnsureCreatedAsync();

        var idGenerator = new IncrementingIdGenerator(1000);
        var backgroundJobs = new BackgroundJobClient(
            dbContext,
            idGenerator,
            Options.Create(new BackgroundJobWorkerOptions { DefaultMaxAttempts = 5 }));
        var exportOptions = Options.Create(new ExportJobOptions
        {
            DefaultFormat = "csv",
            AllowedFormats = ["csv"],
            DefaultPageSize = 500,
            MaxPageSize = 2000,
            DefaultMaxRows = 100000,
            Reconciliation = new ExportJobReconciliationOptions
            {
                Enabled = true,
                IntervalMinutes = 1,
                StalePendingMinutes = 1,
                StaleRunningMinutes = 1,
                BatchSize = 100
            }
        });
        var task = new ExportJobReconciliationTask(
            dbContext,
            backgroundJobs,
            exportOptions,
            [new TestExportProvider()],
            [new TestExportWriter()],
            NullLogger<ExportJobReconciliationTask>.Instance);

        return new TestFixture(connection, dbContext, task);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestFixture(
            SqliteConnection connection,
            AtlasGlobalDbContext dbContext,
            ExportJobReconciliationTask task)
        {
            _connection = connection;
            DbContext = dbContext;
            Task = task;
        }

        public AtlasGlobalDbContext DbContext { get; }

        public ExportJobReconciliationTask Task { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestExportProvider : ExportTaskProvider<TestExportRequest>
    {
        public override string ExportTaskType => ExportJobReconciliationTaskTests.ExportTaskType;

        public override string ResourceCode => ExportJobReconciliationTaskTests.ResourceCode;

        public override string PermissionCode => ExportJobReconciliationTaskTests.PermissionCode;

        public override IReadOnlyList<ExportColumn> Columns { get; } =
        [
            new("id", "Id") { ValueKind = ExportValueKind.Number }
        ];

        public override Task<ExportPage> ReadPageAsync(
            ExportTaskContext<TestExportRequest> context,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            return Task.FromResult(new ExportPage([], 0));
        }
    }

    private sealed class TestExportWriter : IExportFormatWriter
    {
        public string Format => "csv";

        public string ContentType => "text/csv";

        public string FileExtension => ".csv";

        public Task<ExportWriteResult> WriteAsync(
            ExportWriteContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(new ExportWriteResult(0, 0));
        }
    }

    private sealed class TestExportRequest
    {
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
        public long? TenantId => 1;

        public long? StoreId => 10;

        public long? UserId => 20;

        public bool IsAuthenticated => true;

        public string? SessionId => null;

        public string UserName => "test";
    }
}
