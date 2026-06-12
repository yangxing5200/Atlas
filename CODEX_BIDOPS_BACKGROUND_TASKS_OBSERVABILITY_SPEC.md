# CODEX_BIDOPS_BACKGROUND_TASKS_OBSERVABILITY_SPEC

> 目标：为 Atlas 增加“后台任务监控、BidOps 采集/解析任务看板、日志查询”能力。
> 本文档可直接放到 Atlas 仓库根目录交给 Codex 执行。
> 重点：不要只做一个普通任务列表，而要能解释 BidOps 从“定时扫描/手动导入 -> 抓取 -> 附件处理 -> 结构化解析 -> 审核任务”的完整后台流水线。

---

## 0. 给 Codex 的总指令

你正在改造仓库：

```text
https://github.com/yangxing5200/Atlas
```

请先阅读并确认以下现状：

```text
src/Atlas.BackgroundTasks
src/Atlas.Core/Entities/Global/BackgroundJob.cs
src/Atlas.Core/Enums/BackgroundJobStatus.cs
src/Atlas.Modules.BidOps
src/Atlas.Modules.BidOps/BackgroundJobs
src/Atlas.Modules.BidOps/BidOpsConstants.cs
src/Atlas.Modules.BidOps/Controllers
appsettings.Template.json
```

本任务要做的是：

```text
1. 后端增加后台任务监控 API。
2. 后端增加周期任务运行历史、Worker 心跳、任务事件时间线、任务日志/操作日志查询能力。
3. BidOps 增加业务专项监控 API：采集健康、解析流水线、失败任务、恢复队列、配置检查。
4. 前端增加系统运维看板和 BidOps 后台任务看板。
5. 保留 Atlas 多租户边界、权限边界、敏感信息脱敏。
```

不要做：

```text
1. 不要绕过 Atlas Repository / 多租户边界。
2. 不要把任务 Payload、Token、Cookie、Authorization、手机号、邮箱等敏感信息明文展示。
3. 不要直接读取任意服务器文件路径来查日志，避免路径穿越和信息泄露。
4. 不要在 HTTP 请求线程里直接执行耗时后台任务。
5. 不要强杀 Running 任务。当前框架没有安全中断机制，Running 只能“请求取消”，由 Handler 后续支持。
6. 不要做登录态抓取、验证码绕过、自动投标、自动报价等 BidOps 禁止能力。
```

---

## 1. 当前仓库事实核对

### 1.1 Atlas 已有后台任务框架

当前 Atlas 已有 `src/Atlas.BackgroundTasks`：

```text
BackgroundJobClient.cs
BackgroundJobContracts.cs
BackgroundJobWorker.cs
BackgroundTaskServiceCollectionExtensions.cs
RecurringTasks.cs
```

当前一次性后台任务使用全局表 `BackgroundJob`，核心字段包括：

```text
Id
JobType
Queue
JobName
DeduplicationKey
TenantId
StoreId
Payload
Status
Priority
AvailableAtUtc
StartedAtUtc
LockedAtUtc
LockedBy
CompletedAtUtc
AttemptCount
MaxAttempts
NextAttemptAtUtc
LastError
Result
```

当前 `BackgroundJobStatus`：

```text
Pending
Running
Succeeded
Failed
Dead
Canceled
```

`BackgroundJobWorker` 当前行为：

```text
1. 按配置队列轮询 BackgroundJobs。
2. 使用数据库条件 UPDATE 领取任务，支持多实例并发。
3. 支持 Pending / Failed / 超时 Running 回收。
4. 支持 AttemptCount / MaxAttempts。
5. 失败后指数退避，达到最大次数进入 Dead。
6. 当前没有任务事件表、Worker 心跳表、周期任务运行历史表、日志查询 API。
```

`RecurringTaskRunner` 当前行为：

```text
1. 从 DI 获取 IRecurringTask。
2. 通过 IDistributedLockProvider 做分布式锁。
3. 用本地内存 _nextRuns 计算下次运行。
4. 只写 ILogger，不持久化每次运行结果。
```

### 1.2 BidOps 已有后台任务

当前 `src/Atlas.Modules.BidOps/BackgroundJobs` 下已有：

```text
AttachmentProcessJobHandler.cs
BidOpsBackgroundTenantConfiguration.cs
BidOpsJobIdentity.cs
BidOpsRecoveryTask.cs
BidOpsScheduledScanTask.cs
ManualUrlImportJobHandler.cs
MockAiParseJobHandler.cs
MockCrawlJobHandler.cs
StateGridEcpCrawlJobHandler.cs
StructuredParseJobHandler.cs
```

`BidOpsConstants.cs` 中已有：

```text
BidOpsBackgroundJobQueues.BidOps = "bidops"

BidOpsBackgroundJobTypes.ManualUrlImport = "bidops.raw.manual-url-import"
BidOpsBackgroundJobTypes.MockCrawl = "bidops.crawl.mock-scan"
BidOpsBackgroundJobTypes.StateGridEcpCrawl = "bidops.crawl.state-grid-ecp-scan"
BidOpsBackgroundJobTypes.AttachmentProcess = "bidops.document.attachment-process"
BidOpsBackgroundJobTypes.StructuredParse = "bidops.ai.structured-parse"
BidOpsBackgroundJobTypes.MockAiParse = "bidops.ai.mock-parse"
```

BidOps 当前后台链路：

```text
BidOpsScheduledScanTask
  -> enqueue StateGridEcpCrawl 或 MockCrawl
    -> 创建 RawNotice
    -> enqueue AttachmentProcess
      -> 附件下载/文本抽取
      -> enqueue StructuredParse
        -> 生成 NoticeStaging / PackageStaging / RequirementStaging
        -> 生成 ReviewTask

ManualUrlImport
  -> 创建 RawNotice
  -> enqueue AttachmentProcess
    -> enqueue StructuredParse
      -> 生成 ReviewTask

BidOpsRecoveryTask
  -> 扫描 ParseQueued / Failed RawNotice
  -> 扫描 Pending / Failed 附件下载或文本抽取
  -> enqueue AttachmentProcess
```

### 1.3 当前关键缺口

当前缺口：

```text
1. 没有后台任务总览 API。
2. 没有 BidOps 队列看板。
3. 没有 Worker 是否在线的判断。
4. 没有周期任务运行历史，无法知道 ScheduledScan / Recovery 最近一次是否成功。
5. 没有任务事件时间线，无法解释“为什么 RawNotice 没进审核池”。
6. 没有按 JobId / RawNoticeId / TenantId 查询日志的能力。
7. 没有任务重试、取消、重新解析等安全运维动作。
8. 没有配置检查：BackgroundJobWorker / RecurringTaskRunner 是否启用，OneTimeJobs.Queues 是否包含 bidops。
```

特别注意：

```text
appsettings.Template.json 里默认：
Atlas:Runtime:EnableBackgroundJobWorker = false
Atlas:Runtime:EnableRecurringTaskRunner = false
BackgroundTasks:Recurring:Enabled = false
BackgroundTasks:OneTimeJobs:Enabled = false
BackgroundTasks:OneTimeJobs:Queues = ["default", "tenant"]

但 BidOps 任务使用 queue = "bidops"。
因此即使任务被成功入队，如果运行环境没有把 "bidops" 加入 BackgroundTasks:OneTimeJobs:Queues，BidOps 后台任务不会被 Worker 消费。
```

