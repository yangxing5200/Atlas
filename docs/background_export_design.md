# Atlas 通用后台导出设计方案

## 一、背景

Atlas 已经具备多租户、权限、数据范围、后台任务、Worker 独立部署、模块模板等脚手架能力。随着业务模块增多，后台导出会成为高频通用需求，例如订单导出、用户导出、库存导出、审计日志导出、门店经营报表导出等。

如果每个模块自行实现导出，通常会出现以下问题：

1. API 节点直接执行耗时查询，导致 HTTP 请求超时或拖垮 WebApi。
2. 导出逻辑绕过 Repository、QueryService 或数据权限，形成租户越权风险。
3. 每个模块重复实现任务状态、文件存储、下载鉴权、失败重试和清理逻辑。
4. 导出格式、文件存储、审计和告警没有统一扩展点。
5. 模块模板无法给业务开发者提供一条默认正确的实现路径。

因此，通用后台导出应作为 Atlas 脚手架的框架能力沉淀，而不是作为某个业务 demo 的局部实现。

## 二、目标

本方案目标是建立一套可维护、可持续、可扩展的后台导出架构，使业务模块只关注“导出什么数据”，框架负责“如何可靠执行导出”。

核心目标：

1. 复用现有 `Atlas.BackgroundTasks`，不引入第二套后台任务系统。
2. WebApi 只负责提交导出、查询状态和下载结果，不执行重任务。
3. Worker 负责导出任务执行，可独立扩容。
4. 导出过程显式携带租户、门店、用户、权限和查询条件。
5. 业务模块通过导出任务类型和 provider 声明导出数据源，不直接管理 job、文件和重试。
6. 默认支持 CSV，本地文件存储作为第一版实现，后续可替换对象存储。
7. 模块模板内置导出示例，生成后默认遵守 Atlas 的租户边界和数据访问约束。
8. 为未来 Excel、对象存储、定时报表、大数据量拆分、审计合规预留扩展点。

## 三、非目标

第一版不做完整报表平台，不解决所有分析型场景。

明确非目标：

1. 不实现复杂可视化报表设计器。
2. 不在第一版支持任意 SQL 导出。
3. 不允许业务模块绕过数据权限直接拼 SQL 导出。
4. 不在 WebApi 进程内执行导出。
5. 不默认实现 Excel 多 sheet、大文件 zip、Parquet 等高级格式。
6. 不将导出文件元数据塞进 `BackgroundJobs.Result` 作为长期存储。

## 四、现有架构基础

Atlas 当前已有以下能力可以直接复用：

| 能力 | 位置 | 作用 |
| --- | --- | --- |
| 一次性后台任务 | `src/Atlas.BackgroundTasks` | 持久化 job、claim、重试、dead-letter |
| 后台 Worker | `src/Atlas.Worker` | 独立承载后台执行平面 |
| 租户后台任务示例 | `src/Atlas.Services.Tenant/Runtime/BackgroundJobs` | 展示 tenant job handler 写法 |
| 任务状态表 | Global 库 `BackgroundJobs` | 记录 job 执行状态 |
| 模块模板 | `templates/atlas-module` | 生成业务模块 skeleton |
| Repository / QueryBuilder | `src/Atlas.Data.Abstractions` | 受控查询入口 |
| 权限目录 | `AtlasAuthorizationCatalogBuilder` | 模块声明权限、菜单和资源 |

结论：后台导出不应重复建设任务调度能力，而应在 `Atlas.BackgroundTasks` 上增加导出领域层。

## 五、总体方案

新增一个导出领域基础设施层，建议项目名：

```text
src/Atlas.Exporting
```

它负责：

1. 导出任务提交。
2. 导出任务 handler。
3. 按导出任务类型发现和路由 provider。
4. 导出格式 writer。
5. 导出文件存储抽象。
6. 导出业务状态和文件元数据。
7. 下载授权。
8. 过期文件清理。

总体链路：

```text
Module Controller
  -> IExportJobService.EnqueueAsync
  -> IBackgroundJobClient.EnqueueAsync
  -> Global.BackgroundJobs

Atlas.Worker
  -> ExportJobHandler
  -> IExportTaskProvider
  -> Repository / QueryService
  -> IExportFormatWriter
  -> IExportFileStore
  -> ExportJobs

WebApi
  -> IExportJobService.GetAsync
  -> IExportJobService.OpenDownloadAsync
```

