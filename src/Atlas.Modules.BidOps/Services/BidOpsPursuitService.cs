using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Entities.Matching;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Pursuits;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsPursuitService : IBidOpsPursuitService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IRepository<Pursuit> _pursuits;
    private readonly IRepository<PursuitTask> _tasks;
    private readonly IRepository<PursuitFollowRecord> _followRecords;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<Opportunity> _opportunities;
    private readonly IRepository<GoNoGoDecision> _decisions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;

    public BidOpsPursuitService(
        IRepository<Pursuit> pursuits,
        IRepository<PursuitTask> tasks,
        IRepository<PursuitFollowRecord> followRecords,
        IRepository<TenderPackage> packages,
        IRepository<Notice> notices,
        IRepository<Opportunity> opportunities,
        IRepository<GoNoGoDecision> decisions,
        IUnitOfWork unitOfWork,
        ICurrentIdentity current,
        IIdGenerator idGenerator)
    {
        _pursuits = pursuits ?? throw new ArgumentNullException(nameof(pursuits));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _followRecords = followRecords ?? throw new ArgumentNullException(nameof(followRecords));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<PagedResult<PursuitDto>> SearchAsync(
        PursuitSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _pursuits.QueryDataScopeAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.PursuitNo.Contains(keyword) ||
                x.Title.Contains(keyword) ||
                x.SupplierNameSnapshot.Contains(keyword) ||
                x.Remark.Contains(keyword));
        }

        if (query.PackageId.HasValue)
            builder = builder.Where(x => x.PackageId == query.PackageId.Value);

        if (query.OpportunityId.HasValue)
            builder = builder.Where(x => x.OpportunityId == query.OpportunityId.Value);

        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            var stage = query.Stage.Trim();
            builder = builder.Where(x => x.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            builder = builder.Where(x => x.Status == status);
        }

        if (query.OwnerUserId.HasValue)
            builder = builder.Where(x => x.OwnerUserId == query.OwnerUserId.Value);

        if (query.MineOnly == true && _current.UserId.HasValue)
        {
            var userId = _current.UserId.Value;
            builder = builder.Where(x => x.OwnerUserId == userId);
        }

        if (query.OverdueOnly == true)
        {
            var now = DateTime.UtcNow;
            builder = builder.Where(x =>
                x.Status == BidOpsPursuitStatuses.Active &&
                x.BidDeadlineAtUtc.HasValue &&
                x.BidDeadlineAtUtc.Value < now);
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PursuitDto>(
            total,
            await MapListAsync(items, ct),
            pageIndex,
            pageSize);
    }

    public async Task<PursuitDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(id, tracking: false, ct);
        if (pursuit == null)
            return null;

        var package = await GetPackageAsync(pursuit.PackageId, tracking: false, ct);
        var notice = package == null ? null : await GetNoticeAsync(package.NoticeId, ct);
        var opportunity = pursuit.OpportunityId.HasValue
            ? await GetOpportunityAsync(pursuit.OpportunityId.Value, tracking: false, ct)
            : null;

        return new PursuitDetailDto
        {
            Pursuit = (await MapListAsync(new[] { pursuit }, ct)).Single(),
            Package = package == null ? null : MapPackage(package, notice),
            Opportunity = opportunity == null ? null : MapOpportunity(opportunity, package, notice),
            Tasks = (await ListTasksCoreAsync(id, ct)).ToList(),
            FollowRecords = (await ListFollowRecordsCoreAsync(id, ct)).ToList()
        };
    }

    public async Task<PursuitDto> CreateAsync(
        CreatePursuitRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var package = await GetPackageAsync(request.PackageId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps package does not exist: {request.PackageId}");
        await EnsureNoActivePursuitAsync(package.Id, excludingPursuitId: null, ct);

        Opportunity? opportunity = null;
        if (request.OpportunityId.HasValue)
        {
            opportunity = await GetOpportunityAsync(request.OpportunityId.Value, tracking: false, ct)
                ?? throw new AtlasException($"BidOps opportunity does not exist: {request.OpportunityId.Value}");
            if (opportunity.PackageId != package.Id)
                throw new AtlasException("商机不属于当前包件。");
        }

        if (request.GoNoGoDecisionId.HasValue)
        {
            var decision = await GetDecisionAsync(request.GoNoGoDecisionId.Value, ct)
                ?? throw new AtlasException($"BidOps go/no-go decision does not exist: {request.GoNoGoDecisionId.Value}");
            if (decision.PackageId != package.Id)
                throw new AtlasException("立项决策不属于当前包件。");
            if (decision.Decision != BidOpsGoNoGoDecisions.Go)
                throw new AtlasException("只有立项决策为 Go 的记录才能直接创建投标作业。");
        }

        var now = DateTime.UtcNow;
        var notice = await GetNoticeAsync(package.NoticeId, ct);
        var pursuitId = _idGenerator.NextId();
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? BuildPursuitTitle(package, opportunity)
            : request.Title.Trim();
        var pursuit = new Pursuit
        {
            Id = pursuitId,
            TenantId = tenantId,
            NoticeId = package.NoticeId,
            PackageId = package.Id,
            OpportunityId = request.OpportunityId,
            GoNoGoDecisionId = request.GoNoGoDecisionId,
            SupplierId = request.SupplierId,
            SupplierNameSnapshot = Truncate(request.SupplierNameSnapshot, 300),
            PursuitNo = $"PUR-{now:yyyyMMdd}-{Math.Abs(pursuitId % 1_000_000):D6}",
            Title = Truncate(title, 500),
            Stage = BidOpsPursuitStages.New,
            Status = BidOpsPursuitStatuses.Active,
            ActiveMarker = BidOpsPursuitActiveMarkers.Active,
            Priority = NormalizePriority(request.Priority),
            EstimatedAmount = request.EstimatedAmount ?? opportunity?.EstimatedAmount ?? package.BudgetAmount ?? package.MaxPrice,
            BidDeadlineAtUtc = request.BidDeadlineAtUtc ?? notice?.BidDeadline,
            OwnerUserId = request.OwnerUserId ?? _current.UserId,
            ProgressPercent = 0,
            RiskLevel = BidOpsPursuitRiskLevels.None,
            LastStageChangedAtUtc = now,
            Remark = Truncate(request.Remark, 1000),
            CreatedAt = now
        };

        await _pursuits.AddAsync(pursuit, ct);
        await _followRecords.AddAsync(CreateFollowRecord(
            pursuit,
            BidOpsPursuitFollowTypes.StatusChange,
            "创建投标作业",
            nextActionAtUtc: null,
            now),
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return (await MapListAsync(new[] { pursuit }, ct)).Single();
    }

    public async Task<PursuitDto> UpdateAsync(
        long id,
        UpdatePursuitRequest request,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps pursuit does not exist: {id}");

        if (!string.IsNullOrWhiteSpace(request.Title))
            pursuit.Title = Truncate(request.Title.Trim(), 500);
        if (request.Priority.HasValue)
            pursuit.Priority = NormalizePriority(request.Priority);
        if (request.EstimatedAmount.HasValue)
            pursuit.EstimatedAmount = request.EstimatedAmount.Value;
        if (request.BidDeadlineAtUtc.HasValue)
            pursuit.BidDeadlineAtUtc = request.BidDeadlineAtUtc.Value;
        if (request.OwnerUserId.HasValue)
            pursuit.OwnerUserId = request.OwnerUserId.Value;
        if (request.ProgressPercent.HasValue)
            pursuit.ProgressPercent = Math.Clamp(request.ProgressPercent.Value, 0, 100);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel))
            pursuit.RiskLevel = NormalizeRiskLevel(request.RiskLevel);
        if (request.Remark != null)
            pursuit.Remark = Truncate(request.Remark, 1000);

        pursuit.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapListAsync(new[] { pursuit }, ct)).Single();
    }

    public async Task<PursuitDto> ChangeStatusAsync(
        long id,
        ChangePursuitStatusRequest request,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps pursuit does not exist: {id}");
        var newStage = NormalizeStage(request.Stage);
        var newStatus = string.IsNullOrWhiteSpace(request.Status)
            ? InferStatusFromStage(newStage)
            : NormalizeStatus(request.Status);
        if (newStatus == BidOpsPursuitStatuses.Active && pursuit.ActiveMarker != BidOpsPursuitActiveMarkers.Active)
            await EnsureNoActivePursuitAsync(pursuit.PackageId, pursuit.Id, ct);

        var now = DateTime.UtcNow;
        var oldStage = pursuit.Stage;
        pursuit.Stage = newStage;
        pursuit.Status = newStatus;
        pursuit.ActiveMarker = newStatus == BidOpsPursuitStatuses.Active
            ? BidOpsPursuitActiveMarkers.Active
            : null;
        pursuit.LastStageChangedAtUtc = now;
        pursuit.UpdatedAt = now;
        pursuit.ProgressPercent = Math.Max(pursuit.ProgressPercent, ProgressFromStage(newStage));

        await _followRecords.AddAsync(CreateFollowRecord(
            pursuit,
            BidOpsPursuitFollowTypes.StatusChange,
            BuildStatusChangeContent(oldStage, newStage, request.Reason),
            nextActionAtUtc: null,
            now),
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return (await MapListAsync(new[] { pursuit }, ct)).Single();
    }

    public async Task<IReadOnlyList<PursuitTaskDto>> ListTasksAsync(
        long pursuitId,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(pursuitId, tracking: false, ct);
        return pursuit == null ? [] : await ListTasksCoreAsync(pursuitId, ct);
    }

    public async Task<PursuitTaskDto> CreateTaskAsync(
        long pursuitId,
        CreatePursuitTaskRequest request,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(pursuitId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps pursuit does not exist: {pursuitId}");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AtlasException("任务标题不能为空。");

        var now = DateTime.UtcNow;
        var task = new PursuitTask
        {
            Id = _idGenerator.NextId(),
            TenantId = pursuit.TenantId,
            PursuitId = pursuitId,
            Title = Truncate(request.Title.Trim(), 300),
            TaskType = NormalizeTaskType(request.TaskType),
            Status = BidOpsPursuitTaskStatuses.Todo,
            Priority = NormalizePriority(request.Priority),
            OwnerUserId = request.OwnerUserId ?? pursuit.OwnerUserId,
            DueAtUtc = request.DueAtUtc,
            Description = Truncate(request.Description, 2000),
            CreatedAt = now
        };

        await _tasks.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapTask(task);
    }

    public async Task<PursuitTaskDto> UpdateTaskAsync(
        long pursuitId,
        long taskId,
        UpdatePursuitTaskRequest request,
        CancellationToken ct = default)
    {
        _ = await GetPursuitAsync(pursuitId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps pursuit does not exist: {pursuitId}");
        var task = await GetTaskAsync(pursuitId, taskId, tracking: true, ct)
            ?? throw new AtlasException($"BidOps pursuit task does not exist: {taskId}");

        if (!string.IsNullOrWhiteSpace(request.Title))
            task.Title = Truncate(request.Title.Trim(), 300);
        if (!string.IsNullOrWhiteSpace(request.TaskType))
            task.TaskType = NormalizeTaskType(request.TaskType);
        if (!string.IsNullOrWhiteSpace(request.Status))
            task.Status = NormalizeTaskStatus(request.Status);
        if (request.Priority.HasValue)
            task.Priority = NormalizePriority(request.Priority);
        if (request.OwnerUserId.HasValue)
            task.OwnerUserId = request.OwnerUserId.Value;
        if (request.DueAtUtc.HasValue)
            task.DueAtUtc = request.DueAtUtc.Value;
        if (request.Description != null)
            task.Description = Truncate(request.Description, 2000);
        if (request.ResultNote != null)
            task.ResultNote = Truncate(request.ResultNote, 2000);

        task.CompletedAtUtc = task.Status == BidOpsPursuitTaskStatuses.Done
            ? task.CompletedAtUtc ?? DateTime.UtcNow
            : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return MapTask(task);
    }

    public async Task<IReadOnlyList<PursuitFollowRecordDto>> ListFollowRecordsAsync(
        long pursuitId,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(pursuitId, tracking: false, ct);
        return pursuit == null ? [] : await ListFollowRecordsCoreAsync(pursuitId, ct);
    }

    public async Task<PursuitFollowRecordDto> CreateFollowRecordAsync(
        long pursuitId,
        CreatePursuitFollowRecordRequest request,
        CancellationToken ct = default)
    {
        var pursuit = await GetPursuitAsync(pursuitId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps pursuit does not exist: {pursuitId}");
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new AtlasException("跟进内容不能为空。");

        var record = CreateFollowRecord(
            pursuit,
            NormalizeFollowType(request.FollowType),
            request.Content,
            request.NextActionAtUtc,
            DateTime.UtcNow);
        await _followRecords.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapFollowRecord(record);
    }

    private async Task<List<PursuitDto>> MapListAsync(
        IReadOnlyCollection<Pursuit> pursuits,
        CancellationToken ct)
    {
        if (pursuits.Count == 0)
            return [];

        var packageIds = pursuits.Select(x => x.PackageId).Distinct().ToArray();
        var packagesQuery = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var packages = await packagesQuery
            .Where(x => packageIds.Contains(x.Id))
            .ToListAsync(ct);
        var noticeIds = packages.Select(x => x.NoticeId).Concat(pursuits.Select(x => x.NoticeId)).Distinct().ToArray();
        var noticesQuery = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        var notices = await noticesQuery
            .Where(x => noticeIds.Contains(x.Id))
            .ToListAsync(ct);
        var pursuitIds = pursuits.Select(x => x.Id).ToArray();
        var taskQuery = await _tasks.QueryDataScopeAsync(BidOpsDataResources.PursuitTask, AtlasDataScopeType.AllTenant, ct);
        var tasks = await taskQuery
            .Where(x => pursuitIds.Contains(x.PursuitId))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;

        var packageMap = packages.ToDictionary(x => x.Id);
        var noticeMap = notices.ToDictionary(x => x.Id);
        return pursuits.Select(pursuit =>
        {
            packageMap.TryGetValue(pursuit.PackageId, out var package);
            noticeMap.TryGetValue(pursuit.NoticeId, out var notice);
            var relatedTasks = tasks.Where(x => x.PursuitId == pursuit.Id).ToList();
            return MapPursuit(pursuit, package, notice, relatedTasks, now);
        }).ToList();
    }

    private PursuitDto MapPursuit(
        Pursuit pursuit,
        TenderPackage? package,
        Notice? notice,
        IReadOnlyCollection<PursuitTask> tasks,
        DateTime now)
    {
        var openTasks = tasks.Count(IsOpenTask);
        var overdueTasks = tasks.Count(x => IsOpenTask(x) && x.DueAtUtc.HasValue && x.DueAtUtc.Value < now);
        return new PursuitDto
        {
            Id = pursuit.Id,
            NoticeId = pursuit.NoticeId,
            PackageId = pursuit.PackageId,
            OpportunityId = pursuit.OpportunityId,
            GoNoGoDecisionId = pursuit.GoNoGoDecisionId,
            SupplierId = pursuit.SupplierId,
            SupplierNameSnapshot = pursuit.SupplierNameSnapshot,
            PursuitNo = pursuit.PursuitNo,
            Title = pursuit.Title,
            NoticeTitle = notice?.Title ?? string.Empty,
            PackageNo = package?.PackageNo ?? string.Empty,
            PackageName = package?.PackageName ?? string.Empty,
            ProjectName = notice?.ProjectName ?? string.Empty,
            ProjectCode = notice?.ProjectCode ?? string.Empty,
            BuyerName = notice?.BuyerName ?? string.Empty,
            Region = notice?.Region ?? package?.DeliveryPlace ?? string.Empty,
            Stage = pursuit.Stage,
            Status = pursuit.Status,
            Priority = pursuit.Priority,
            EstimatedAmount = pursuit.EstimatedAmount,
            BidDeadlineAtUtc = pursuit.BidDeadlineAtUtc,
            OwnerUserId = pursuit.OwnerUserId,
            ProgressPercent = pursuit.ProgressPercent,
            RiskLevel = pursuit.RiskLevel,
            TaskCount = tasks.Count,
            OpenTaskCount = openTasks,
            OverdueTaskCount = overdueTasks,
            LastStageChangedAtUtc = pursuit.LastStageChangedAtUtc,
            Remark = pursuit.Remark,
            CreatedAt = pursuit.CreatedAt,
            UpdatedAt = pursuit.UpdatedAt
        };
    }

    private TenderPackageDto MapPackage(TenderPackage package, Notice? notice)
    {
        return new TenderPackageDto
        {
            Id = package.Id,
            NoticeId = package.NoticeId,
            NoticeTitle = notice?.Title ?? string.Empty,
            NoticeType = notice?.NoticeType ?? string.Empty,
            ProjectName = notice?.ProjectName ?? string.Empty,
            ProjectCode = notice?.ProjectCode ?? string.Empty,
            BuyerName = notice?.BuyerName ?? string.Empty,
            Region = notice?.Region ?? string.Empty,
            PublishTime = notice?.PublishTime,
            BidDeadline = notice?.BidDeadline,
            LotNo = package.LotNo,
            LotName = package.LotName,
            PackageNo = package.PackageNo,
            PackageName = package.PackageName,
            Category = package.Category,
            Quantity = package.Quantity,
            Unit = package.Unit,
            BudgetAmount = package.BudgetAmount,
            MaxPrice = package.MaxPrice,
            DeliveryPlace = package.DeliveryPlace,
            DeliveryPeriod = package.DeliveryPeriod,
            Status = package.Status,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private OpportunityDto MapOpportunity(
        Opportunity opportunity,
        TenderPackage? package,
        Notice? notice)
    {
        return new OpportunityDto
        {
            Id = opportunity.Id,
            NoticeId = opportunity.NoticeId,
            PackageId = opportunity.PackageId,
            OpportunityNo = opportunity.OpportunityNo,
            Title = opportunity.Title,
            NoticeTitle = notice?.Title ?? string.Empty,
            PackageName = package?.PackageName ?? string.Empty,
            PackageNo = package?.PackageNo ?? string.Empty,
            ProjectName = notice?.ProjectName ?? string.Empty,
            ProjectCode = notice?.ProjectCode ?? string.Empty,
            BuyerName = notice?.BuyerName ?? string.Empty,
            Region = notice?.Region ?? string.Empty,
            PublishTime = notice?.PublishTime,
            BidDeadline = notice?.BidDeadline,
            Stage = opportunity.Stage,
            Status = opportunity.Status,
            Priority = opportunity.Priority,
            EstimatedAmount = opportunity.EstimatedAmount,
            ValueScore = opportunity.ValueScore,
            ValueLevel = opportunity.ValueLevel,
            Decision = opportunity.Decision,
            OwnerUserId = opportunity.OwnerUserId,
            NextActionAtUtc = opportunity.NextActionAtUtc,
            LastStageChangedAtUtc = opportunity.LastStageChangedAtUtc,
            AssessmentSummary = opportunity.AssessmentSummary,
            Remark = opportunity.Remark,
            WatchCount = 0,
            WatchedByMe = false,
            CreatedAt = opportunity.CreatedAt,
            UpdatedAt = opportunity.UpdatedAt
        };
    }

    private async Task<IReadOnlyList<PursuitTaskDto>> ListTasksCoreAsync(
        long pursuitId,
        CancellationToken ct)
    {
        var query = await _tasks.QueryDataScopeAsync(BidOpsDataResources.PursuitTask, AtlasDataScopeType.AllTenant, ct);
        var tasks = await query
            .Where(x => x.PursuitId == pursuitId)
            .ToListAsync(ct);
        return tasks
            .OrderBy(x => x.Status == BidOpsPursuitTaskStatuses.Done)
            .ThenBy(x => x.DueAtUtc)
            .ThenByDescending(x => x.Priority)
            .Select(MapTask)
            .ToList();
    }

    private async Task<IReadOnlyList<PursuitFollowRecordDto>> ListFollowRecordsCoreAsync(
        long pursuitId,
        CancellationToken ct)
    {
        var query = await _followRecords.QueryDataScopeAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct);
        var records = await query
            .Where(x => x.PursuitId == pursuitId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return records.Select(MapFollowRecord).ToList();
    }

    private static PursuitTaskDto MapTask(PursuitTask task)
    {
        return new PursuitTaskDto
        {
            Id = task.Id,
            PursuitId = task.PursuitId,
            Title = task.Title,
            TaskType = task.TaskType,
            Status = task.Status,
            Priority = task.Priority,
            OwnerUserId = task.OwnerUserId,
            DueAtUtc = task.DueAtUtc,
            CompletedAtUtc = task.CompletedAtUtc,
            Description = task.Description,
            ResultNote = task.ResultNote,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }

    private static PursuitFollowRecordDto MapFollowRecord(PursuitFollowRecord record)
    {
        return new PursuitFollowRecordDto
        {
            Id = record.Id,
            PursuitId = record.PursuitId,
            FollowType = record.FollowType,
            Content = record.Content,
            NextActionAtUtc = record.NextActionAtUtc,
            CreatedByUserId = record.CreatedByUserId,
            CreatedByUserName = record.CreatedByUserName,
            CreatedAt = record.CreatedAt
        };
    }

    private PursuitFollowRecord CreateFollowRecord(
        Pursuit pursuit,
        string followType,
        string content,
        DateTime? nextActionAtUtc,
        DateTime now)
    {
        return new PursuitFollowRecord
        {
            Id = _idGenerator.NextId(),
            TenantId = pursuit.TenantId,
            PursuitId = pursuit.Id,
            FollowType = followType,
            Content = Truncate(content, 2000),
            NextActionAtUtc = nextActionAtUtc,
            CreatedByUserId = _current.UserId,
            CreatedByUserName = Truncate(_current.UserName, 128),
            CreatedAt = now
        };
    }

    private async Task EnsureNoActivePursuitAsync(
        long packageId,
        long? excludingPursuitId,
        CancellationToken ct)
    {
        var query = await _pursuits.QueryDataScopeAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct);
        var exists = await query
            .Where(x =>
                x.PackageId == packageId &&
                x.ActiveMarker == BidOpsPursuitActiveMarkers.Active &&
                (!excludingPursuitId.HasValue || x.Id != excludingPursuitId.Value))
            .AnyAsync(ct);
        if (exists)
            throw new AtlasException("当前包件已存在有效投标作业。");
    }

    private async Task<Pursuit?> GetPursuitAsync(
        long id,
        bool tracking,
        CancellationToken ct)
    {
        var query = tracking
            ? await _pursuits.QueryDataScopeTrackingAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct)
            : await _pursuits.QueryDataScopeAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<PursuitTask?> GetTaskAsync(
        long pursuitId,
        long taskId,
        bool tracking,
        CancellationToken ct)
    {
        var query = tracking
            ? await _tasks.QueryDataScopeTrackingAsync(BidOpsDataResources.PursuitTask, AtlasDataScopeType.AllTenant, ct)
            : await _tasks.QueryDataScopeAsync(BidOpsDataResources.PursuitTask, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.PursuitId == pursuitId && x.Id == taskId).FirstOrDefaultAsync(ct);
    }

    private async Task<TenderPackage?> GetPackageAsync(
        long id,
        bool tracking,
        CancellationToken ct)
    {
        var query = tracking
            ? await _packages.QueryDataScopeTrackingAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct)
            : await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<Notice?> GetNoticeAsync(long id, CancellationToken ct)
    {
        var query = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<Opportunity?> GetOpportunityAsync(
        long id,
        bool tracking,
        CancellationToken ct)
    {
        var query = tracking
            ? await _opportunities.QueryDataScopeTrackingAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct)
            : await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<GoNoGoDecision?> GetDecisionAsync(long id, CancellationToken ct)
    {
        var query = await _decisions.QueryDataScopeAsync(BidOpsDataResources.GoNoGoDecision, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private long RequireTenantId()
    {
        return _current.TenantId ?? throw new AtlasException("BidOps pursuit operations require tenant context.");
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static int NormalizePriority(int? priority)
    {
        return Math.Clamp(priority ?? 3, 1, 5);
    }

    private static string NormalizeStage(string? value)
    {
        var stage = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitStages.Preparing : value.Trim();
        return stage switch
        {
            BidOpsPursuitStages.New => BidOpsPursuitStages.New,
            BidOpsPursuitStages.Preparing => BidOpsPursuitStages.Preparing,
            BidOpsPursuitStages.Review => BidOpsPursuitStages.Review,
            BidOpsPursuitStages.Submitted => BidOpsPursuitStages.Submitted,
            BidOpsPursuitStages.Awarded => BidOpsPursuitStages.Awarded,
            BidOpsPursuitStages.Closed => BidOpsPursuitStages.Closed,
            _ => throw new AtlasException($"Unsupported pursuit stage: {stage}")
        };
    }

    private static string NormalizeStatus(string? value)
    {
        var status = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitStatuses.Active : value.Trim();
        return status switch
        {
            BidOpsPursuitStatuses.Active => BidOpsPursuitStatuses.Active,
            BidOpsPursuitStatuses.Closed => BidOpsPursuitStatuses.Closed,
            BidOpsPursuitStatuses.Archived => BidOpsPursuitStatuses.Archived,
            _ => throw new AtlasException($"Unsupported pursuit status: {status}")
        };
    }

    private static string InferStatusFromStage(string stage)
    {
        return stage == BidOpsPursuitStages.Closed
            ? BidOpsPursuitStatuses.Closed
            : BidOpsPursuitStatuses.Active;
    }

    private static int ProgressFromStage(string stage)
    {
        return stage switch
        {
            BidOpsPursuitStages.New => 0,
            BidOpsPursuitStages.Preparing => 25,
            BidOpsPursuitStages.Review => 60,
            BidOpsPursuitStages.Submitted => 90,
            BidOpsPursuitStages.Awarded => 100,
            BidOpsPursuitStages.Closed => 100,
            _ => 0
        };
    }

    private static string NormalizeRiskLevel(string? value)
    {
        var riskLevel = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitRiskLevels.None : value.Trim();
        return riskLevel switch
        {
            BidOpsPursuitRiskLevels.None => BidOpsPursuitRiskLevels.None,
            BidOpsPursuitRiskLevels.Low => BidOpsPursuitRiskLevels.Low,
            BidOpsPursuitRiskLevels.Medium => BidOpsPursuitRiskLevels.Medium,
            BidOpsPursuitRiskLevels.High => BidOpsPursuitRiskLevels.High,
            _ => BidOpsPursuitRiskLevels.None
        };
    }

    private static string NormalizeTaskType(string? value)
    {
        var taskType = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitTaskTypes.Other : value.Trim();
        return taskType switch
        {
            BidOpsPursuitTaskTypes.Qualification => BidOpsPursuitTaskTypes.Qualification,
            BidOpsPursuitTaskTypes.Technical => BidOpsPursuitTaskTypes.Technical,
            BidOpsPursuitTaskTypes.Commercial => BidOpsPursuitTaskTypes.Commercial,
            BidOpsPursuitTaskTypes.Pricing => BidOpsPursuitTaskTypes.Pricing,
            BidOpsPursuitTaskTypes.Review => BidOpsPursuitTaskTypes.Review,
            BidOpsPursuitTaskTypes.Submission => BidOpsPursuitTaskTypes.Submission,
            _ => BidOpsPursuitTaskTypes.Other
        };
    }

    private static string NormalizeTaskStatus(string? value)
    {
        var status = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitTaskStatuses.Todo : value.Trim();
        return status switch
        {
            BidOpsPursuitTaskStatuses.Todo => BidOpsPursuitTaskStatuses.Todo,
            BidOpsPursuitTaskStatuses.InProgress => BidOpsPursuitTaskStatuses.InProgress,
            BidOpsPursuitTaskStatuses.Done => BidOpsPursuitTaskStatuses.Done,
            BidOpsPursuitTaskStatuses.Blocked => BidOpsPursuitTaskStatuses.Blocked,
            BidOpsPursuitTaskStatuses.Canceled => BidOpsPursuitTaskStatuses.Canceled,
            BidOpsPursuitTaskStatuses.Overdue => BidOpsPursuitTaskStatuses.Overdue,
            _ => throw new AtlasException($"Unsupported pursuit task status: {status}")
        };
    }

    private static string NormalizeFollowType(string? value)
    {
        var followType = string.IsNullOrWhiteSpace(value) ? BidOpsPursuitFollowTypes.Note : value.Trim();
        return followType switch
        {
            BidOpsPursuitFollowTypes.Note => BidOpsPursuitFollowTypes.Note,
            BidOpsPursuitFollowTypes.Call => BidOpsPursuitFollowTypes.Call,
            BidOpsPursuitFollowTypes.Meeting => BidOpsPursuitFollowTypes.Meeting,
            BidOpsPursuitFollowTypes.StatusChange => BidOpsPursuitFollowTypes.StatusChange,
            BidOpsPursuitFollowTypes.Risk => BidOpsPursuitFollowTypes.Risk,
            _ => BidOpsPursuitFollowTypes.Other
        };
    }

    private static bool IsOpenTask(PursuitTask task)
    {
        return task.Status is not BidOpsPursuitTaskStatuses.Done and not BidOpsPursuitTaskStatuses.Canceled;
    }

    private static string BuildPursuitTitle(TenderPackage package, Opportunity? opportunity)
    {
        var title = Truncate(opportunity?.Title, 500);
        if (!string.IsNullOrWhiteSpace(title))
            return title;

        title = Truncate(package.PackageName, 500);
        if (!string.IsNullOrWhiteSpace(title))
            return title;

        title = Truncate(package.PackageNo, 500);
        return string.IsNullOrWhiteSpace(title) ? $"投标作业 {package.Id}" : title;
    }

    private static string BuildStatusChangeContent(string oldStage, string newStage, string? reason)
    {
        var message = $"阶段从 {oldStage} 调整为 {newStage}";
        return string.IsNullOrWhiteSpace(reason) ? message : $"{message}：{reason.Trim()}";
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