这一点必须在看板中做红色配置告警。

---

## 2. 设计目标

最终用户在后台应能回答这些问题：

```text
1. 后台 Worker 有没有启动？
2. 当前有哪些后台任务在跑？
3. BidOps 采集任务有没有正常跑？
4. 哪些任务失败了？失败原因是什么？
5. 哪些任务卡住了？卡在哪一步？
6. 一个 RawNotice 为什么没有进入审核池？
7. 一个 Channel 最近有没有扫描成功？
8. StateGridEcp / Mock / ManualUrlImport / AttachmentProcess / StructuredParse 各自成功率如何？
9. Recovery 是否在不断重试同一批失败数据？
10. 系统日志能否按 JobId、RawNoticeId、TenantId、SourceContext、Level、时间范围查询？
```

---

## 3. 产品信息架构

新增两个一级能力：

```text
系统运维中心
  ├── 运行概览
  ├── 后台任务
  ├── 周期任务
  ├── Worker 节点
  ├── 日志查询
  └── 告警中心

BidOps
  ├── 运营看板
  ├── 标讯采集
  ├── 待审核池
  ├── 商机包件
  └── 后台任务监控
      ├── BidOps 任务总览
      ├── 采集健康
      ├── 解析流水线
      ├── 失败与恢复
      └── 配置检查
```

前端路由建议：

```text
/ops
/ops/jobs
/ops/jobs/:id
/ops/recurring-tasks
/ops/workers
/ops/logs
/ops/alerts

/bidops/operations
/bidops/operations/jobs
/bidops/operations/channels
/bidops/operations/raw-notices/:id/pipeline
/bidops/operations/config
```

---

## 4. 权限设计

新增系统级权限：

```csharp
public static class OperationsPermissionCodes
{
    public const string RuntimeRead = "ops.runtime.read";
    public const string JobsRead = "ops.jobs.read";
    public const string JobsManage = "ops.jobs.manage";
    public const string LogsRead = "ops.logs.read";
    public const string LogsExport = "ops.logs.export";
    public const string AlertsRead = "ops.alerts.read";
    public const string AlertsManage = "ops.alerts.manage";
}
```

新增 BidOps 运维权限：

```csharp
public static class BidOpsPermissionCodes
{
    // 已有：
    // CrawlRead, CrawlManage, CrawlImport, ReviewRead, ReviewApprove, BusinessRead

    public const string OpsRead = "bidops.ops.read";
    public const string OpsManage = "bidops.ops.manage";
}
```

权限规则：

```text
1. 系统运维中心默认只给系统管理员。
2. 租户管理员可看当前 TenantId 的后台任务和日志。
3. BidOps 运维页默认需要 bidops.ops.read。
4. 任务重试、取消、重新解析需要 bidops.ops.manage 或 ops.jobs.manage。
5. 查看完整 Payload 需要 ops.jobs.manage；普通读权限只能看脱敏 Payload。
6. 日志导出需要 ops.logs.export。
```

第一阶段如果不想改授权目录太多：

```text
1. /bidops/operations 读取可先复用 bidops.crawl.read。
2. BidOps 任务重试可先复用 bidops.crawl.manage。
3. 系统 /ops/* 必须新增独立权限，不要复用 BidOps 权限。
```

---

## 5. 数据模型设计

### 5.1 扩展 BackgroundJob

在 `BackgroundJob` 上增加可空字段，保持兼容：

```csharp
public long? ParentJobId { get; set; }
public long? RootJobId { get; set; }
public string? CorrelationId { get; set; }

public string? SourceModule { get; set; }      // BidOps, Export, TenantMigration 等
public string? BusinessType { get; set; }      // RawNotice, CrawlChannel, ReviewTask
public long? BusinessId { get; set; }

public int? Progress { get; set; }             // 0-100
public string? ProgressMessage { get; set; }

public DateTime? LastHeartbeatAtUtc { get; set; }

public DateTime? CancelRequestedAtUtc { get; set; }
public long? CancelRequestedByUserId { get; set; }
public string? CancelReason { get; set; }
```

索引建议：

```text
IX_BackgroundJobs_Status_Queue_AvailableAtUtc
IX_BackgroundJobs_TenantId_Status_CreatedAt
IX_BackgroundJobs_JobType_Status_CreatedAt
IX_BackgroundJobs_CorrelationId
IX_BackgroundJobs_RootJobId
IX_BackgroundJobs_ParentJobId
IX_BackgroundJobs_SourceModule_BusinessType_BusinessId
IX_BackgroundJobs_DeduplicationKey_TenantId  // 保留或确认已有
```

### 5.2 新增 BackgroundJobEvent

用于任务详情时间线，解决“只有 LastError，无法知道过程”的问题。

```csharp
public sealed class BackgroundJobEvent : BaseEntity, ISnowflakeId
{
    public long JobId { get; set; }
    public long? ParentJobId { get; set; }
    public long? RootJobId { get; set; }

    public long? TenantId { get; set; }
    public long? StoreId { get; set; }

    public string Queue { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    public string? SourceModule { get; set; }
    public string? BusinessType { get; set; }
    public long? BusinessId { get; set; }

    public string Message { get; set; } = string.Empty;
    public string? DataJson { get; set; }

    public int? AttemptCount { get; set; }
    public string? WorkerId { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
```

`EventType` 建议：

```text
Enqueued
Claimed
Started
Progress
ChildEnqueued
Succeeded
Failed
RetryScheduled
Dead
CancelRequested
Canceled
Skipped
Recovered
```

索引：

```text
IX_BackgroundJobEvents_JobId_OccurredAtUtc
IX_BackgroundJobEvents_RootJobId_OccurredAtUtc
IX_BackgroundJobEvents_TenantId_OccurredAtUtc
IX_BackgroundJobEvents_SourceModule_BusinessType_BusinessId
```

### 5.3 新增 BackgroundWorkerHeartbeat

用于判断 Worker 是否在线、是否消费 bidops 队列。

```csharp
public sealed class BackgroundWorkerHeartbeat : BaseEntity, ISnowflakeId
{
    public string WorkerId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int ProcessId { get; set; }

    public string RuntimeMode { get; set; } = string.Empty; // WebApi / Worker / MigrationJob
    public string QueuesJson { get; set; } = "[]";

    public bool OneTimeJobWorkerEnabled { get; set; }
    public bool RecurringTaskRunnerEnabled { get; set; }

    public long? CurrentJobId { get; set; }
    public string? CurrentJobType { get; set; }
    public string? CurrentQueue { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}
```

在线判断：

```text
LastSeenAtUtc >= now - max(60s, PollIntervalSeconds * 3)
```

### 5.4 新增 RecurringTaskRun

用于记录 `BidOpsScheduledScanTask`、`BidOpsRecoveryTask` 每次运行结果。

```csharp
public sealed class RecurringTaskRun : BaseEntity, ISnowflakeId
{
    public string TaskName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Running/Succeeded/Failed/SkippedLocked

    public string? SourceModule { get; set; }
    public string? TriggerType { get; set; } // Schedule/Manual/Startup

    public string? WorkerId { get; set; }
    public string? LockResource { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? DurationMs { get; set; }

    public int? ProducedJobs { get; set; }
    public string? ResultSummary { get; set; }
    public string? ErrorMessage { get; set; }

    public string? DataJson { get; set; }
}
```