## 六、项目边界

### 6.1 `Atlas.BackgroundTasks`

继续只负责通用 job 能力：

1. 入队。
2. 查询。
3. Worker claim。
4. 重试。
5. job handler 分发。

不把导出格式、文件存储、下载权限放入该项目，避免 `BackgroundTasks` 膨胀成业务基础设施集合。

### 6.2 `Atlas.Exporting`

新增导出领域能力：

1. `ExportJobService`
2. `ExportJobHandler`
3. `ExportJobPayload`
4. `ExportJobOptions`
5. `IExportTaskProvider`
6. `IExportFileStore`
7. `IExportFormatWriter`
8. `ExportJobs` 实体和配置
9. `ExportArtifactCleanupTask`

### 6.3 业务模块

业务模块只负责：

1. 定义稳定的导出任务类型。
2. 定义该任务类型的查询条件模型。
3. 实现 `IExportTaskProvider`。
4. 在模块入口注册 provider。
5. 在 controller 暴露导出入口。

业务模块不直接操作 `BackgroundJobs`，不直接保存文件，不直接实现重试和清理。

## 七、核心抽象

### 7.1 导出任务服务

```csharp
public interface IExportJobService
{
    Task<ExportEnqueueResult> EnqueueAsync<TQuery>(
        ExportEnqueueRequest<TQuery> request,
        CancellationToken ct = default);

    Task<ExportJobStatusDto?> GetAsync(
        long exportJobId,
        CancellationToken ct = default);

    Task<ExportDownloadResult> OpenDownloadAsync(
        long exportJobId,
        CancellationToken ct = default);
}
```

提交请求建议结构：

```csharp
public sealed class ExportEnqueueRequest<TQuery>
{
    public required string ExportTaskType { get; init; }
    public string? Format { get; init; }
    public required TQuery Query { get; init; }
    public string? ClientRequestId { get; init; }
}
```

`ResourceCode` 和 `PermissionCode` 不由 controller 传入，而由 `ExportTaskType` 对应的 provider 声明。这样可以避免同一个导出任务在不同入口配置出不同权限。

职责：

1. 读取当前身份。
2. 根据导出任务类型查找 provider。
3. 将查询条件序列化为稳定 JSON 快照。
4. 校验租户、用户、权限和格式。
5. 创建 `ExportJobs` 记录。
6. 调用 `IBackgroundJobClient.EnqueueAsync`。
7. 查询导出状态。
8. 下载时进行二次鉴权。

### 7.2 导出任务类型

导出设计中有三个容易混淆的概念，必须明确区分：

| 概念 | 示例 | 作用 |
| --- | --- | --- |
| `BackgroundJobs.JobType` | `export.generate` | 后台任务 runtime 路由到通用导出 handler |
| `ExportTaskType` | `module-template.tenant-record.list` | 导出领域路由到具体 provider |
| `ResourceCode` | `module-template.tenant-record` | 权限、数据范围、审计和菜单资源 |

推荐做法：

1. `BackgroundJobs.JobType` 第一版固定为 `export.generate`，避免每个业务导出都实现一个后台 handler。
2. `ExportTaskType` 是导出 provider 的唯一路由键，一种导出任务类型对应一个 provider。
3. `ResourceCode` 不承担 provider 路由职责，只用于权限、数据范围和审计。
4. 新增导出能力时，业务模块新增 provider 和查询条件模型，不扩展后台任务 runtime。

命名建议：

```text
{module}.{aggregate}.{export-scenario}
```

示例：

```text
orders.order.list
orders.order.detail-lines
inventory.stock.snapshot
audit.operation-log.list
module-template.tenant-record.list
```

同一个 `ResourceCode` 可以对应多个 `ExportTaskType`。例如订单资源可以同时有订单列表导出、订单明细行导出、订单对账导出，它们查询条件和列定义不同，因此应由不同 provider 承载。

### 7.3 导出任务 Provider

```csharp
public interface IExportTaskProvider
{
    string ExportTaskType { get; }
    string ResourceCode { get; }
    string PermissionCode { get; }
    Type QueryType { get; }
    IReadOnlyList<ExportColumn> Columns { get; }

    Task<ExportPage> ReadPageAsync(
        ExportTaskContext context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
}
```

也可以提供强类型基类降低业务模块样板代码：

