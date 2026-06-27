# Atlas 后台任务架构设计指南

Atlas 是多租户系统，后台任务不能只靠 `Task.Run`、临时线程或单个定时器解决。长期可维护的设计必须先区分任务类型，再决定可靠性、隔离方式、扩容方式和运维边界。

当前实现把后台能力拆成三层：

| 层 | 项目 | 职责 |
| --- | --- | --- |
| Runtime | `src/Atlas.BackgroundTasks` | 通用任务契约、持久化 job 入队、worker claim、重试、周期 runner。 |
| Tenant Jobs | `src/Atlas.Services.Tenant/Runtime/BackgroundJobs` | 租户运行时任务 handler，例如缓存预热、outbox 清理。 |
| Consumers | `src/Atlas.Consumers` | RabbitMQ consumers，按业务域组织，不能放在 WebApi 层。 |
| Host | `src/Atlas.Worker` / `samples/Atlas.Sample.WebApi` | WebApi 负责入队和查询；Worker 负责消费者、outbox dispatcher、后台任务执行和水平扩容。 |

## 任务类型选择

| 类型 | 适合场景 | Demo | 不适合 |
| --- | --- | --- | --- |
| RabbitMQ + outbox/inbox | 业务事件、跨服务通知、订阅消费，例如订单创建后通知库存/积分 | `src/Atlas.Consumers/Orders/OrderPlacedEventConsumer.cs` | 需要查询进度的长任务 |
| `BackgroundJobs` 持久化一次性任务 | 用户触发的异步任务、可查询状态、可重试、可 dead-letter | `TenantCacheWarmupJobHandler` | 高频事件广播 |
| `IRecurringTask` 周期任务 | 运维维护、扫描、清理、补偿调度 | `TenantOutboxMaintenanceTask` | 复杂 cron、强 misfire 策略、大量动态计划 |

长期原则：

- 业务事件优先走 RabbitMQ + outbox/inbox。
- 有业务状态和用户可见进度的耗时任务走 `BackgroundJobs`。
- 周期任务只做控制面和维护工作；重活可以由周期任务扫描后再入队 `BackgroundJobs`。
- WebApi 不承载 consumers、outbox dispatcher 或重任务执行，生产环境用 `Atlas.Worker` 独立部署。

## Consumer 部署边界

生产环境不把 RabbitMQ consumers 放在 WebApi 项目中。WebApi 的职责是处理 HTTP 请求、执行业务事务、写 tenant outbox 或创建 `BackgroundJobs`；消费者属于后台执行平面。

推荐结构：

```text
Atlas.Sample.WebApi
  Controllers
  Authentication
  Request/response models
  Writes TenantOutboxMessages
  Enqueues BackgroundJobs

Atlas.Consumers
  Orders/OrderPlacedEventConsumer
  Inventory/...
  Members/...

Atlas.Worker
  Registers Atlas.Consumers assembly
  Runs RabbitMQ consumers
  Runs TenantOutboxDispatcher
  Runs BackgroundJobWorker
  Runs RecurringTaskRunner
```

`Atlas.Worker` 注册 consumers：

```csharp
builder.Services.AddAtlasCore(
    builder.Configuration,
    typeof(OrderPlacedEventConsumer).Assembly);
```

WebApi 只注册核心服务，不传 consumer assembly：

```csharp
builder.Services.AddAtlasCore(builder.Configuration);
```

这样 API 扩容、消费者扩容、后台任务扩容互不绑定。某个 consumer 出现积压或失败时，不会拖垮 HTTP 服务。

## 持久化任务架构

一次性任务通过全局库 `BackgroundJobs` 表持久化。核心字段：

| 字段 | 说明 |
| --- | --- |
| `Id` | 雪花 ID，job 主键。 |
| `Queue` | 队列名，用于隔离不同业务线和 worker 扩容。 |
| `JobType` | 任务类型，用于路由到 handler。 |
| `DeduplicationKey` | 幂等键，唯一索引；相同 key 不重复入队。 |
| `TenantId` / `StoreId` | 显式租户上下文，worker 不依赖 HTTP 上下文。 |
| `Payload` | JSON 参数。 |
| `Status` | `Pending` / `Running` / `Succeeded` / `Failed` / `Dead` / `Canceled`。 |
| `AttemptCount` / `MaxAttempts` | 已领取执行次数和最大执行次数。 |
| `LockedAtUtc` / `LockedBy` | worker claim 信息。 |
| `NextAttemptAtUtc` | 失败后的下次重试时间。 |
| `CancellationRequestedAt` / `CancellationRequestedBy` / `CancellationReason` | 运营侧终止请求信息。 |
| `LastError` / `Result` | 运维排障信息。 |