`TaskName` 当前 BidOps 已有：

```text
bidops.scheduled-scan
bidops.recovery
```

索引：

```text
IX_RecurringTaskRuns_TaskName_StartedAtUtc
IX_RecurringTaskRuns_Status_StartedAtUtc
```

### 5.5 新增 OperationLogEntry

用于可查询日志。不要把所有日志无脑落库，优先收集：

```text
1. Error / Warning。
2. BackgroundJobWorker / RecurringTaskRunner 日志。
3. BidOps 模块关键业务日志。
4. 带 JobId / CorrelationId 的结构化日志。
```

实体：

```csharp
public sealed class OperationLogEntry : BaseEntity, ISnowflakeId
{
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = string.Empty;       // Debug/Information/Warning/Error/Fatal
    public string Message { get; set; } = string.Empty;
    public string? MessageTemplate { get; set; }
    public string? Exception { get; set; }

    public string? SourceContext { get; set; }
    public string? Module { get; set; }

    public long? TenantId { get; set; }
    public long? StoreId { get; set; }
    public long? UserId { get; set; }

    public long? JobId { get; set; }
    public string? JobType { get; set; }
    public string? Queue { get; set; }

    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }

    public string? PropertiesJson { get; set; }
}
```

索引：

```text
IX_OperationLogs_TimestampUtc
IX_OperationLogs_Level_TimestampUtc
IX_OperationLogs_TenantId_TimestampUtc
IX_OperationLogs_JobId_TimestampUtc
IX_OperationLogs_CorrelationId_TimestampUtc
IX_OperationLogs_Module_TimestampUtc
IX_OperationLogs_SourceContext_TimestampUtc
```

保留策略：

```text
OperationLogEntry 默认保留 30 天。
BackgroundJobEvent 默认保留 90 天。
BackgroundJob 默认保留 180 天。
Dead / Failed / 审计相关任务不要自动删除，至少保留 180 天。
```

---

## 6. 后台任务框架改造

### 6.1 BackgroundJobClient 改造

`EnqueueBackgroundJobRequest` 增加：

```csharp
public long? ParentJobId { get; init; }
public long? RootJobId { get; init; }
public string? CorrelationId { get; init; }

public string? SourceModule { get; init; }
public string? BusinessType { get; init; }
public long? BusinessId { get; init; }
```

如果当前正在执行一个 Job，且请求没有显式传入 Parent/Root/Correlation，则自动继承当前上下文：

```text
ParentJobId = CurrentJob.Id
RootJobId = CurrentJob.RootJobId ?? CurrentJob.Id
CorrelationId = CurrentJob.CorrelationId ?? new Guid/string
SourceModule = CurrentJob.SourceModule
```

新增：

```csharp
public interface IBackgroundJobExecutionContextAccessor
{
    BackgroundJobExecutionSnapshot? Current { get; }
}
```

`BackgroundJobClient.EnqueueAsync` 成功后写 `BackgroundJobEvent.Enqueued`。如果由一个父任务入队子任务，再额外写父任务的 `ChildEnqueued` 事件。

### 6.2 BackgroundJobWorker 改造

执行前：

```text
1. 更新 WorkerHeartbeat。
2. 写 BackgroundJobEvent.Claimed。
3. 进入 ILogger Scope：
   JobId, JobType, Queue, TenantId, StoreId, CorrelationId, SourceModule, BusinessType, BusinessId。
4. 设置 IBackgroundJobExecutionContextAccessor.Current。
5. 写 BackgroundJobEvent.Started。
```

执行成功：

```text
1. 更新 BackgroundJob.Status = Succeeded。
2. 写 CompletedAtUtc / Result。
3. 写 BackgroundJobEvent.Succeeded。
4. 清理 CurrentJobId 心跳。
```

执行失败：

```text
1. 写 LastError。
2. 如果还能重试，Status = Failed，NextAttemptAtUtc = 指数退避。
3. 写 BackgroundJobEvent.Failed。
4. 写 BackgroundJobEvent.RetryScheduled。
5. 达到 MaxAttempts 时 Status = Dead，写 BackgroundJobEvent.Dead。
```

处理中：

```text
1. 每个 Job 开始和结束都更新 BackgroundWorkerHeartbeat。
2. 长任务支持 IBackgroundJobProgressReporter 更新 Progress / ProgressMessage。
3. Handler 暂时没有进度也不影响。
```

### 6.3 Cancel 设计

当前框架不能安全强杀正在执行的 .NET 任务。实现规则：

```text
Pending / Failed：
  可以直接置为 Canceled，并写 CompletedAtUtc。

Dead：
  可以置 Canceled 作为人工关闭，不删除原记录。

Running：
  不直接强制停止。
  只能设置 CancelRequestedAtUtc / CancelReason。
  只有 Handler 显式检查取消标记时才会中断。
```

第一阶段 UI 对 Running 显示：

```text
“已请求取消，等待任务处理器响应；当前框架不会强杀正在运行的任务。”
```

### 6.4 Retry 设计

不要原地把 Dead 改回 Pending，避免破坏历史。建议：

```text
POST /api/ops/background-jobs/{id}/retry
```

行为：

```text
1. 读取原任务。
2. 创建一个新的 BackgroundJob。
3. Payload、JobType、Queue、TenantId、StoreId 继承原任务。
4. ParentJobId = 原任务 Id。
5. RootJobId = 原任务 RootJobId ?? 原任务 Id。
6. CorrelationId 继承原任务。
7. DeduplicationKey 重新生成：原 DeduplicationKey + ":manual-retry:" + timestamp/userId。
8. 写原任务事件 ManualRetry。
9. 写新任务事件 Enqueued。
```

### 6.5 RecurringTaskRunner 改造

每次真正拿到分布式锁并开始运行时：

```text
1. 插入 RecurringTaskRun(Status=Running)。
2. 写 WorkerHeartbeat。
3. 执行 task.ExecuteAsync。
4. 成功后 Status=Succeeded，记录 DurationMs、ResultSummary。
5. 失败后 Status=Failed，记录 ErrorMessage。
```

如果没有拿到锁，默认不写入历史，避免多实例刷屏。需要诊断时可以在 Debug 日志里记录。

新增服务：

```csharp
public interface IRecurringTaskRunRecorder
{
    Task<RecurringTaskRun> StartedAsync(...);
    Task SucceededAsync(...);
    Task FailedAsync(...);
}
```

### 6.6 OperationLogEntry 写入策略

第一阶段不要实现完整 Serilog Sink 也可以，但必须实现可查询的操作日志来源：

```text
1. BackgroundJobWorker 在失败、死亡、重试时写 OperationLogEntry。
2. RecurringTaskRunner 在失败时写 OperationLogEntry。
3. BidOps Job Handler 在关键业务节点写 OperationLogEntry：
   - 创建 RawNotice
   - 附件处理结果
   - 结构化解析完成
   - 生成 ReviewTask
   - Recovery 重新入队
```

第二阶段可以补：

```text
1. Serilog 自定义 Sink，将 Warning+ 日志写入 OperationLogEntry。
2. Seq 查询 Provider：如果 Logging:Atlas:EnableSeq=true，则日志页可跳转或代理查询 Seq。
```

---

## 7. BidOps 后台任务专项设计

### 7.1 BidOps Job 业务标识

所有 BidOps Job 入队时必须带：

```text
SourceModule = "BidOps"
```

不同任务的业务标识：