```csharp
public abstract class ExportTaskProvider<TQuery> : IExportTaskProvider
{
    public abstract string ExportTaskType { get; }
    public abstract string ResourceCode { get; }
    public abstract string PermissionCode { get; }
    public Type QueryType => typeof(TQuery);
    public abstract IReadOnlyList<ExportColumn> Columns { get; }

    public abstract Task<ExportPage> ReadPageAsync(
        ExportTaskContext<TQuery> context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
}
```

强类型上下文建议结构：

```csharp
public sealed class ExportTaskContext<TQuery>
{
    public required long ExportJobId { get; init; }
    public required long TenantId { get; init; }
    public long? StoreId { get; init; }
    public required long UserId { get; init; }
    public required string ExportTaskType { get; init; }
    public required string ResourceCode { get; init; }
    public required TQuery Query { get; init; }
}
```

职责：

1. 声明导出任务类型。
2. 声明该任务类型对应的查询条件类型。
3. 声明权限和数据资源。
4. 声明列定义。
5. 根据反序列化后的查询条件分页读取数据。

约束：

1. provider 必须使用 Repository、QueryService 或经过审核的基础设施 API。
2. provider 不应直接使用 `DbContext.Set<T>()`。
3. provider 不应接收外部传入的 `TenantId` 作为信任来源。
4. provider 必须稳定排序，避免分页导出漏数或重复。
5. provider 必须只信任 `ExportTaskContext` 中的执行身份和查询条件快照。

### 7.4 查询条件序列化

导出请求的查询条件必须在提交时序列化，并作为 payload 的一部分持久化。

```csharp
public sealed record ExportSerializedQuery(
    string Json,
    string Hash,
    string TypeName,
    string SchemaVersion);
```

序列化要求：

1. 使用统一 `JsonSerializerOptions`，例如 `JsonSerializerDefaults.Web`。
2. 忽略 request body 中任何租户身份字段。
3. 对 JSON 做 canonical hash，作为审计、排查和可选 dedup 输入。
4. provider 通过 `QueryType` 反序列化，不由 controller 传递运行时对象。
5. 查询条件模型演进时通过 `SchemaVersion` 兼容老任务。

设计理由：

1. Worker 执行时可以完全复现用户提交时的查询条件。
2. 任务重试不会受到后续 HTTP request 生命周期影响。
3. 查询条件和执行结果可以被审计。
4. 不同导出任务类型可以拥有完全不同的查询条件模型。

### 7.5 文件存储

```csharp
public interface IExportFileStore
{
    Task<Stream> CreateAsync(
        string temporaryKey,
        CancellationToken ct = default);

    Task<ExportStoredFile> CommitAsync(
        string temporaryKey,
        string finalKey,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken ct = default);
}
```

第一版内置 `LocalExportFileStore`。

未来可扩展：

1. MinIO
2. 阿里云 OSS
3. AWS S3
4. Azure Blob
5. 私有对象存储

业务模块不感知具体存储实现。

### 7.6 格式 Writer

```csharp
public interface IExportFormatWriter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }

    Task<ExportWriteResult> WriteAsync(
        ExportWriteContext context,
        CancellationToken ct = default);
}
```

第一版内置 CSV writer。

未来可扩展：

1. `xlsx`
2. `jsonl`
3. `zip`
4. `parquet`

## 八、数据模型

建议新增 Global 实体 `ExportJob`，对应表名 `ExportJobs`。

字段设计：

| 字段 | 说明 |
| --- | --- |
| `Id` | 导出任务 ID，业务侧使用 |
| `BackgroundJobId` | 对应 `BackgroundJobs.Id` |
| `TenantId` | 租户 ID |
| `StoreId` | 发起时门店 ID |
| `UserId` | 发起用户 ID |
| `ExportTaskType` | 导出任务类型，用于路由 provider |
| `ResourceCode` | 权限、数据范围和审计资源编码 |
| `PermissionCode` | 导出权限编码 |
| `Format` | 导出格式 |
| `QueryJson` | 序列化后的查询条件快照 |
| `Status` | 导出业务状态 |
| `Progress` | 进度百分比 |
| `ProcessedRows` | 已处理行数 |
| `TotalRows` | 总行数，可为空 |
| `FileName` | 下载文件名 |
| `ContentType` | 文件 MIME 类型 |
| `StorageProvider` | 存储提供方 |
| `StorageKey` | 文件存储 key |
| `FileSizeBytes` | 文件大小 |
| `Sha256` | 文件摘要 |
| `QueryHash` | 查询条件 hash |
| `RequestedAtUtc` | 提交时间 |
| `StartedAtUtc` | 开始时间 |
| `CompletedAtUtc` | 完成时间 |
| `ExpiresAtUtc` | 过期时间 |
| `LastError` | 最后错误 |
| `CreatedAt` | 创建时间 |
| `UpdatedAt` | 更新时间 |