`BackgroundJobs` 历史列名中仍保留 `Utc` 后缀，避免破坏既有迁移和索引；当前任务生命周期写入使用服务器本地时间（`DateTime.Now`）。运维 API 同时返回无 `Utc` 后缀的本地时间字段，例如 `availableAt`、`startedAt`、`completedAt`、`nextAttemptAt`，前端应优先展示这些字段。`xxxAtUtc` 字段仅作为兼容旧调用的别名保留。后台任务列表支持通过 `SortBy=CompletedAt` 和 `SortDescending=true/false` 按完成时间排序，未完成任务会排在已完成任务之后。

运营侧取消采用协作终止语义。`Pending` / `Failed` / `Dead` 任务会立即落成 `Canceled`；`Running` 任务会先写入 `CancellationRequestedAt`、请求人和原因，并在运维 API 中返回 `isCancellationRequested=true`。Worker 会轮询当前任务的终止请求并取消传给 handler 的 `CancellationToken`；handler 因该 token 退出后，任务才最终标记为 `Canceled` 并清空锁定信息。后台任务处理器必须把 `CancellationToken` 继续传给 HTTP、文件、数据库、AI、延迟等待等耗时调用，不能依赖强杀线程来停止工作。

Worker 默认把 handler 的 `Result` 截到 4000 字符，避免普通任务把全局任务表变成大文本存储。确实需要排障明细的 handler 可以在 `BackgroundJobExecutionResult.Success` 中显式传入更大的 `maxResultCharacters`；运维详情 API 默认仍只返回 20000 字符。BidOps AI 解析任务是当前例外：它们把 AI 响应诊断写入历史兼容字段 `deepSeekResponses`，包括 provider/model/status、原始 response body 和 assistant content，但不保存请求体、Authorization header 或 API key；详情页会单独展示 `AI 返回`。

Claim 是数据库原子更新，支持多 worker 并发：

```sql
UPDATE BackgroundJobs
SET Status = @running,
    LockedAtUtc = @now,
    LockedBy = @workerId,
    StartedAtUtc = COALESCE(StartedAtUtc, @now),
    AttemptCount = AttemptCount + 1,
    NextAttemptAtUtc = NULL,
    UpdatedAt = @now
WHERE Id = @jobId
  AND Queue = @queue
  AND CompletedAtUtc IS NULL
  AND AttemptCount < MaxAttempts
  AND AvailableAtUtc <= @now
  AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= @now)
  AND (
       Status IN (@pending, @failed)
       OR (Status = @running AND LockedAtUtc < @staleLockedBefore)
  );
```

这个设计的语义是 at-least-once。任务可能因为进程崩溃、锁超时后被重试，所以 handler 必须幂等。

## 队列和扩容

`BackgroundJobs.Queue` 是长期扩容边界。不同 worker 可以监听不同队列：

```json
{
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Enabled": true,
      "Queues": [ "tenant" ],
      "BatchSize": 20,
      "ProcessingTimeoutSeconds": 300
    }
  }
}
```

常见部署方式：

| Worker 类型 | Queues | 说明 |
| --- | --- | --- |
| `worker-default` | `[ "default" ]` | 低频通用任务。 |
| `worker-tenant` | `[ "tenant" ]` | 租户业务任务，可按业务量水平扩容。 |
| `worker-maintenance` | `[ "maintenance" ]` | 清理、补偿、报表等维护任务。 |

吞吐上涨时，优先通过增加同队列 worker 实例扩容；业务隔离不足时，再拆队列。

如果只想让某些 Worker 消费指定任务类型，可以配置 `IncludedJobTypes` / `ExcludedJobTypes`。例如新增一台只跑 BidOps AI 解析的机器：

```json
{
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Enabled": true,
      "Queues": [ "bidops" ],
      "MaxConcurrency": 4,
      "IncludedJobTypes": [
        "bidops.ai.structured-parse",
        "bidops.outcome.supplier-extract"
      ],
      "JobTypeConcurrency": {
        "bidops.ai.structured-parse": 2,
        "bidops.outcome.supplier-extract": 2
      }
    }
  }
}
```

跨机器消费同一队列时，每台 Worker 必须使用不同的 Snowflake `WorkerId` / `DatacenterId`，并指向同一套 Global/Tenant 数据库。BidOps 当前本地文件存储模式还要求所有会处理附件、文本抽取或依赖文件内容的 Worker 能访问同一份 `BidOps:FileStore:LocalRootPath`；否则应先切到共享目录、MinIO 或 S3 兼容存储。AI 专用 Worker 仍需要安装并配置相同的 AI Provider，例如 Codex CLI、DeepSeek API Key 或 Mimo API Key。

## Demo: 租户缓存预热

API 入队位置：

```text
samples/Atlas.Sample.WebApi/Controllers/BackgroundJobsController.cs
```

核心入队代码：