| JobType | BusinessType | BusinessId | 说明 |
|---|---|---:|---|
| `bidops.raw.manual-url-import` | `RawNotice` 或 `ManualUrl` | 还未有 RawNotice 时为空 | 手动 URL 导入 |
| `bidops.crawl.mock-scan` | `CrawlChannel` | `ChannelId` | Mock 采集 |
| `bidops.crawl.state-grid-ecp-scan` | `CrawlChannel` | `ChannelId` | 国网公开页面采集 |
| `bidops.document.attachment-process` | `RawNotice` | `RawNoticeId` | 附件下载与文本抽取 |
| `bidops.ai.structured-parse` | `RawNotice` | `RawNoticeId` | 结构化解析 |
| `bidops.ai.mock-parse` | `RawNotice` | `RawNoticeId` | Mock AI 解析 |

注意：

```text
ManualUrlImport 一开始没有 RawNoticeId。
Handler 创建 RawNotice 后，应更新当前 BackgroundJob.BusinessType = "RawNotice"，BusinessId = rawNoticeId。
```

### 7.2 BidOps 采集链路

页面应能展示：

```text
ScheduledScanTask run
  -> StateGridEcpCrawl / MockCrawl job
    -> RawNotice
      -> AttachmentProcess job
        -> StructuredParse job
          -> ReviewTask
            -> 审核通过
              -> Notice / TenderPackage / RequirementItem
```

每个 RawNotice 详情页应有“后台流水线”：

```text
发现/导入
  时间、来源、Channel、JobId、结果

原始公告入库
  RawNoticeId、ContentHash、Status、LastError

附件处理
  附件总数、下载成功、抽取成功、失败数、JobId、错误

结构化解析
  ParserVersion、AI Provider、ReviewTaskId、JobId、错误

审核
  ReviewTask 状态、审核人、审核时间

正式业务
  NoticeId、Package 数、Requirement 数
```

### 7.3 BidOps 看板指标

`/bidops/operations` 页面卡片：

```text
1. BidOps Worker 在线状态
2. bidops 队列 Pending 数
3. bidops 队列 Running 数
4. bidops 队列 Failed 数
5. bidops 队列 Dead 数
6. 今日采集 RawNotice 数
7. 今日结构化解析成功数
8. 今日生成 ReviewTask 数
9. 待恢复 RawNotice 数
10. 附件失败数
11. ScheduledScan 最近运行时间/结果
12. Recovery 最近运行时间/结果
```

趋势图：

```text
1. 最近 24 小时 BidOps Job 成功/失败趋势。
2. 最近 7 天 RawNotice 入库趋势。
3. 最近 7 天 StructuredParse 成功率。
4. 失败任务按 JobType 分布。
5. 失败任务按 CrawlSource / CrawlChannel 分布。
```

重点表格：

```text
1. 最近失败任务。
2. Dead 任务。
3. 运行超过阈值任务。
4. 长时间未成功扫描的 Channel。
5. 待恢复 RawNotice。
6. 附件下载/抽取失败。
7. 解析失败 RawNotice。
```

### 7.4 BidOps 配置检查

新增 `/api/bidops/operations/config-check`。

检查项：

```text
BackgroundJobWorkerEnabled
RecurringTaskRunnerEnabled
OneTimeJobQueuesContainsBidOps
BidOpsScheduledScanEnabled
BidOpsScheduledScanTenantConfigured
BidOpsRecoveryEnabled
BidOpsRecoveryTenantConfigured
BidOpsSourcesEnabled
BidOpsChannelsEnabled
StateGridEcpSourceConfigured
MockSourceConfigured
```

返回示例：

```json
{
  "overallStatus": "Warning",
  "items": [
    {
      "code": "bidops.queue.missing",
      "status": "Error",
      "message": "BackgroundTasks:OneTimeJobs:Queues 未包含 bidops，BidOps 入队任务不会被消费。",
      "suggestion": "将 bidops 加入 BackgroundTasks:OneTimeJobs:Queues，并启用 BackgroundTasks:OneTimeJobs:Enabled。"
    },
    {
      "code": "bidops.recurring.disabled",
      "status": "Warning",
      "message": "RecurringTaskRunner 未启用，ScheduledScan/Recovery 不会自动运行。",
      "suggestion": "启用 Atlas:Runtime:EnableRecurringTaskRunner 或 BackgroundTasks:Recurring:Enabled。"
    }
  ]
}
```

### 7.5 BidOps 运维动作

允许的动作：

```text
1. 重试 Dead / Failed 的 BidOps Job。
2. 取消 Pending 的 BidOps Job。
3. 对 RawNotice 重新入队 AttachmentProcess。
4. 对 RawNotice 重新入队 StructuredParse。
5. 对 Channel 立即扫描。
6. 对长期失败 Channel 暂停。
```

不允许：

```text
1. 不允许删除历史 Job。
2. 不允许直接修改 RawNotice 为成功。
3. 不允许绕过审核直接写正式 Notice/Package。
4. 不允许强杀 Running Job。
```

---

## 8. 后端 API 设计

### 8.1 系统运维 API

建议新增模块：

```text
src/Atlas.Modules.Operations
```

如果 Atlas 模块注册成本过高，也可以先放在：

```text
src/Atlas.WebApi/Controllers/Operations
```

但推荐模块化。

#### 运行概览

```http
GET /api/ops/runtime/summary
```

返回：

```ts
interface RuntimeSummaryDto {
  oneTimeJobWorkerEnabled: boolean
  recurringTaskRunnerEnabled: boolean
  activeWorkers: number
  offlineWorkers: number
  queues: QueueSummaryDto[]
  totalPending: number
  totalRunning: number
  totalFailed: number
  totalDead: number
  oldestPendingAgeSeconds?: number
  lastErrorAtUtc?: string
}
```

#### 后台任务汇总

```http
GET /api/ops/background-jobs/summary?tenantId=&queue=&module=&from=&to=
```

返回：

```ts
interface BackgroundJobSummaryDto {
  pending: number
  running: number
  succeeded: number
  failed: number
  dead: number
  canceled: number
  delayed: number
  retryWaiting: number
  staleRunning: number
  avgDurationMs?: number
  p95DurationMs?: number
  oldestPendingAtUtc?: string
  byQueue: QueueSummaryDto[]
  byJobType: JobTypeSummaryDto[]
}
```

#### 后台任务列表

```http
GET /api/ops/background-jobs
```

查询参数：

```ts
interface BackgroundJobSearchQuery {
  keyword?: string
  tenantId?: number
  queue?: string
  jobType?: string
  status?: string
  sourceModule?: string
  businessType?: string
  businessId?: number
  correlationId?: string
  rootJobId?: number
  parentJobId?: number
  createdFromUtc?: string
  createdToUtc?: string
  onlyStaleRunning?: boolean
  onlyRetryWaiting?: boolean
  pageIndex?: number
  pageSize?: number
}
```

列表 DTO：

```ts
interface BackgroundJobListItemDto {
  id: string
  jobType: string
  queue: string
  jobName: string
  tenantId?: string
  storeId?: string

  status: string
  priority: number
  attemptCount: number
  maxAttempts: number

  sourceModule?: string
  businessType?: string
  businessId?: string

  correlationId?: string
  parentJobId?: string
  rootJobId?: string

  availableAtUtc: string
  startedAtUtc?: string
  completedAtUtc?: string
  nextAttemptAtUtc?: string

  lockedBy?: string
  lockedAtUtc?: string
  durationMs?: number
  waitingMs?: number

  progress?: number
  progressMessage?: string

  lastErrorPreview?: string
}
```