状态枚举：

```text
Pending
Running
Ready
Failed
Expired
Canceled
```

推荐索引：

```text
UX_ExportJobs_BackgroundJobId
IX_ExportJobs_TenantId_UserId_RequestedAtUtc
IX_ExportJobs_TenantId_Status
IX_ExportJobs_ExpiresAtUtc
IX_ExportJobs_ExportTaskType
IX_ExportJobs_ResourceCode
```

`BackgroundJobs` 是执行状态来源，`ExportJobs` 是导出领域状态和文件元数据来源。两者不互相替代。

`ExportTaskType`、`QueryJson`、`QueryHash`、`ResourceCode`、`PermissionCode` 在导出提交后应视为不可变快照。`BackgroundJobs.Payload` 中保存同一份查询快照用于执行，`ExportJobs` 中保存查询快照用于审计、排查和后台管理。两处数据必须由 `ExportJobService` 在同一提交流程内生成，业务模块不应在任务执行期间重新组装查询条件。

## 九、任务 Payload

`ExportJobPayload` 必须可版本化，避免直接序列化复杂对象。

建议结构：

```csharp
public sealed record ExportJobPayload(
    long ExportJobId,
    long TenantId,
    long? StoreId,
    long UserId,
    string ExportTaskType,
    string ResourceCode,
    string PermissionCode,
    string Format,
    string QueryJson,
    string QueryHash,
    string Culture,
    string TimeZone,
    int PageSize,
    int? MaxRows,
    string SchemaVersion);
```

设计理由：

1. `ExportJobId` 用于 handler 回写导出业务状态。
2. `TenantId`、`StoreId`、`UserId` 显式保存，Worker 不依赖 HTTP 上下文。
3. `ExportTaskType` 路由到 `IExportTaskProvider`。
4. `ResourceCode` 用于权限、数据范围和审计，不用于 provider 路由。
5. `PermissionCode` 用于执行时二次校验。
6. `QueryJson` 保存查询条件快照，provider 根据自身 `QueryType` 反序列化。
7. `QueryHash` 用于审计、排查和可选去重。
8. `Culture`、`TimeZone` 用于格式化时间和数字。
9. `SchemaVersion` 为后续 payload 演进留出口。

## 十、队列与扩容

建议新增导出队列：

```csharp
public static class ExportBackgroundJobQueues
{
    public const string Export = "export";
}
```

导出任务不建议复用 `tenant` 队列，原因：

1. 导出通常 I/O 重，容易拖慢普通租户后台任务。
2. 导出吞吐与业务异步任务吞吐不同。
3. 后续可以单独扩容导出 Worker。
4. 可以为导出设置不同的超时、batch size 和告警策略。

Worker 配置示例：

```json
{
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Enabled": true,
      "Queues": [ "export" ],
      "BatchSize": 5,
      "ProcessingTimeoutSeconds": 1800
    }
  }
}
```

## 十一、权限与数据范围

导出是高风险能力，必须遵守更严格的权限和数据范围策略。

### 11.1 提交时校验

提交导出时：

1. 从当前身份读取 `TenantId`、`StoreId`、`UserId`。
2. 不信任 request body 中的租户字段。
3. 校验 `ExportTaskType` 对应 provider 是否存在。
4. 使用 provider 的 `QueryType` 验证并序列化查询条件。
5. 校验当前用户具备 provider 声明的 `PermissionCode`。
6. 校验导出格式是否启用。

### 11.2 执行时校验

Worker 执行时再次校验权限。

理由：

1. 用户可能在排队期间被禁用。
2. 用户可能在排队期间被撤销导出权限。
3. 租户可能在排队期间停用。

默认策略：执行时权限为准。若执行时已无权限，任务应失败并记录明确错误。

### 11.3 下载时校验

下载时再次校验：

