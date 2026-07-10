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
    public async Task SearchAsync_SortsByCompletedAtWhenRequested()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Local);

        fixture.DbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Id = 1021,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Older completed job",
                Payload = "{}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddMinutes(-30),
                AvailableAtUtc = now.AddMinutes(-30),
                StartedAtUtc = now.AddMinutes(-29),
                CompletedAtUtc = now.AddMinutes(-20),
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1022,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.outcome.supplier-extract",
                JobName = "Latest completed job",
                Payload = "{}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddMinutes(-40),
                AvailableAtUtc = now.AddMinutes(-40),
                StartedAtUtc = now.AddMinutes(-39),
                CompletedAtUtc = now.AddMinutes(-5),
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1023,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.document.attachment-process",
                JobName = "Pending job without completed time",
                Payload = "{}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 0,
                MaxAttempts = 3
            });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.SearchAsync(new BackgroundJobSearchQuery
        {
            Queue = "bidops",
            SortBy = "CompletedAt",
            SortDescending = true,
            PageSize = 10
        });

        Assert.Equal(new long[] { 1022, 1021, 1023 }, result.Items.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task SearchAsync_FiltersAndMapsBidOpsProjectCode()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;

        fixture.DbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Id = 1006,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse",
                Payload = "{\"rawNoticeId\":123,\"projectCode\":\"SGCC-001\"}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now,
                AvailableAtUtc = now,
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1007,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.document.attachment-process",
                JobName = "Attachment process",
                Payload = "{\"rawNoticeId\":124}",
                Result = "rawNoticeId=124;projectCode=SGCC-001",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddSeconds(-1),
                AvailableAtUtc = now.AddSeconds(-1),
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1008,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse",
                Payload = "{\"rawNoticeId\":125,\"projectCode\":\"SGCC-002\"}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddSeconds(-2),
                AvailableAtUtc = now.AddSeconds(-2),
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.SearchAsync(new BackgroundJobSearchQuery
        {
            Queue = "bidops",
            ProjectCode = "SGCC-001",
            PageSize = 10
        });

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, x => x.Id == 1006 && x.ProjectCode == "SGCC-001");
        Assert.Contains(result.Items, x => x.Id == 1007 && x.ProjectCode == "SGCC-001");
        Assert.DoesNotContain(result.Items, x => x.Id == 1008);
    }

    [Fact]
    public async Task SearchAsync_FiltersBidOpsJobsByExactRawNoticeId()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;

        fixture.DbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Id = 1009,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse",
                Payload = "{\"rawNoticeId\":123,\"projectCode\":\"SGCC-001\"}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now,
                AvailableAtUtc = now,
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1010,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse",
                DeduplicationKey = "bidops:structured-parse:v1:42:1234",
                Payload = "{\"rawNoticeId\": 1234}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddSeconds(-1),
                AvailableAtUtc = now.AddSeconds(-1),
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1011,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.document.attachment-process",
                JobName = "Attachment process",
                Payload = "{}",
                Result = "rawNoticeId=123;projectCode=SGCC-001;attachments=2",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddSeconds(-2),
                AvailableAtUtc = now.AddSeconds(-2),
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1012,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.outcome.supplier-extract",
                JobName = "Outcome supplier extract",
                Payload = "{\"RawNoticeId\": 123}",
                Result = "{\"rawNoticeId\":123,\"savedCount\":8}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddSeconds(-3),
                AvailableAtUtc = now.AddSeconds(-3),
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1014,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse with deduplication key",
                DeduplicationKey = "bidops:structured-parse:v1:42:123",
                Payload = "{}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now.AddSeconds(-4),
                AvailableAtUtc = now.AddSeconds(-4),
                AttemptCount = 0,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1013,
                TenantId = 999999,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Other tenant",
                Payload = "{\"rawNoticeId\":123}",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now,
                AvailableAtUtc = now,
                CompletedAtUtc = now,
                AttemptCount = 1,
                MaxAttempts = 3
            });
        await fixture.DbContext.SaveChangesAsync();

        var backfillHandler = new BackgroundJobBusinessLinkBackfillJobHandler(
            fixture.DbContext,
            NullLogger<BackgroundJobBusinessLinkBackfillJobHandler>.Instance);
        var backfillPayload = JsonSerializer.Serialize(
            new BackgroundJobBusinessLinkBackfillJobPayload(BatchSize: 20, MaxRows: 100, IncludeResult: true),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var backfillResult = await backfillHandler.HandleAsync(
            new BackgroundJobExecutionContext(new BackgroundJob { Payload = backfillPayload }),
            CancellationToken.None);

        Assert.True(backfillResult.Succeeded);
        Assert.Contains("updated=6", backfillResult.Result);

        var result = await fixture.Service.SearchAsync(
            new BackgroundJobSearchQuery
            {
                RawNoticeId = 123,
                PageSize = 20
            },
            bidOpsOnly: true);

        Assert.Equal(4, result.Total);
        Assert.Contains(result.Items, x => x.Id == 1009);
        Assert.Contains(result.Items, x => x.Id == 1011);
        Assert.Contains(result.Items, x => x.Id == 1012);
        Assert.Contains(result.Items, x => x.Id == 1014);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(BackgroundJobBusinessConstants.BidOpsSourceModule, item.SourceModule);
            Assert.Equal(BackgroundJobBusinessConstants.RawNoticeBusinessType, item.BusinessType);
            Assert.Equal(123, item.BusinessId);
        });
        Assert.DoesNotContain(result.Items, x => x.Id == 1010);
        Assert.DoesNotContain(result.Items, x => x.Id == 1013);
    }

    [Fact]
    public async Task SearchByIdsAsync_ReturnsOnlyRequestedTenantScopedBidOpsJobs()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;

        fixture.DbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Id = 1011,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.document.attachment-process",
                JobName = "Attachment process",
                Payload = "{\"rawNoticeId\":123,\"forceParseRunId\":null}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now.AddMinutes(-1),
                AvailableAtUtc = now.AddMinutes(-1),
                AttemptCount = 0,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1012,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.ai.structured-parse",
                JobName = "Structured parse",
                Payload = "{\"rawNoticeId\": 1234}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 0,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1013,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "bidops",
                JobType = "bidops.manual-url-import",
                JobName = "Manual URL import",
                Payload = "{\"detailUrl\":\"https://example.test/notice/123\"}",
                Result = "rawNoticeId=123",
                Status = BackgroundJobStatus.Succeeded,
                CreatedAt = now.AddMinutes(-2),
                AvailableAtUtc = now.AddMinutes(-2),
                CompletedAtUtc = now.AddMinutes(-1),
                AttemptCount = 1,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1014,
                TenantId = 999999,
                Queue = "bidops",
                JobType = "bidops.outcome.supplier-extract",
                JobName = "Outcome supplier extract",
                Payload = "{\"rawNoticeId\":123}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 0,
                MaxAttempts = 3
            },
            new BackgroundJob
            {
                Id = 1015,
                TenantId = FixedIdentity.TestTenantId,
                Queue = "default",
                JobType = "tenant.cache-warmup",
                JobName = "Tenant cache warmup",
                Payload = "{\"rawNoticeId\":123}",
                Status = BackgroundJobStatus.Pending,
                CreatedAt = now,
                AvailableAtUtc = now,
                AttemptCount = 0,
                MaxAttempts = 3
            });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.SearchByIdsAsync(
            [1011, 1013, 1014, 1015, 9999],
            new BackgroundJobSearchQuery { PageSize = 20 },
            bidOpsOnly: true);

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, x => x.Id == 1011);
        Assert.Contains(result.Items, x => x.Id == 1013);
        Assert.DoesNotContain(result.Items, x => x.Id == 1012);
        Assert.DoesNotContain(result.Items, x => x.Id == 1014);
        Assert.DoesNotContain(result.Items, x => x.Id == 1015);
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
    public async Task RetryAsync_BidOpsOnlyPromotesManualRetryPriority()
    {
        await using var fixture = await CreateFixtureAsync();
        var originalCreatedAt = DateTime.Now.AddMinutes(-5);
        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1013,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = "bidops.ai.structured-parse",
            JobName = "Structured parse",
            Payload = "{\"rawNoticeId\":123}",
            Status = BackgroundJobStatus.Dead,
            Priority = 0,
            CreatedAt = originalCreatedAt,
            AvailableAtUtc = originalCreatedAt,
            CompletedAtUtc = originalCreatedAt.AddSeconds(2),
            AttemptCount = 5,
            MaxAttempts = 5
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.RetryAsync(1013, bidOpsOnly: true);

        Assert.NotNull(result);
        var retry = await fixture.DbContext.BackgroundJobs.SingleAsync(x => x.Id == result.NewJobId);
        Assert.Equal(100, retry.Priority);
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
    public async Task CancelAsync_RunningJobForceCancelsImmediately()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.Now;
        fixture.DbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Id = 1018,
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

        var result = await fixture.Service.CancelAsync(
            1018,
            new BackgroundJobCancelRequest
            {
                Reason = "强制停止长时间解析",
                Force = true
            });

        Assert.NotNull(result);
        Assert.False(result.IsCancellationRequested);
        Assert.Equal(BackgroundJobStatus.Canceled, result.Status);

        var job = await fixture.DbContext.BackgroundJobs.SingleAsync(x => x.Id == 1018);
        Assert.Equal(BackgroundJobStatus.Canceled, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.NotNull(job.CancellationRequestedAt);
        Assert.Null(job.LockedAtUtc);
        Assert.Null(job.LockedBy);
        Assert.Null(job.NextAttemptAtUtc);
        Assert.Equal("强制停止长时间解析", job.CancellationReason);
        Assert.Contains("Force canceled by operator", job.Result);
    }

    [Fact]
    public async Task Worker_ProcessesHigherPriorityPendingJobFirst()
    {
        await using var fixture = await CreateWorkerFixtureAsync();
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1014,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Older low priority job",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            Priority = 0,
            CreatedAt = now.AddMinutes(-10),
            AvailableAtUtc = now.AddMinutes(-10),
            AttemptCount = 0,
            MaxAttempts = 1
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1015,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Newer manual priority job",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            Priority = 100,
            CreatedAt = now.AddMinutes(-1),
            AvailableAtUtc = now.AddMinutes(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var lowPriority = await fixture.GetJobAsync(1014);
        var manualPriority = await fixture.GetJobAsync(1015);

        Assert.Equal(1, processed);
        Assert.Equal(BackgroundJobStatus.Pending, lowPriority.Status);
        Assert.Equal(BackgroundJobStatus.Succeeded, manualPriority.Status);
    }

    [Fact]
    public async Task Worker_ProcessesConfiguredConcurrencyInParallel()
    {
        var parallelHandler = new ParallelBarrierHandler(expectedStarts: 2);
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options =>
            {
                options.BatchSize = 2;
                options.MaxConcurrency = 2;
            },
            extraHandlers: [parallelHandler]);
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1020,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = ParallelBarrierHandler.Type,
            JobName = "Parallel job 1",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now.AddSeconds(-2),
            AvailableAtUtc = now.AddSeconds(-2),
            AttemptCount = 0,
            MaxAttempts = 1
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1021,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = ParallelBarrierHandler.Type,
            JobName = "Parallel job 2",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now.AddSeconds(-1),
            AvailableAtUtc = now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var first = await fixture.GetJobAsync(1020);
        var second = await fixture.GetJobAsync(1021);

        Assert.Equal(2, processed);
        Assert.Equal(2, parallelHandler.MaxObservedConcurrency);
        Assert.Equal(BackgroundJobStatus.Succeeded, first.Status);
        Assert.Equal(BackgroundJobStatus.Succeeded, second.Status);
    }

    [Fact]
    public async Task Worker_RespectsJobTypeConcurrencyLimitAndFillsOtherWork()
    {
        var limitedHandler = new ImmediateResultHandler();
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options =>
            {
                options.BatchSize = 3;
                options.MaxConcurrency = 3;
                options.JobTypeConcurrency[ImmediateResultHandler.Type] = 1;
            },
            extraHandlers: [limitedHandler]);
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1022,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = ImmediateResultHandler.Type,
            JobName = "Limited job 1",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now.AddSeconds(-3),
            AvailableAtUtc = now.AddSeconds(-3),
            AttemptCount = 0,
            MaxAttempts = 1
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1023,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = ImmediateResultHandler.Type,
            JobName = "Limited job 2",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now.AddSeconds(-2),
            AvailableAtUtc = now.AddSeconds(-2),
            AttemptCount = 0,
            MaxAttempts = 1
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1024,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Other job",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now.AddSeconds(-1),
            AvailableAtUtc = now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var first = await fixture.GetJobAsync(1022);
        var second = await fixture.GetJobAsync(1023);
        var other = await fixture.GetJobAsync(1024);

        Assert.Equal(2, processed);
        Assert.Equal(BackgroundJobStatus.Succeeded, first.Status);
        Assert.Equal(BackgroundJobStatus.Pending, second.Status);
        Assert.Equal(BackgroundJobStatus.Succeeded, other.Status);
    }

    [Fact]
    public async Task Worker_OnlyClaimsIncludedJobTypes()
    {
        var allowedHandler = new ImmediateResultHandler();
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options =>
            {
                options.BatchSize = 2;
                options.MaxConcurrency = 2;
                options.IncludedJobTypes = [ImmediateResultHandler.Type];
            },
            extraHandlers: [allowedHandler]);
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1025,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Disallowed high priority job",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            Priority = 100,
            CreatedAt = now.AddSeconds(-2),
            AvailableAtUtc = now.AddSeconds(-2),
            AttemptCount = 0,
            MaxAttempts = 1
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1026,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = ImmediateResultHandler.Type,
            JobName = "Allowed low priority job",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            Priority = 0,
            CreatedAt = now.AddSeconds(-1),
            AvailableAtUtc = now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var disallowed = await fixture.GetJobAsync(1025);
        var allowed = await fixture.GetJobAsync(1026);

        Assert.Equal(1, processed);
        Assert.Equal(BackgroundJobStatus.Pending, disallowed.Status);
        Assert.Equal(BackgroundJobStatus.Succeeded, allowed.Status);
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
    public async Task Worker_CancelsStaleRunningJobWithTerminationRequestBeforeNewWork()
    {
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options => options.ProcessingTimeoutSeconds = 30);
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1016,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Stale cancel requested handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Running,
            CreatedAt = now.AddMinutes(-10),
            AvailableAtUtc = now.AddMinutes(-10),
            StartedAtUtc = now.AddMinutes(-10),
            LockedAtUtc = now.AddMinutes(-10),
            LockedBy = "worker-stale",
            CancellationRequestedAt = now.AddMinutes(-5),
            CancellationReason = "停止旧任务",
            AttemptCount = 1,
            MaxAttempts = 5
        });
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1017,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "High priority new work",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            Priority = 100,
            CreatedAt = now.AddMinutes(-1),
            AvailableAtUtc = now.AddMinutes(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var stale = await fixture.GetJobAsync(1016);
        var pending = await fixture.GetJobAsync(1017);

        Assert.Equal(2, processed);
        Assert.Equal(BackgroundJobStatus.Canceled, stale.Status);
        Assert.NotNull(stale.CompletedAtUtc);
        Assert.Null(stale.LockedAtUtc);
        Assert.Null(stale.LockedBy);
        Assert.Equal("停止旧任务", stale.CancellationReason);
        Assert.Contains("Canceled by operator", stale.Result);
        Assert.Equal(BackgroundJobStatus.Succeeded, pending.Status);
    }

    [Fact]
    public async Task Worker_ForceTerminatesRunningJobOlderThanMaxRunningTime()
    {
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options => options.MaxRunningSeconds = 120);
        var now = DateTime.Now;
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1010,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Stale running handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Running,
            CreatedAt = now.AddHours(-3),
            AvailableAtUtc = now.AddHours(-3),
            StartedAtUtc = now.AddHours(-3),
            LockedAtUtc = now.AddHours(-3),
            LockedBy = "worker-stale",
            AttemptCount = 1,
            MaxAttempts = 5
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var job = await fixture.GetJobAsync(1010);

        Assert.Equal(1, processed);
        Assert.Equal(BackgroundJobStatus.Dead, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Null(job.LockedAtUtc);
        Assert.Null(job.LockedBy);
        Assert.Null(job.NextAttemptAtUtc);
        Assert.Contains("Force terminated by timeout watchdog", job.LastError);
        Assert.Contains("2 minutes", job.Result);
    }

    [Fact]
    public async Task Worker_MarksActiveJobDeadWhenItExceedsMaxRunningTime()
    {
        await using var fixture = await CreateWorkerFixtureAsync(
            configureOptions: options => options.MaxRunningSeconds = 1);
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1011,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = BlockingCancellationHandler.Type,
            JobName = "Timeout handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = DateTime.Now.AddSeconds(-1),
            AvailableAtUtc = DateTime.Now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 5
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var job = await fixture.GetJobAsync(1011);

        Assert.Equal(1, processed);
        Assert.True(fixture.Handler.SawCancellation);
        Assert.Equal(BackgroundJobStatus.Dead, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Null(job.LockedAtUtc);
        Assert.Null(job.LockedBy);
        Assert.Null(job.NextAttemptAtUtc);
        Assert.Contains("Force terminated by timeout watchdog", job.LastError);
        Assert.Contains("1 seconds", job.Result);
    }

    [Fact]
    public async Task ProgressReporter_UpdatesRunningJobResult()
    {
        await using var fixture = await CreateWorkerFixtureAsync();
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1012,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Progress handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Running,
            CreatedAt = DateTime.Now.AddMinutes(-1),
            AvailableAtUtc = DateTime.Now.AddMinutes(-1),
            StartedAtUtc = DateTime.Now.AddMinutes(-1),
            LockedAtUtc = DateTime.Now.AddMinutes(-1),
            LockedBy = "worker-test",
            AttemptCount = 1,
            MaxAttempts = 5
        });

        var reporter = fixture.GetRequiredService<IBackgroundJobProgressReporter>();
        await reporter.ReportAsync(
            1012,
            "notice-structured-parse",
            "AI 正在结构化解析公告",
            new Dictionary<string, object?> { ["rawNoticeId"] = 123 });
        var job = await fixture.GetJobAsync(1012);

        Assert.Equal(BackgroundJobStatus.Running, job.Status);
        Assert.Contains("AI 正在结构化解析公告", job.Result);
        Assert.Contains("notice-structured-parse", job.Result);
        Assert.Contains("rawNoticeId", job.Result);
        Assert.NotNull(job.UpdatedAt);
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

    [Fact]
    public async Task Worker_DefersJobWhenExecutionGateBlocksWithoutConsumingAttempt()
    {
        await using var fixture = await CreateWorkerFixtureAsync(new BlockingExecutionGate());
        await fixture.SeedJobAsync(new BackgroundJob
        {
            Id = 1009,
            TenantId = FixedIdentity.TestTenantId,
            Queue = "bidops",
            JobType = LongResultHandler.Type,
            JobName = "Deferred handler",
            Payload = "{}",
            Status = BackgroundJobStatus.Pending,
            CreatedAt = DateTime.Now.AddSeconds(-1),
            AvailableAtUtc = DateTime.Now.AddSeconds(-1),
            AttemptCount = 0,
            MaxAttempts = 1
        });

        var processed = await fixture.Worker.ProcessOnceAsync();
        var job = await fixture.GetJobAsync(1009);

        Assert.Equal(1, processed);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.StartedAtUtc);
        Assert.Null(job.LockedAtUtc);
        Assert.Null(job.LockedBy);
        Assert.Null(job.CompletedAtUtc);
        Assert.NotNull(job.NextAttemptAtUtc);
        Assert.Contains("paused", job.Result);
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

    private static async Task<WorkerFixture> CreateWorkerFixtureAsync(
        IBackgroundJobExecutionGate? executionGate = null,
        Action<BackgroundJobWorkerOptions>? configureOptions = null,
        IReadOnlyCollection<IBackgroundJobHandler>? extraHandlers = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var identity = new FixedIdentity();
        var handler = new BlockingCancellationHandler();
        var workerOptions = new BackgroundJobWorkerOptions
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
        };
        configureOptions?.Invoke(workerOptions);
        var options = Options.Create(workerOptions);

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddDbContext<AtlasGlobalDbContext>((provider, builder) =>
            builder.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddSingleton<ICurrentIdentity>(identity);
        services.AddSingleton<IOptions<BackgroundJobWorkerOptions>>(options);
        services.AddSingleton<BackgroundWorkerHeartbeatState>();
        services.AddSingleton<IBackgroundJobProgressReporter, BackgroundJobProgressReporter>();
        services.AddSingleton(handler);
        services.AddScoped<IBackgroundJobHandler>(provider => provider.GetRequiredService<BlockingCancellationHandler>());
        services.AddSingleton<LongResultHandler>();
        services.AddScoped<IBackgroundJobHandler>(provider => provider.GetRequiredService<LongResultHandler>());
        if (extraHandlers != null)
        {
            foreach (var extraHandler in extraHandlers)
                services.AddSingleton(extraHandler);
        }
        services.AddSingleton<IIdGenerator>(new IncrementingIdGenerator(3000));
        services.AddSingleton<ISensitiveJsonMasker, SensitiveJsonMasker>();
        services.AddScoped<IBackgroundJobOperationsService, BackgroundJobOperationsService>();
        if (executionGate != null)
            services.AddScoped<IBackgroundJobExecutionGate>(_ => executionGate);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<BackgroundJobWorker>>(
            NullLogger<BackgroundJobWorker>.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<BackgroundJobProgressReporter>>(
            NullLogger<BackgroundJobProgressReporter>.Instance);
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

        public T GetRequiredService<T>()
            where T : notnull
        {
            return _provider.GetRequiredService<T>();
        }

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

    private sealed class ImmediateResultHandler : IBackgroundJobHandler
    {
        public const string Type = "test.immediate-result";

        public string JobType => Type;

        public Task<BackgroundJobExecutionResult> HandleAsync(
            BackgroundJobExecutionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(BackgroundJobExecutionResult.Success("ok"));
        }
    }

    private sealed class ParallelBarrierHandler : IBackgroundJobHandler
    {
        public const string Type = "test.parallel-barrier";

        private readonly int _expectedStarts;
        private readonly TaskCompletionSource _allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startedCount;
        private int _runningCount;
        private int _maxObservedConcurrency;

        public ParallelBarrierHandler(int expectedStarts)
        {
            _expectedStarts = expectedStarts;
        }

        public string JobType => Type;

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

        public async Task<BackgroundJobExecutionResult> HandleAsync(
            BackgroundJobExecutionContext context,
            CancellationToken ct = default)
        {
            var running = Interlocked.Increment(ref _runningCount);
            UpdateMaxObservedConcurrency(running);

            if (Interlocked.Increment(ref _startedCount) >= _expectedStarts)
                _allStarted.TrySetResult();

            try
            {
                await _allStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), ct);
                await Task.Delay(50, ct);
                return BackgroundJobExecutionResult.Success("ok");
            }
            finally
            {
                Interlocked.Decrement(ref _runningCount);
            }
        }

        private void UpdateMaxObservedConcurrency(int running)
        {
            while (true)
            {
                var observed = Volatile.Read(ref _maxObservedConcurrency);
                if (running <= observed)
                    return;

                if (Interlocked.CompareExchange(ref _maxObservedConcurrency, running, observed) == observed)
                    return;
            }
        }
    }

    private sealed class BlockingExecutionGate : IBackgroundJobExecutionGate
    {
        public Task<BackgroundJobExecutionGateDecision> EvaluateAsync(
            BackgroundJob job,
            CancellationToken ct = default)
        {
            return Task.FromResult(BackgroundJobExecutionGateDecision.Defer(
                "paused by test",
                DateTime.Now.AddMinutes(1)));
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