#### 后台任务详情

```http
GET /api/ops/background-jobs/{id}
```

返回：

```ts
interface BackgroundJobDetailDto {
  job: BackgroundJobListItemDto
  payload: unknown              // 默认脱敏
  result?: string
  lastError?: string
  events: BackgroundJobEventDto[]
  children: BackgroundJobListItemDto[]
  relatedLogs: OperationLogEntryDto[]
}
```

#### 后台任务事件

```http
GET /api/ops/background-jobs/{id}/events
GET /api/ops/background-jobs/{id}/tree
GET /api/ops/background-jobs/{id}/logs
```

#### 重试 / 取消

```http
POST /api/ops/background-jobs/{id}/retry
POST /api/ops/background-jobs/{id}/cancel
```

请求：

```ts
interface RetryBackgroundJobRequest {
  reason?: string
}

interface CancelBackgroundJobRequest {
  reason?: string
}
```

### 8.2 周期任务 API

#### 注册任务与最近运行

```http
GET /api/ops/recurring-tasks
```

返回：

```ts
interface RecurringTaskDto {
  name: string
  intervalSeconds: number
  runOnStartup: boolean

  enabled: boolean
  sourceModule?: string

  lastRunId?: string
  lastStatus?: string
  lastStartedAtUtc?: string
  lastCompletedAtUtc?: string
  lastDurationMs?: number
  lastErrorPreview?: string

  estimatedNextRunAtUtc?: string
}
```

#### 周期任务运行历史

```http
GET /api/ops/recurring-task-runs?taskName=&status=&from=&to=&pageIndex=&pageSize=
```

#### 手动触发周期任务

```http
POST /api/ops/recurring-tasks/{taskName}/run-now
```

实现建议：

```text
第一阶段不要直接在 HTTP 请求中执行 task.ExecuteAsync。
创建一个一次性后台任务：
JobType = "ops.recurring-task.run-now"
Payload = { taskName, requestedByUserId }
Queue = "default" 或配置队列
```

然后由 Handler 在后台拿分布式锁执行目标周期任务。

### 8.3 Worker API

```http
GET /api/ops/workers
GET /api/ops/workers/summary
```

返回：

```ts
interface BackgroundWorkerDto {
  workerId: string
  hostName: string
  processId: number
  runtimeMode: string
  queues: string[]
  oneTimeJobWorkerEnabled: boolean
  recurringTaskRunnerEnabled: boolean
  currentJobId?: string
  currentJobType?: string
  currentQueue?: string
  startedAtUtc: string
  lastSeenAtUtc: string
  online: boolean
}
```

### 8.4 日志查询 API

```http
GET /api/ops/logs
GET /api/ops/logs/facets
GET /api/ops/logs/export
```

查询参数：

```ts
interface OperationLogSearchQuery {
  keyword?: string
  level?: string
  sourceContext?: string
  module?: string
  tenantId?: number
  storeId?: number
  userId?: number
  jobId?: number
  jobType?: string
  queue?: string
  correlationId?: string
  traceId?: string
  fromUtc?: string
  toUtc?: string
  pageIndex?: number
  pageSize?: number
}
```

列表 DTO：

```ts
interface OperationLogEntryDto {
  id: string
  timestampUtc: string
  level: string
  message: string
  sourceContext?: string
  module?: string
  tenantId?: string
  storeId?: string
  userId?: string
  jobId?: string
  jobType?: string
  queue?: string
  correlationId?: string
  traceId?: string
  exceptionPreview?: string
}
```

日志详情：

```http
GET /api/ops/logs/{id}
```

返回完整脱敏后的：

```text
Message
MessageTemplate
Exception
PropertiesJson
TraceId
SpanId
JobId
TenantId
```

### 8.5 BidOps 运维 API

放在 BidOps 模块：

```text
src/Atlas.Modules.BidOps/Controllers/BidOpsOperationsController.cs
src/Atlas.Modules.BidOps/Queries/BidOpsOperationsQueryService.cs
```

#### BidOps 看板

```http
GET /api/bidops/operations/dashboard?from=&to=
```

返回：

```ts
interface BidOpsOperationsDashboardDto {
  configStatus: string

  worker: {
    bidOpsQueueConfigured: boolean
    activeBidOpsWorkers: number
    recurringRunnerEnabled: boolean
    oneTimeWorkerEnabled: boolean
  }

  jobs: {
    pending: number
    running: number
    failed: number
    dead: number
    staleRunning: number
    oldestPendingAtUtc?: string
  }

  today: {
    rawNoticesCreated: number
    attachmentProcessed: number
    structuredParseSucceeded: number
    reviewTasksGenerated: number
    reviewTasksPending: number
  }

  recovery: {
    recoverableRawNotices: number
    failedRawNotices: number
    failedAttachments: number
    parseQueuedRawNotices: number
    lastRecoveryStatus?: string
    lastRecoveryAtUtc?: string
  }

  scheduledScan: {
    lastStatus?: string
    lastRunAtUtc?: string
    producedJobs?: number
    enabledChannels: number
    staleChannels: number
    failedChannels: number
  }

  failureBreakdown: JobTypeSummaryDto[]
  recentFailures: BackgroundJobListItemDto[]
}
```

#### BidOps 任务列表

```http
GET /api/bidops/operations/jobs
```

等价于：

```text
/api/ops/background-jobs?sourceModule=BidOps
或 queue=bidops + jobType startsWith bidops.
```

#### BidOps 采集健康

```http
GET /api/bidops/operations/channels/health
```

DTO：

```ts
interface BidOpsChannelHealthDto {
  channelId: string
  sourceId: string
  sourceName: string
  sourceType: string
  channelName: string
  noticeType: string
  enabled: boolean

  crawlIntervalMinutes: number
  lastScanTime?: string
  lastSuccessTime?: string
  lastError?: string

  healthStatus: 'Healthy' | 'Due' | 'Stale' | 'Failed' | 'Disabled' | 'SourceDisabled' | 'SkippedNeedLogin'
  nextDueAtUtc?: string
  minutesSinceLastSuccess?: number

  pendingJobs: number
  runningJobs: number
  failedJobs24h: number
  succeededJobs24h: number
}
```

健康规则：

```text
Disabled：channel.Enabled=false
SourceDisabled：source.Enabled=false
SkippedNeedLogin：source.NeedLogin=true，当前 ScheduledScan 会跳过
Healthy：lastSuccessTime 在 2 * CrawlIntervalMinutes 内
Due：已到扫描时间但未超 2 倍间隔
Stale：超过 2 * CrawlIntervalMinutes 未成功
Failed：LastError 不为空且最近一次 Job 失败
```

#### RawNotice 流水线

```http
GET /api/bidops/operations/raw-notices/{rawNoticeId}/pipeline
```

DTO：