1. 当前租户等于 `ExportJobs.TenantId`。
2. 当前用户等于 `ExportJobs.UserId`，或具备导出管理权限。
3. 文件未过期。
4. 状态为 `Ready`。
5. 当前用户仍具备对应资源的导出权限。

### 11.4 数据范围

导出数据范围应通过现有 Repository、QueryBuilder 和数据权限能力解析。

默认策略：

1. 提交时记录用户和门店上下文。
2. 执行时以该用户身份重新解析数据范围。
3. 不把门店可见列表作为长期信任快照写入 payload。

这样可以避免组织架构或权限变更后导出越权。

## 十二、后台身份上下文

当前部分 Repository 和数据范围能力依赖 `ICurrentIdentity`，Worker 没有 HTTP 上下文。为了让后台任务安全复用应用层查询能力，建议引入后台执行身份上下文。

新增抽象：

```csharp
public interface IExecutionIdentityAccessor
{
    ExecutionIdentitySnapshot? Current { get; }
    IDisposable Begin(ExecutionIdentitySnapshot snapshot);
}
```

`ExecutionIdentitySnapshot`：

```csharp
public sealed record ExecutionIdentitySnapshot(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    string? SessionId,
    bool IsAuthenticated);
```

`ICurrentIdentity` 实现调整为：

1. 优先读取 `IExecutionIdentityAccessor.Current`。
2. 没有后台身份时再读取 `HttpContext.User`。

收益：

1. 导出 handler 可以安全复用 QueryService。
2. 消费者、周期任务、维护任务未来也能复用同一机制。
3. 不需要伪造 HTTP 上下文。
4. 租户边界从“请求上下文”扩展为“执行上下文”。

## 十三、幂等与重复提交

导出任务是否去重不能一刀切。

### 13.1 手动导出

默认允许重复提交。

理由：

1. 用户可能需要多次生成同一条件的文件。
2. 数据可能在两次提交之间变化。
3. 强行使用查询 hash 去重会造成用户误解。

### 13.2 前端防重复点击

支持可选 `ClientRequestId`。

dedup key 示例：

```text
export:manual:{tenantId}:{userId}:{clientRequestId}
```

### 13.3 定时报表

定时报表必须去重。

dedup key 示例：

```text
export:schedule:{tenantId}:{scheduleId}:{period}
```

### 13.4 Handler 幂等

handler 必须使用临时 storage key 写文件：

```text
exports/tmp/{jobId}/{attempt}/data.csv
```

成功后 commit 到最终 key：

```text
exports/{tenantId}/{yyyyMMdd}/{exportJobId}/data.csv
```

重试时可以覆盖或清理临时文件，不影响已完成文件。

## 十四、文件存储策略

第一版本地存储建议：

```text
var/exports/{tenantId}/{yyyyMMdd}/{exportJobId}/{fileName}
```

配置示例：

```json
{
  "Exporting": {
    "DefaultFormat": "csv",
    "RetentionDays": 7,
    "LocalStorage": {
      "RootPath": "var/exports"
    }
  }
}
```

生产建议：

1. 单机或开发环境可以使用本地存储。
2. 多实例生产环境优先使用对象存储。
3. 若必须使用本地存储，下载请求需要路由到同一节点或使用共享挂载。

为了避免第一版过度复杂，先提供本地实现和清晰接口，后续增加对象存储实现。

## 十五、导出格式策略

第一版推荐只内置 CSV。

理由：

1. 支持流式写入。
2. 内存占用低。
3. 依赖少。
4. 适合大多数列表型导出。
5. 易于验证整体架构。

CSV writer 约束：

1. 使用 UTF-8 with BOM 或可配置编码，兼容 Excel 打开。
2. 正确处理逗号、双引号、换行。
3. 列顺序来自 provider 的 `Columns`。
4. 日期、数字使用 payload 中的 `Culture` 和 `TimeZone`。

后续格式：

| 格式 | 场景 | 注意事项 |
| --- | --- | --- |
| `xlsx` | 运营人员直接查看 | 控制行数和内存 |
| `jsonl` | 系统集成 | 一行一条，便于流式处理 |
| `zip` | 多文件或拆分导出 | 下载和清理复杂度更高 |
| `parquet` | 分析型数据 | 需要额外依赖和 schema 管理 |

## 十六、导出执行流程

详细流程：

