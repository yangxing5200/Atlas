using System.Text.Json;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsRuntimeControlService : IBidOpsRuntimeControlService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultDeferredDelay = TimeSpan.FromSeconds(30);

    private readonly IRepository<BidOpsRuntimeSetting> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _identity;
    private readonly IIdGenerator _idGenerator;

    public BidOpsRuntimeControlService(
        IRepository<BidOpsRuntimeSetting> settings,
        IUnitOfWork unitOfWork,
        ICurrentIdentity identity,
        IIdGenerator idGenerator)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<BidOpsRuntimeStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var setting = await FindTaskPauseSettingAsync(tenantId: null, tracking: false, ct);
        return BuildStatus(setting);
    }

    public async Task<BidOpsRuntimeStatusDto> SetTaskPauseAsync(
        UpdateBidOpsTaskPauseRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = NormalizeReason(request.Reason);
        var now = DateTime.Now;
        var setting = await FindTaskPauseSettingAsync(tenantId: null, tracking: true, ct);
        var value = new TaskPauseSettingValue
        {
            Paused = request.Paused,
            Reason = request.Paused
                ? (string.IsNullOrWhiteSpace(reason) ? "Paused by operator." : reason)
                : string.Empty,
            DeferredUntil = request.Paused ? now.Add(DefaultDeferredDelay) : null
        };

        if (setting == null)
        {
            setting = new BidOpsRuntimeSetting
            {
                Id = _idGenerator.NextId(),
                SettingKey = BidOpsSystemValues.TaskPauseRuntimeSettingKey,
                SettingValue = Serialize(value),
                UpdatedByUserName = _identity.UserName,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _settings.AddAsync(setting, ct);
        }
        else
        {
            setting.SettingValue = Serialize(value);
            setting.UpdatedByUserName = _identity.UserName;
            setting.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return BuildStatus(setting);
    }

    public async Task<bool> IsTaskPausedAsync(long tenantId, CancellationToken ct = default)
    {
        if (tenantId <= 0)
            return false;

        var setting = await FindTaskPauseSettingAsync(tenantId, tracking: false, ct);
        return BuildStatus(setting).TaskPaused;
    }

    public async Task EnsureTasksNotPausedAsync(CancellationToken ct = default)
    {
        if (_identity.TenantId is not > 0)
            return;

        var status = await GetStatusAsync(ct);
        if (!status.TaskPaused)
            return;

        var message = string.IsNullOrWhiteSpace(status.PauseReason)
            ? "BidOps 全局任务已暂停。"
            : $"BidOps 全局任务已暂停：{status.PauseReason}";
        throw new AtlasException(message);
    }

    private async Task<BidOpsRuntimeSetting?> FindTaskPauseSettingAsync(
        long? tenantId,
        bool tracking,
        CancellationToken ct)
    {
        var query = tenantId.HasValue
            ? tracking
                ? await _settings.QueryTrackingAsync(tenantId.Value, ct)
                : await _settings.QueryAsync(tenantId.Value, ct)
            : tracking
                ? await _settings.QueryTrackingAsync(ct)
                : await _settings.QueryAsync(ct);

        return await query
            .Where(x => x.SettingKey == BidOpsSystemValues.TaskPauseRuntimeSettingKey)
            .FirstOrDefaultAsync(ct);
    }

    private static BidOpsRuntimeStatusDto BuildStatus(BidOpsRuntimeSetting? setting)
    {
        var value = Deserialize(setting?.SettingValue);
        return new BidOpsRuntimeStatusDto
        {
            TaskPaused = value.Paused,
            PauseReason = value.Reason,
            PauseUpdatedAt = setting?.UpdatedAt ?? setting?.CreatedAt,
            PauseUpdatedByUserName = setting?.UpdatedByUserName ?? string.Empty,
            DeferredUntil = value.DeferredUntil
        };
    }

    private static string Serialize(TaskPauseSettingValue value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static TaskPauseSettingValue Deserialize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new TaskPauseSettingValue();

        if (bool.TryParse(raw, out var paused))
            return new TaskPauseSettingValue { Paused = paused };

        try
        {
            return JsonSerializer.Deserialize<TaskPauseSettingValue>(raw, JsonOptions) ?? new TaskPauseSettingValue();
        }
        catch (JsonException)
        {
            return new TaskPauseSettingValue();
        }
    }

    private static string NormalizeReason(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= 300 ? trimmed : trimmed[..300];
    }

    private sealed class TaskPauseSettingValue
    {
        public bool Paused { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime? DeferredUntil { get; set; }
    }
}