```ts
interface BidOpsRawNoticePipelineDto {
  rawNotice: RawNoticeDto

  rootJobs: BackgroundJobListItemDto[]
  jobs: BackgroundJobListItemDto[]
  events: BackgroundJobEventDto[]
  logs: OperationLogEntryDto[]

  attachmentSummary: {
    total: number
    pending: number
    downloaded: number
    downloadFailed: number
    textExtracted: number
    textExtractFailed: number
  }

  parseSummary: {
    structuredParseJob?: BackgroundJobListItemDto
    reviewTaskId?: string
    reviewTaskStatus?: string
    stagingNoticeId?: string
    packageCount: number
    requirementCount: number
  }

  businessSummary: {
    noticeId?: string
    packageCount: number
    requirementCount: number
  }
}
```

#### BidOps 配置检查

```http
GET /api/bidops/operations/config-check
```

#### BidOps 操作

```http
POST /api/bidops/operations/jobs/{id}/retry
POST /api/bidops/operations/jobs/{id}/cancel

POST /api/bidops/operations/raw-notices/{rawNoticeId}/requeue-attachment-process
POST /api/bidops/operations/raw-notices/{rawNoticeId}/reparse

POST /api/bidops/operations/channels/{channelId}/scan-now
POST /api/bidops/operations/channels/{channelId}/pause
```

注意：

```text
scan-now 可以复用已有 CrawlChannelsController 的立即扫描逻辑。
reparse 只允许进入 StructuredParse 或 MockAiParse，不允许直接改正式业务表。
```

---

## 9. 前端页面设计

### 9.1 系统运行概览 `/ops`

卡片：

```text
1. OneTimeJobWorker：启用 / 未启用
2. RecurringTaskRunner：启用 / 未启用
3. 在线 Worker 数
4. 当前 Running 任务
5. Pending 积压
6. Dead 任务
7. 最老 Pending 等待时间
8. 最近错误时间
```

图表：

```text
1. 最近 24 小时任务成功/失败趋势。
2. 按 Queue 的任务分布。
3. 按 JobType 的失败 Top 10。
```

### 9.2 后台任务列表 `/ops/jobs`

搜索条件：

```text
关键词
TenantId
Queue
JobType
Status
SourceModule
BusinessType
BusinessId
CorrelationId
时间范围
只看 Dead
只看 Stale Running
只看等待重试
```

表格列：

```text
状态
JobType
Queue
JobName
TenantId
BusinessType/BusinessId
AttemptCount/MaxAttempts
等待时间
运行时长
NextAttemptAtUtc
LockedBy
LastErrorPreview
操作
```

操作：

```text
详情
重试
取消
复制 JobId
按 JobId 查日志
按 CorrelationId 查链路
```

### 9.3 后台任务详情 `/ops/jobs/:id`

页面结构：

```text
顶部摘要：
  JobId、状态、JobType、Queue、Tenant、耗时、重试次数、CorrelationId

Tab 1 基本信息：
  所有时间、锁信息、优先级、业务对象、进度、结果

Tab 2 Payload：
  JSON Viewer，默认脱敏

Tab 3 时间线：
  BackgroundJobEvent

Tab 4 子任务：
  Parent/Children/Root 链路图

Tab 5 错误：
  LastError、Exception、失败事件

Tab 6 日志：
  自动按 JobId 过滤 OperationLogEntry
```

### 9.4 周期任务 `/ops/recurring-tasks`

表格列：

```text
TaskName
Interval
RunOnStartup
Enabled
最近运行状态
最近开始时间
最近耗时
最近错误
预计下次运行
操作：运行历史 / 手动触发
```

重点显示：

```text
bidops.scheduled-scan
bidops.recovery
```

### 9.5 Worker 节点 `/ops/workers`

表格列：

```text
在线状态
WorkerId
HostName
ProcessId
RuntimeMode
Queues
当前任务
启动时间
最后心跳
```

如果没有任何 Worker 消费 `bidops` 队列，显示红色：

```text
当前没有在线 Worker 消费 bidops 队列，BidOps 后台任务会积压。
```

### 9.6 日志查询 `/ops/logs`

搜索条件：

```text
时间范围
Level
Module
SourceContext
TenantId
JobId
JobType
Queue
CorrelationId
TraceId
关键词
```

表格列：

```text
时间
Level
Message
SourceContext
Module
TenantId
JobId
CorrelationId
异常预览
```

点击详情：

```text
完整消息
异常堆栈
属性 JSON
相关任务
相关 Trace
```

### 9.7 BidOps 后台任务看板 `/bidops/operations`

顶部告警：

```text
1. BackgroundJobWorker 未启用
2. RecurringTaskRunner 未启用
3. Queues 未包含 bidops
4. ScheduledScan 未配置 TenantIds
5. Recovery 未配置 TenantIds
6. 没有启用的 CrawlSource/CrawlChannel
```

核心卡片：

```text
BidOps Worker 在线
Pending
Running
Failed
Dead
今日 RawNotice
今日解析成功
今日生成审核任务
Recovery 待处理
```

漏斗：

```text
采集入队
  -> RawNotice 创建
    -> 附件处理
      -> 结构化解析
        -> ReviewTask 生成
          -> 审核通过
```

重点表：

```text
Dead 任务
最近失败任务
超时 Running 任务
待恢复 RawNotice
失败附件
长时间未扫描栏目
```

### 9.8 采集健康 `/bidops/operations/channels`

表格列：

```text
健康状态
Source
Channel
NoticeType
Enabled
NeedLogin
CrawlIntervalMinutes
LastScanTime
LastSuccessTime
LastError
最近24小时成功/失败
Pending/Running Job
操作：立即扫描、查看任务、暂停
```

### 9.9 RawNotice 流水线 `/bidops/operations/raw-notices/:id/pipeline`

页面结构：

```text
左侧：
  RawNotice 摘要

中间：
  流水线 Timeline

右侧：
  当前阻塞点诊断
```

诊断示例：

```text
1. 未发现 AttachmentProcess Job：
   建议重新入队附件处理。

2. AttachmentProcess Dead：
   显示 LastError，提供重试。

3. StructuredParse 未生成：
   检查 AttachmentProcess 是否成功。

4. ReviewTask 已生成但未审核：
   跳转审核详情。

5. 审核通过但没有正式 Notice：
   检查审核服务日志。
```

---

## 10. 前端组件

新增通用组件：

```text
JobStatusTag.vue
QueueStatusTag.vue
WorkerOnlineBadge.vue
DurationText.vue
RelativeTime.vue
PayloadJsonViewer.vue
ErrorStackViewer.vue
JobTimeline.vue
JobTree.vue
LogSearchForm.vue
LogLevelTag.vue
ConfigCheckPanel.vue
MetricCard.vue
```

BidOps 专用组件：

```text
BidOpsJobFunnel.vue
BidOpsChannelHealthTag.vue
BidOpsPipelineTimeline.vue
BidOpsRecoveryBacklogTable.vue
BidOpsConfigWarningPanel.vue
```

---

## 11. 日志与敏感信息脱敏

必须脱敏的字段名：

```text
password
pwd
secret
token
accessToken
refreshToken
apiKey
apikey
authorization
cookie
set-cookie
phone
mobile
email
idCard
bankCard
```

Payload 展示策略：

```text
1. 默认折叠。
2. 默认脱敏。
3. 普通读权限不显示完整 Payload。
4. 高权限用户可点“显示完整脱敏 JSON”。
5. 不提供“显示未脱敏原文”功能。
```

异常堆栈：

```text
1. 可以展示。
2. 过长截断。
3. 文件路径可保留，但不要暴露环境变量。
```

日志查询：