1. Controller 调用 `IExportJobService.EnqueueAsync`。
2. `ExportJobService` 获取当前身份。
3. `ExportJobService` 根据 `ExportTaskType` 找到 provider。
4. `ExportJobService` 使用 provider 的 `QueryType` 校验并序列化查询条件。
5. `ExportJobService` 校验权限、格式和参数。
6. `ExportJobService` 写入 `ExportJobs`，状态为 `Pending`。
7. `ExportJobService` 调用 `IBackgroundJobClient.EnqueueAsync`，`JobType` 固定为 `export.generate`。
8. `ExportJobService` 回写 `BackgroundJobId`。
9. Worker claim `BackgroundJobs`。
10. `ExportJobHandler` 读取 payload。
11. `ExportJobHandler` 设置执行身份上下文。
12. `ExportJobHandler` 重新校验租户、用户和权限。
13. `ExportJobHandler` 根据 `ExportTaskType` 找到 provider。
14. `ExportJobHandler` 根据 provider 的 `QueryType` 反序列化 `QueryJson`。
15. `ExportJobHandler` 找到格式 writer。
16. writer 分页调用 provider 读取数据。
17. writer 写入临时文件。
18. 写入成功后 commit 文件。
19. 更新 `ExportJobs` 为 `Ready`。
20. `BackgroundJobs` 标记 `Succeeded`。
21. 用户通过下载接口获取文件。

失败流程：

1. handler 捕获可记录异常。
2. 更新 `ExportJobs.LastError`。
3. 更新 `ExportJobs.Status = Failed`。
4. 由 `BackgroundJobs` 负责重试或进入 `Dead`。
5. 重试开始时重新设置 `Running` 并使用新的临时 key。

## 十七、模块模板集成

`templates/atlas-module` 应生成一套最小但完整的导出示例。

新增文件建议：

```text
templates/atlas-module/BackgroundJobs/TenantRecordListExportProvider.cs
templates/atlas-module/Models/ExportTenantRecordsRequest.cs
```

调整文件：

```text
templates/atlas-module/Controllers/TenantRecordsController.cs
templates/atlas-module/ModuleEntry.cs
templates/atlas-module/Tests/Atlas.ModuleTemplate.Tests/ModuleTemplateTests.cs
scripts/verify-atlas-module-template.ps1
docs/atlas_module_template.md
```

模板生成接口：

```text
POST /api/tenant-records/exports
GET  /api/tenant-records/exports/{exportJobId}
GET  /api/tenant-records/exports/{exportJobId}/download
```

模板新增权限：

```csharp
public const string TenantRecordsExport = "module-template.tenant-record.export";
```

模板新增导出任务类型：

```csharp
public static class ModuleTemplateExportTaskTypes
{
    public const string TenantRecordList = "module-template.tenant-record.list";
}
```

模板注册 provider：

```csharp
context.Services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IExportTaskProvider, TenantRecordListExportProvider>());
```

模板 provider 只允许使用：

1. `IRepository<TenantRecord>`
2. `ITenantRecordQueryService`
3. 其他受控 QueryService

不允许使用：

1. `AtlasTenantDbContext`
2. `ITenantDbContextFactory`
3. `DbContext.Set<T>()`
4. `FromSql`
5. `ExecuteSql`
6. `IgnoreQueryFilters`

## 十八、API 设计建议

业务 controller 示例：

```csharp
[HttpPost("exports")]
public async Task<ActionResult<ExportEnqueueResult>> ExportAsync(
    [FromBody] ExportTenantRecordsRequest request,
    CancellationToken ct)
{
    var result = await _exports.EnqueueAsync(
        new ExportEnqueueRequest<ExportTenantRecordsRequest>
        {
            ExportTaskType = ModuleTemplateExportTaskTypes.TenantRecordList,
            Format = request.Format,
            Query = request
        },
        ct);

    return Accepted($"/api/tenant-records/exports/{result.ExportJobId}", result);
}
```

状态响应：

```json
{
  "exportJobId": 123,
  "backgroundJobId": 456,
  "exportTaskType": "module-template.tenant-record.list",
  "resourceCode": "module-template.tenant-record",
  "format": "csv",
  "status": "Ready",
  "progress": 100,
  "processedRows": 1500,
  "fileName": "tenant-records-20260604.csv",
  "expiresAtUtc": "2026-06-11T00:00:00Z"
}
```

## 十九、配置设计

