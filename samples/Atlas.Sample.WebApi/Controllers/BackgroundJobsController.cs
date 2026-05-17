using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Services.Tenant.BackgroundJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/background-jobs")]
[Produces("application/json")]
[Authorize]
public sealed class BackgroundJobsController : ControllerBase
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly ICurrentIdentity _currentIdentity;

    public BackgroundJobsController(
        IBackgroundJobClient jobClient,
        ICurrentIdentity currentIdentity)
    {
        _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
    }

    [HttpPost("tenant-cache-warmup")]
    public async Task<ActionResult<BackgroundJobEnqueueResult>> EnqueueTenantCacheWarmup(
        [FromBody] EnqueueTenantCacheWarmupRequest request,
        CancellationToken ct)
    {
        var tenantId = _currentIdentity.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        if (request.TenantId.HasValue && request.TenantId.Value != tenantId.Value)
            return Forbid();

        var storeId = request.StoreId ?? _currentIdentity.StoreId;
        var result = await _jobClient.EnqueueAsync(
            new EnqueueBackgroundJobRequest<TenantCacheWarmupJobPayload>
            {
                JobType = TenantBackgroundJobTypes.TenantCacheWarmup,
                Queue = TenantBackgroundJobQueues.Tenant,
                JobName = "Tenant cache warmup",
                DeduplicationKey = request.DeduplicationKey,
                TenantId = tenantId,
                StoreId = storeId,
                Priority = request.Priority,
                AvailableAtUtc = request.AvailableAtUtc,
                MaxAttempts = request.MaxAttempts,
                Payload = new TenantCacheWarmupJobPayload(
                    tenantId.Value,
                    storeId,
                    request.Reason ?? "Sample API request")
            },
            ct);

        return Accepted($"/api/background-jobs/{result.JobId}", result);
    }

    [HttpGet("{jobId:long}")]
    public async Task<ActionResult<BackgroundJobStatusResponse>> Get(long jobId, CancellationToken ct)
    {
        var tenantId = _currentIdentity.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        var job = await _jobClient.FindAsync(jobId, ct);
        if (job == null || job.TenantId != tenantId.Value)
            return NotFound();

        return new BackgroundJobStatusResponse
        {
            JobId = job.Id,
            JobType = job.JobType,
            Queue = job.Queue,
            JobName = job.JobName,
            TenantId = job.TenantId,
            StoreId = job.StoreId,
            Status = job.Status.ToString(),
            AttemptCount = job.AttemptCount,
            MaxAttempts = job.MaxAttempts,
            AvailableAtUtc = job.AvailableAtUtc,
            NextAttemptAtUtc = job.NextAttemptAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            LastError = job.LastError,
            Result = job.Result
        };
    }
}

public sealed class EnqueueTenantCacheWarmupRequest
{
    public long? TenantId { get; init; }
    public long? StoreId { get; init; }
    public string? Reason { get; init; }
    public string? DeduplicationKey { get; init; }
    public DateTime? AvailableAtUtc { get; init; }
    public int Priority { get; init; }
    public int? MaxAttempts { get; init; }
}

public sealed class BackgroundJobStatusResponse
{
    public long JobId { get; init; }
    public string JobType { get; init; } = string.Empty;
    public string Queue { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public long? TenantId { get; init; }
    public long? StoreId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime AvailableAtUtc { get; init; }
    public DateTime? NextAttemptAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? LastError { get; init; }
    public string? Result { get; init; }
}