```text
1. 默认时间范围最近 1 小时。
2. 最大查询范围默认 7 天。
3. 单页最大 200 条。
4. 导出需要 ops.logs.export。
```

---

## 12. 告警规则设计

第一阶段只在页面中显示告警，不需要通知系统。

系统级告警：

```text
1. BackgroundJobWorker 未启用。
2. RecurringTaskRunner 未启用。
3. 无在线 Worker。
4. 某队列 Pending 超过阈值。
5. 最老 Pending 等待超过阈值。
6. Running 超过 ProcessingTimeoutSeconds。
7. Dead 任务数量 > 0。
8. 失败率最近 1 小时 > 20%。
```

BidOps 级告警：

```text
1. 无 Worker 消费 bidops 队列。
2. ScheduledScan 最近 2 个周期未成功。
3. Recovery 最近运行失败。
4. Enabled Channel 超过 2 倍 CrawlIntervalMinutes 未成功扫描。
5. RawNotice ParseQueued 积压超过阈值。
6. AttachmentProcess Dead > 0。
7. StructuredParse Dead > 0。
8. 最近 24 小时没有任何 RawNotice 入库，但存在启用的 Channel。
9. Source.NeedLogin=true 导致 ScheduledScan 跳过。
10. StateGridEcp 失败率异常升高。
```

第二阶段可以新增：

```text
BackgroundTaskAlertRule
BackgroundTaskAlert
```

并支持站内消息、邮件、Webhook。

---

## 13. 配置建议

开发环境：

```json
{
  "Atlas": {
    "Runtime": {
      "EnableBackgroundJobWorker": true,
      "EnableRecurringTaskRunner": true
    }
  },
  "BackgroundTasks": {
    "Recurring": {
      "Enabled": true,
      "PollIntervalSeconds": 10,
      "LockSeconds": 300
    },
    "OneTimeJobs": {
      "Enabled": true,
      "Queues": [ "default", "tenant", "bidops" ],
      "PollIntervalSeconds": 5,
      "BatchSize": 20,
      "ProcessingTimeoutSeconds": 300,
      "InitialRetryDelaySeconds": 10,
      "MaxRetryDelaySeconds": 300,
      "DefaultMaxAttempts": 5
    }
  },
  "BidOps": {
    "ScheduledScan": {
      "Enabled": true,
      "RunOnStartup": false,
      "IntervalMinutes": 5,
      "TenantIds": [ 1 ],
      "UserId": 0,
      "UserName": "BidOps Scheduler",
      "MaxChannelsPerCycle": 20
    },
    "Recovery": {
      "Enabled": true,
      "RunOnStartup": true,
      "IntervalMinutes": 5,
      "TenantIds": [ 1 ],
      "UserId": 0,
      "UserName": "BidOps Recovery",
      "MaxRawPerCycle": 50
    }
  }
}
```

生产环境：

```text
1. WebApi 可以只入队不消费。
2. 独立 Atlas.Worker 节点启用 OneTimeJobWorker 和 RecurringTaskRunner。
3. Worker 的 Queues 必须包含 bidops。
4. 多实例 Worker 允许，但要确认分布式锁 Provider 正常。
5. OperationLogEntry、BackgroundJobEvent 设置保留清理任务。
```

---

## 14. 实施阶段

### P0：只读看板 + 基础运维动作

目标：快速解决“看不到任务、看不到失败、看不到 BidOps 是否在跑”。

后端：

```text
1. 新增 /api/ops/background-jobs 列表、详情、summary。
2. 新增 /api/ops/background-jobs/{id}/retry。
3. 新增 /api/ops/background-jobs/{id}/cancel，仅支持 Pending/Failed/Dead。
4. 新增 /api/bidops/operations/dashboard。
5. 新增 /api/bidops/operations/jobs。
6. 新增 /api/bidops/operations/config-check。
7. 新增 /api/bidops/operations/channels/health。
8. 所有 API 遵守 TenantId 过滤和权限。
```

前端：

```text
1. /ops/jobs
2. /ops/jobs/:id
3. /bidops/operations
4. /bidops/operations/jobs
5. /bidops/operations/channels
6. 配置告警面板
```

P0 可先不做新表，但要利用现有 BackgroundJob 字段：

```text
Status
JobType
Queue
TenantId
Payload
AttemptCount
MaxAttempts
NextAttemptAtUtc
StartedAtUtc
CompletedAtUtc
LockedAtUtc
LockedBy
LastError
Result
```

### P1：任务时间线、周期任务历史、Worker 心跳、日志查询

后端：

```text
1. 扩展 BackgroundJob 观测字段。
2. 新增 BackgroundJobEvent。
3. 新增 BackgroundWorkerHeartbeat。
4. 新增 RecurringTaskRun。
5. 新增 OperationLogEntry。
6. 改造 BackgroundJobClient 写 Enqueued/ChildEnqueued 事件。
7. 改造 BackgroundJobWorker 写 Claimed/Started/Succeeded/Failed/Dead 事件。
8. 改造 RecurringTaskRunner 写 RecurringTaskRun。
9. 新增 /api/ops/recurring-tasks。
10. 新增 /api/ops/recurring-task-runs。
11. 新增 /api/ops/workers。
12. 新增 /api/ops/logs。
13. 新增 /api/bidops/operations/raw-notices/{id}/pipeline。
```

前端：

```text
1. /ops
2. /ops/recurring-tasks
3. /ops/workers
4. /ops/logs
5. /bidops/operations/raw-notices/:id/pipeline
6. JobTimeline / JobTree / LogSearchPanel
```

### P2：告警、进度、实时刷新、Seq Provider

后端：

```text
1. 新增 BackgroundTaskAlertRule / BackgroundTaskAlert。
2. 新增 IBackgroundJobProgressReporter。
3. BidOps AttachmentProcess / StructuredParse 报告进度。
4. 支持 Seq 查询 Provider 或跳转链接。
5. 支持 SignalR 推送任务状态变化。
```

前端：

```text
1. /ops/alerts
2. 实时刷新或 WebSocket 推送。
3. Job 进度条。
4. 失败率趋势和 TopN 分析。
```

---

## 15. 验收标准

### 15.1 基础任务监控

```text
1. 入队一个 BidOps ManualUrlImport 后，可以在 /ops/jobs 和 /bidops/operations/jobs 看到。
2. JobType、Queue、TenantId、Status、AttemptCount、LastError、Result 显示正确。
3. Dead 任务可以点击重试，产生一个新的 Job，而不是覆盖原任务。
4. Pending 任务可以取消，状态变 Canceled。
5. Running 任务点击取消时只显示“请求取消”，不能假装已经强杀。
```

### 15.2 BidOps 看板

```text
1. BackgroundTasks:OneTimeJobs:Queues 不包含 bidops 时，/bidops/operations/config-check 显示 Error。
2. BackgroundJobWorker 未启用时，看板显示 Error。
3. RecurringTaskRunner 未启用时，看板显示 Warning。
4. ScheduledScan TenantIds 未配置时，看板显示 Warning。
5. 有 Failed/Dead 的 AttachmentProcess 或 StructuredParse 时，看板能按 JobType 统计出来。
6. Channel 长时间未成功扫描时，采集健康页显示 Stale。
```

### 15.3 周期任务历史

```text
1. bidops.scheduled-scan 每次运行后生成 RecurringTaskRun。
2. bidops.recovery 每次运行后生成 RecurringTaskRun。
3. 运行失败时 ErrorMessage 可查。
4. 周期任务页能显示最近一次运行状态和耗时。
```