建议配置：

```json
{
  "Exporting": {
    "Enabled": true,
    "DefaultFormat": "csv",
    "AllowedFormats": [ "csv" ],
    "DefaultPageSize": 500,
    "MaxPageSize": 2000,
    "DefaultMaxRows": 100000,
    "RetentionDays": 7,
    "StorageProvider": "Local",
    "LocalStorage": {
      "RootPath": "var/exports"
    }
  }
}
```

配置校验：

1. `DefaultPageSize > 0`
2. `MaxPageSize >= DefaultPageSize`
3. `RetentionDays > 0`
4. `AllowedFormats` 不能为空
5. `StorageProvider` 必须有对应实现

## 二十、可观测性

导出任务需要接入日志、指标和 tracing。

日志字段：

1. `ExportJobId`
2. `BackgroundJobId`
3. `TenantId`
4. `UserId`
5. `ExportTaskType`
6. `ResourceCode`
7. `Format`
8. `ProcessedRows`
9. `DurationMs`

指标建议：

| 指标 | 类型 | 说明 |
| --- | --- | --- |
| `atlas.export.jobs.started` | counter | 导出开始数 |
| `atlas.export.jobs.succeeded` | counter | 导出成功数 |
| `atlas.export.jobs.failed` | counter | 导出失败数 |
| `atlas.export.rows.processed` | counter | 导出行数 |
| `atlas.export.file.bytes` | histogram | 文件大小 |
| `atlas.export.duration` | histogram | 导出耗时 |

Tracing tag：

1. `atlas.export.job_id`
2. `atlas.export.task_type`
3. `atlas.export.resource_code`
4. `atlas.export.format`
5. `atlas.tenant_id`

## 二十一、清理与生命周期

新增周期任务：

```csharp
public sealed class ExportArtifactCleanupTask : IRecurringTask
```

职责：

1. 扫描 `ExportJobs.ExpiresAtUtc < now` 且状态为 `Ready` 的记录。
2. 删除存储文件。
3. 将状态更新为 `Expired`。
4. 记录删除失败错误，但不阻断下一批。

清理任务必须分页执行。

配置：

```json
{
  "Exporting": {
    "Cleanup": {
      "Enabled": true,
      "IntervalMinutes": 60,
      "BatchSize": 100
    }
  }
}
```

## 二十二、未来场景预演

### 22.1 大数据量导出

风险：

1. 单文件过大。
2. 查询耗时过长。
3. Worker 长时间占用。
4. 下载体验差。

应对：

1. provider 必须分页读取。
2. 强制稳定排序。
3. 配置 `MaxRows`。
4. 超过阈值时失败并提示缩小条件，或后续支持分片 zip。
5. 导出 Worker 独立扩容。

### 22.2 定时报表

实现方式：

1. 新增 schedule 表和管理 API。
2. `IRecurringTask` 扫描到期计划。
3. 按租户和周期入队 export job。
4. dedup key 使用 `scheduleId + period`。

不需要改 `ExportJobHandler`。

### 22.3 多租户批量导出

原则：

1. 控制面可以扫描租户。
2. 执行面必须拆成每租户一个 job。
3. 单个 job 只处理一个租户。
4. 不允许一个导出 job 同时打开多个租户库并混合输出。

### 22.4 对象存储迁移

第一版本地存储，未来替换对象存储时：

1. 新增 `ObjectStorageExportFileStore`。
2. 配置 `StorageProvider = "ObjectStorage"`。
3. 新任务写入对象存储。
4. 历史本地文件通过迁移任务搬迁或保留到过期。

业务模块无需修改。

### 22.5 Excel 多 sheet

新增 `XlsxExportFormatWriter`。

需要额外设计：

1. sheet 名称。
2. 多数据集 provider。
3. 单 sheet 最大行数。
4. 内存控制。

建议在 CSV 路径稳定后再做。

### 22.6 敏感数据导出

敏感字段导出应接入权限和审计：

1. provider 列定义标记敏感列。
2. 默认导出脱敏值。
3. 只有具备高风险权限时可导出明文。
4. 明文导出必须写审计。

可复用现有 `sensitive.export` 权限思路。

### 22.7 用户撤权或离职

默认策略：

1. 执行时重新校验用户状态和权限。
2. 下载时再次校验。
3. 已生成但用户被撤权后，不允许继续下载。

