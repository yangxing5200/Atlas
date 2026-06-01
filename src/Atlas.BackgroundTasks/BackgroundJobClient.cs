using System.Text.Json;
using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.BackgroundTasks;

/// <summary>
/// 将一次性后台任务写入全局任务表的客户端。
/// </summary>
/// <remarks>
/// 只负责入队和查询，不直接执行任务；执行由 BackgroundJobWorker 轮询完成。
/// </remarks>
public sealed class BackgroundJobClient : IBackgroundJobClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AtlasGlobalDbContext _dbContext;
    private readonly IIdGenerator _idGenerator;
    private readonly BackgroundJobWorkerOptions _options;

    public BackgroundJobClient(
        AtlasGlobalDbContext dbContext,
        IIdGenerator idGenerator,
        IOptions<BackgroundJobWorkerOptions> options)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _options = options?.Value ?? new BackgroundJobWorkerOptions();
    }

    public async Task<BackgroundJobEnqueueResult> EnqueueAsync<TPayload>(
        EnqueueBackgroundJobRequest<TPayload> request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.JobType))
            throw new ArgumentException("Job type is required.", nameof(request));

        var jobType = request.JobType.Trim();
        var queue = NormalizeQueue(request.Queue);
        var deduplicationKey = string.IsNullOrWhiteSpace(request.DeduplicationKey)
            ? null
            : request.DeduplicationKey.Trim();

        if (deduplicationKey != null)
        {
            // 先查询一遍可避免常规重复请求触发数据库唯一约束异常。
            var existing = await _dbContext.BackgroundJobs
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == request.TenantId &&
                    x.DeduplicationKey == deduplicationKey)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                return new BackgroundJobEnqueueResult(
                    existing.Id,
                    existing.JobType,
                    existing.Queue,
                    existing.Status,
                    AlreadyExists: true);
            }
        }

        var job = new BackgroundJob
        {
            Id = _idGenerator.NextId(),
            JobType = jobType,
            Queue = queue,
            JobName = string.IsNullOrWhiteSpace(request.JobName) ? jobType : request.JobName.Trim(),
            DeduplicationKey = deduplicationKey,
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            Payload = JsonSerializer.Serialize(request.Payload, JsonOptions),
            Status = BackgroundJobStatus.Pending,
            Priority = request.Priority,
            AvailableAtUtc = request.AvailableAtUtc ?? DateTime.UtcNow,
            MaxAttempts = Math.Max(1, request.MaxAttempts ?? _options.DefaultMaxAttempts)
        };

        await _dbContext.BackgroundJobs.AddAsync(job, ct);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (deduplicationKey != null)
        {
            // 并发入队时仍可能撞上唯一索引；失败后回读已有任务作为幂等结果返回。
            _dbContext.Entry(job).State = EntityState.Detached;

            var existing = await _dbContext.BackgroundJobs
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == request.TenantId &&
                    x.DeduplicationKey == deduplicationKey)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (existing == null)
                throw;

            return new BackgroundJobEnqueueResult(
                existing.Id,
                existing.JobType,
                existing.Queue,
                existing.Status,
                AlreadyExists: true);
        }

        return new BackgroundJobEnqueueResult(
            job.Id,
            job.JobType,
            job.Queue,
            job.Status,
            AlreadyExists: false);
    }

    public Task<BackgroundJob?> FindAsync(long jobId, CancellationToken ct = default)
    {
        return _dbContext.BackgroundJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);
    }

    private static string NormalizeQueue(string? queue)
    {
        return string.IsNullOrWhiteSpace(queue)
            ? BackgroundJobQueues.Default
            : queue.Trim();
    }
}