```csharp
await _jobClient.EnqueueAsync(
    new EnqueueBackgroundJobRequest<TenantCacheWarmupJobPayload>
    {
        JobType = TenantBackgroundJobTypes.TenantCacheWarmup,
        Queue = TenantBackgroundJobQueues.Tenant,
        TenantId = tenantId,
        StoreId = storeId,
        DeduplicationKey = $"tenant-cache-warmup:{tenantId}:{DateTime.UtcNow:yyyyMMdd}",
        Payload = new TenantCacheWarmupJobPayload(tenantId, storeId, "manual")
    },
    ct);
```

Handler 位置：

```text
src/Atlas.Services.Tenant/Runtime/BackgroundJobs/TenantCacheWarmupJob.cs
```

Handler 使用显式 `TenantId` 打开租户库，并写入 `OperationLogs` 做幂等记录：

```csharp
x.Module == "BackgroundJob"
&& x.OperationType == "TenantCacheWarmup"
&& x.EntityId == context.Job.Id
```

## Demo: 周期维护任务

周期任务位置：

```text
src/Atlas.Services.Tenant/Runtime/BackgroundJobs/TenantOutboxMaintenanceTask.cs
```

它扫描 Active/Trial 租户库，按保留时间清理已完成 outbox/inbox：

```sql
DELETE FROM TenantOutboxMessages
WHERE TenantId = @tenantId
  AND ProcessedAtUtc IS NOT NULL
  AND ProcessedAtUtc < @cutoff
LIMIT @batchSize;
```

租户库维护 SQL 必须通过 `ITenantSqlExecutor` 的命名方法执行；调用方不传完整 SQL，executor 负责生成带 `TenantId` 谓词的最终语句。

生产多实例运行时，`RecurringTaskRunner` 会使用 `IDistributedLockProvider` 加锁：

```text
atlas:recurring-task:{task.Name}
```

`CacheSettings:Provider=Memory` 时默认是内存锁，只适合单实例或开发环境；`Redis` 或 `Hybrid` 模式会自动替换为 Redis 分布式锁。Redis 锁使用 token 校验释放，避免非持有者误删其他实例的锁。复杂调度场景可以升级到 Quartz.NET 或 Hangfire，但业务 handler 和 `BackgroundJobs` 表仍可复用。

## 独立 Worker

Worker 宿主位置：

```text
src/Atlas.Worker
```

入口非常薄，只组合 Atlas DI：

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAtlasCore(
    builder.Configuration,
    typeof(OrderPlacedEventConsumer).Assembly);
var app = builder.Build();
await app.RunAsync();
```

建议生产部署：

- WebApi：`Atlas:Runtime:Mode=WebApi`，默认只负责入队和查询，不启动 consumers、tenant outbox dispatcher、`BackgroundJobWorker`、`RecurringTaskRunner`。
- Worker：`Atlas:Runtime:Mode=Worker`，默认启动 tenant outbox dispatcher、消费者、一次性任务 worker 和周期 runner。
- Worker：按 `BackgroundTasks:OneTimeJobs:Queues` 拆成多个部署单元；队列为空时自动使用 `default`。
- migration 在发布流水线执行，不在 WebApi/Worker 启动时自动迁移。

详细配置见 `docs/production_runtime_modes.md`。

## 新增业务任务

新增一次性任务：

1. 定义 payload。
2. 实现 `IBackgroundJobHandler`。
3. 定义稳定的 `JobType` 和 `Queue` 常量。
4. 在业务模块扩展方法中注册 handler。
5. 用 `IBackgroundJobClient.EnqueueAsync` 入队。
6. 保证 handler 幂等。

注册示例：

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IBackgroundJobHandler, MyJobHandler>());
```

新增周期任务：

1. 实现 `IRecurringTask`。
2. 单次执行要短、可重复、可中断。
3. 需要处理大量租户时分页扫描。
4. 重业务处理优先入队 `BackgroundJobs`。

## 运维和最佳实践

- 所有 job 必须有稳定 `JobType`，不要用类名自动生成。
- 业务 handler 必须幂等，不能假设只执行一次。
- `DeduplicationKey` 用于防重复入队，不替代 handler 内部幂等。
- 长任务不要持有 HTTP 请求上下文；租户、门店、用户信息要进 payload 或 job 字段。
- 大批量任务要分片入队，避免单个 job 执行太久。
- `ProcessingTimeoutSeconds` 要大于正常处理时间，但不能无限大。
- `Dead` 状态要接入告警或后台管理页面。
- 多租户扫描必须分页，避免一次加载所有租户。
- `BackgroundJobs` 表要按状态、队列、时间建立索引；历史成功任务需要定期归档或清理。
- RabbitMQ 仍然是业务事件的主通道，`BackgroundJobs` 是可靠任务执行器，不替代消息系统。