### 22.8 Worker 崩溃与重试

依赖 `BackgroundJobs` at-least-once 语义。

handler 要求：

1. 使用临时文件。
2. 成功后 commit。
3. 回写状态幂等。
4. 重试前可以清理上一次临时文件。

## 二十三、风险与取舍

### 23.1 为什么不直接在模块里生成完整导出 job

问题：

1. 重复代码多。
2. 每个模块都要理解 `BackgroundJobs` 细节。
3. 文件存储和下载授权难以统一。
4. 后续改对象存储或格式会影响所有模块。

结论：模块模板只生成 provider 和入口，框架提供通用能力。

### 23.2 为什么新增 `ExportJobs` 表

`BackgroundJobs` 是任务执行表，不适合承载导出领域元数据。

导出需要：

1. 文件名。
2. 文件大小。
3. 文件 hash。
4. 存储 key。
5. 过期时间。
6. 下载权限。
7. 业务状态。

结论：新增 `ExportJobs` 更清晰，也便于后续后台管理。

### 23.3 为什么第一版只做 CSV

CSV 能验证完整架构，且低依赖、低内存、易流式。Excel 虽然常见，但更容易把第一版拖入格式细节和内存问题。

结论：第一版 CSV，第二阶段补 xlsx。

### 23.4 为什么执行时重新校验权限

排队期间权限可能变化。若只在提交时校验，可能产生越权导出。

结论：提交、执行、下载三段校验。

## 二十四、实施计划

### 阶段 1：导出基础设施

1. 新增 `Atlas.Exporting` 项目。
2. 新增核心抽象和 DTO。
3. 新增 `ExportJob` Global 实体和 EF configuration。
4. 新增 migration。
5. 实现 `ExportJobService`。
6. 实现 `ExportJobHandler`。
7. 实现 CSV writer。
8. 实现本地 file store。
9. 接入 DI 扩展。

### 阶段 2：运行时接入

1. Worker 默认可注册导出 handler。
2. 新增 `export` 队列常量。
3. 增加配置校验。
4. 增加健康检查或 ready 状态扩展。
5. 增加清理周期任务。

### 阶段 3：模块模板接入

1. 模板新增导出 provider 示例。
2. 模板新增导出 request。
3. controller 增加导出提交、状态、下载接口。
4. `ModuleEntry` 增加导出权限和 provider 注册。
5. 模板测试覆盖 request 不接受 `TenantId`。
6. verify 脚本继续禁止直接数据访问 API。
7. 更新模块模板文档。

### 阶段 4：增强能力

1. xlsx writer。
2. 对象存储 file store。
3. 导出审计。
4. 导出后台管理 API。
5. 定时报表。
6. 大文件拆分 zip。

## 二十五、验收标准

第一版验收：

1. `dotnet restore Atlas.sln` 成功。
2. `dotnet build Atlas.sln --no-restore` 成功。
3. WebApi 模式不会执行导出任务。
4. Worker 模式可以执行 `export` 队列任务。
5. 模板生成模块后 build/test 通过。
6. 模板导出 provider 不包含直接 `DbContext` 访问。
7. 导出提交时不允许 request body 指定 `TenantId`。
8. 导出执行时可以正确设置租户和用户上下文。
9. 下载接口进行租户、用户、权限和过期校验。
10. 导出成功后生成文件并更新 `ExportJobs`。
11. 导出失败后记录错误，`BackgroundJobs` 仍负责重试。
12. 清理任务可以删除过期文件并标记 `Expired`。

## 二十六、推荐结论

Atlas 的通用后台导出应采用“框架能力 + 模块 provider + 后台任务执行”的三层设计。

推荐最终边界：

```text
Atlas.BackgroundTasks
  负责可靠任务执行

Atlas.Exporting
  负责导出领域通用能力

Business Module
  负责声明导出任务类型、查询条件模型和分页数据源

Atlas.Worker
  负责执行导出任务

WebApi
  负责提交、查询和下载
```

第一版应优先把租户边界、权限边界、执行边界和扩展边界定稳。CSV、本地存储和模板示例足以验证主链路。后续再逐步增加 xlsx、对象存储、定时报表和大文件拆分能力。

这条路径最符合 Atlas 当前脚手架目标：让业务开发者专注模块实体、服务、查询和控制器，把后台任务、权限、文件存储、重试、清理和部署职责交给框架统一处理。