### 15.4 日志查询

```text
1. BackgroundJob 失败时产生 OperationLogEntry。
2. 可以按 JobId 查询相关日志。
3. 可以按 TenantId、Level、Module、时间范围过滤。
4. Payload 和 PropertiesJson 中敏感字段已脱敏。
5. 普通用户不能跨租户看日志。
```

### 15.5 RawNotice 流水线

```text
1. RawNotice 详情能看到关联的 AttachmentProcess / StructuredParse 任务。
2. AttachmentProcess 失败时能定位失败 Job 和 LastError。
3. StructuredParse 成功时能看到 ReviewTaskId。
4. 审核通过后能看到正式 Notice / Package 摘要。
```

---

## 16. 代码落点建议

后端新增：

```text
src/Atlas.Modules.Operations/
  Atlas.Modules.Operations.csproj
  OperationsModule.cs
  Constants/OperationsConstants.cs
  Controllers/
    BackgroundJobsController.cs
    RecurringTasksController.cs
    WorkersController.cs
    OperationLogsController.cs
    RuntimeController.cs
  Models/
    BackgroundJobDtos.cs
    OperationLogDtos.cs
    RecurringTaskDtos.cs
  Queries/
    BackgroundJobQueryService.cs
    OperationLogQueryService.cs
    WorkerQueryService.cs
  Services/
    BackgroundJobManagementService.cs
    OperationLogWriter.cs
    BackgroundJobEventWriter.cs
```

BidOps 新增：

```text
src/Atlas.Modules.BidOps/
  Controllers/
    BidOpsOperationsController.cs
  Queries/
    BidOpsOperationsQueryService.cs
  Models/
    BidOpsOperationsDtos.cs
```

Core / BackgroundTasks 修改：

```text
src/Atlas.Core/Entities/Global/
  BackgroundJob.cs
  BackgroundJobEvent.cs
  BackgroundWorkerHeartbeat.cs
  RecurringTaskRun.cs
  OperationLogEntry.cs

src/Atlas.BackgroundTasks/
  BackgroundJobContracts.cs
  BackgroundJobClient.cs
  BackgroundJobWorker.cs
  RecurringTasks.cs
  BackgroundJobExecutionContextAccessor.cs
  BackgroundJobEventWriter.cs
  BackgroundWorkerHeartbeatService.cs
```

EF 配置与迁移：

```text
src/Atlas.Data.Global.Migrations/
  EntityConfigurations/
    BackgroundJobEventConfiguration.cs
    BackgroundWorkerHeartbeatConfiguration.cs
    RecurringTaskRunConfiguration.cs
    OperationLogEntryConfiguration.cs
  Migrations/
    AddBackgroundTaskObservability.cs
```

前端新增：

```text
frontend/atlas-admin/src/modules/operations/
  routes.ts
  api/
    backgroundJobs.api.ts
    recurringTasks.api.ts
    workers.api.ts
    operationLogs.api.ts
  pages/
    OperationsDashboardPage.vue
    BackgroundJobListPage.vue
    BackgroundJobDetailPage.vue
    RecurringTaskListPage.vue
    WorkerListPage.vue
    OperationLogListPage.vue
  components/
    JobStatusTag.vue
    JobTimeline.vue
    JobTree.vue
    WorkerOnlineBadge.vue
    OperationLogTable.vue

frontend/atlas-admin/src/modules/bidops/pages/operations/
  BidOpsOperationsDashboardPage.vue
  BidOpsOperationsJobsPage.vue
  BidOpsChannelHealthPage.vue
  BidOpsRawNoticePipelinePage.vue
  BidOpsConfigCheckPage.vue
```

---

## 17. 重要实现细节

### 17.1 查询 BackgroundJob 时的多租户边界

`BackgroundJob` 在全局库，有 `TenantId` 字段。查询规则：

```text
系统管理员：
  可按 TenantId 查询全部。

租户用户：
  只能看 TenantId = 当前租户 的任务。
  TenantId 为空的系统任务默认不可见，除非有 ops.runtime.read 的系统范围权限。

BidOps 用户：
  只能看 SourceModule=BidOps 或 queue=bidops 或 jobType startsWith bidops. 的任务。
  仍然必须限制 TenantId。
```

### 17.2 Payload 脱敏

实现一个公共服务：

```csharp
public interface ISensitiveJsonMasker
{
    string MaskJson(string json);
    object? MaskObject(object? value);
}
```

脱敏规则：

```text
1. key 名包含 token/password/secret/cookie/mobile/email 等，value 替换为 "***"。
2. 超长字符串截断到 2000 字符。
3. Payload 不是合法 JSON 时按字符串处理并截断。
```

### 17.3 Duration 计算

```text
Pending 等待：
  now - CreatedAt

Running 耗时：
  now - StartedAtUtc

Succeeded/Failed/Dead 耗时：
  CompletedAtUtc - StartedAtUtc

Retry 等待：
  NextAttemptAtUtc - now
```

### 17.4 Stale Running 判断

与 Worker 一致：

```text
LockedAtUtc < now - max(30s, BackgroundTasks:OneTimeJobs:ProcessingTimeoutSeconds)
```

UI 文案：

```text
该任务锁已超过处理超时时间，Worker 下一轮可能回收并重试。请确认 Handler 是否幂等。
```

### 17.5 BidOps Channel Stale 判断

```text
如果 Source 或 Channel 未启用：不告警。
如果 Source.NeedLogin=true：显示 SkippedNeedLogin。
如果 LastSuccessTime 为空：显示 NeverSucceeded。
如果 now - LastSuccessTime > 2 * CrawlIntervalMinutes：Stale。
如果 LastError 非空且最近 Job 失败：Failed。
```

---

## 18. README / 文档输出要求

执行完成后请新增或更新：

```text
docs/background_tasks_observability.md
docs/bidops_operations_dashboard.md
docs/log_query.md
```

文档必须说明：

```text
1. 如何启用 BackgroundJobWorker。
2. 如何启用 RecurringTaskRunner。
3. 为什么 BidOps 需要把 bidops 加入 OneTimeJobs.Queues。
4. 如何查看任务。
5. 如何重试任务。
6. 如何按 JobId 查询日志。
7. BidOps RawNotice 流水线如何排错。
8. 日志保留和脱敏策略。
```

---

## 19. 推荐第一轮 Codex 执行范围

为了降低风险，第一轮建议实现：

```text
P0 全部
P1 中的 BackgroundJobEvent、RecurringTaskRun、BackgroundWorkerHeartbeat
OperationLogEntry 可以先只写 BackgroundJobWorker / RecurringTaskRunner / BidOps Job 关键日志
前端实现 /ops/jobs、/ops/recurring-tasks、/ops/workers、/ops/logs、/bidops/operations、/bidops/operations/channels
```

第一轮暂缓：

```text
1. SignalR 实时推送。
2. 完整 Serilog DB Sink。
3. 告警规则持久化。
4. p95 耗时精确统计。
5. 强取消 Running Job。
```

---

## 20. Codex 完成后必须汇报

请按以下格式输出执行结果：

```text
## 已完成
- ...

## 数据库变更
- ...

## 新增 API
- ...

## 新增前端页面
- ...

## 配置项
- ...

## 如何验证
- ...

## 未完成 / 后续建议
- ...
```
