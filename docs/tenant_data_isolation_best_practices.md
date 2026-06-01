# 多租户数据隔离最佳实践

Atlas 的租户业务数据按 `TenantId` 存放在租户库中。当前设计允许多个租户共用同一个租户库，因此不能把“拿到了某个租户的 DbContext”理解为已经完成租户隔离。所有租户业务表的读、写、更新、删除都必须在 SQL 语义上带有租户边界。

## 核心原则

1. 租户业务实体必须实现 `ITenantEntity`，需要门店范围的实体继续实现 `ISharedEntity` 或 `IStoreOnlyEntity`。
2. 业务查询优先使用 `IRepository<TEntity>`，仓储内部会通过 `ScopedSet` 套用 `TenantId` 和门店范围。
3. 业务层不直接依赖 `AtlasTenantDbContext`，需要访问租户数据时通过 Repository、QueryService 或领域服务进入。
4. 原生 SQL、`ExecuteSql*`、批量更新、批量删除不会自动应用 `ScopedSet`，必须把 `TenantId` 放进 `WHERE`。
5. 租户内幂等和唯一约束必须包含 `TenantId`，例如 inbox 去重、outbox 事件、后台任务 dedup key。

## 推荐访问方式

普通业务查询使用仓储：

```csharp
var query = await repository.QueryAsync(ct);
var item = await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
```

仓储会调用 `AtlasTenantDbContext.ScopedSet<TEntity>()`，最终由 `EntityScopeFilter` 根据实体接口追加：

- `ITenantEntity`: `TenantId == 当前租户`
- `IStoreOnlyEntity`: `StoreId == 当前门店`
- `ISharedEntity`: `StoreId IN 当前可共享门店列表`

登录、初始化、后台任务等没有 HTTP Token 的路径，需要显式传入 `tenantId`：

```csharp
var query = await repository.QueryAsync(tenantId, ct);
```

## 访问边界设计

不要通过把整个 `AtlasTenantDbContext.Set<TEntity>()` 标记为 `Obsolete` 来解决问题。这个做法过于粗，会误伤 EF 基础能力，也会让基础设施代码缺少合理入口。

推荐采用类似 Ares 的分层方式：

- `DbContext` 可以保留 EF 的 `Set<TEntity>()` 能力，但业务层不直接注入或使用 `AtlasTenantDbContext`。
- 普通业务实体通过 Repository 暴露访问能力，Repository 内部统一调用 `ScopedSet`。
- 高风险基础设施实体不在业务上下文上暴露 `DbSet` 属性，只由专用 Repository 或基础设施服务访问。
- 对 outbox/inbox、权限边界表等业务不应直接访问的实体，后续优先考虑移动到基础设施程序集并降低类型可见性。
- `Atlas.Analyzers` 已经接入构建：业务/API 层直接引用 `AtlasTenantDbContext`、`ITenantDbContextFactory`、调用 `DbContext.Set<T>()`、`FromSql*` 或直接 `ExecuteSql*` 会编译失败。

仅仅不声明 `DbSet<TEntity>` 属性不能完全禁止下面写法：

```csharp
var stores = await tenantDb.Set<Store>().ToListAsync(ct);
```

只要实体在 EF Model 中且调用方能引用实体类型，`Set<TEntity>()` 语法仍然成立。因此更重要的是控制依赖方向：业务代码不拿 DbContext，高风险实体不向业务程序集暴露。

实体定义仍然需要通过接口声明隔离语义：

- `ITenantEntity`: 必须限定租户。
- `ISharedEntity`: 必须限定租户，并按共享门店组过滤。
- `IStoreOnlyEntity`: 必须限定租户，并只允许当前门店。

普通业务代码应使用 Repository：

```csharp
var query = await productRepository.QueryAsync(ct);
var products = await query.Where(x => x.IsActive).ToListAsync(ct);
```

新增租户实体时：

1. 实体类放在 `src/Atlas.Core/Entities/Tenant`，并按隔离语义实现 `ITenantEntity`、`ISharedEntity` 或 `IStoreOnlyEntity`。
2. EF 映射放在 `src/Atlas.Data.Tenant.Migrations/EntityConfigurations`，通过 `ApplyConfigurationsFromAssembly` 加入模型。
3. 不在 `AtlasTenantDbContext` 上新增 `DbSet<TEntity>` 属性。
4. 普通访问使用 `IRepository<TEntity>`；需要复杂查询时定义 QueryService 或专用 Repository，并把它注册到 DI。

Repository 或基础设施代码确需使用 EF `Set<TEntity>()` 时，不应新增公开逃生口。允许的直接 `Set<TEntity>()` 调用应收敛在 Repository、outbox/inbox、初始化工具等基础设施代码中，并必须满足下面至少一种条件：

- 查询的是 Global 库实体，不属于租户业务数据。
- 查询租户业务实体时，表达式中显式包含 `TenantId == tenantId`。
- 写入租户业务实体时，实体的 `TenantId` 来自可信上下文，并在保存前校验与当前租户一致。
- 后台扫描多个租户时，每次处理单个租户，并且该租户内的所有 `SELECT/UPDATE/DELETE` 都带 `TenantId`。

基础设施示例：

```csharp
var messages = await db.Set<TenantOutboxMessage>()
    .Where(x => x.TenantId == tenantId && x.ProcessedAtUtc == null)
    .ToListAsync(ct);
```

## Atlas 实体分层建议

当前实体可以先按访问职责分为三类：

| 类别 | 示例实体 | 访问方式 | 后续治理 |
| --- | --- | --- | --- |
| 普通租户业务实体 | `Store`、`User`、`UserStore`、`OperationLog` | Repository / QueryService | 不在业务代码中直接注入 `AtlasTenantDbContext` |
| 门店范围业务实体 | `Product`、`Member`、`Promotion`、`Order`、`Inventory`、`CashierRecord` | Repository 内部走 `ScopedSet` | 按 `ISharedEntity` / `IStoreOnlyEntity` 保持范围语义 |
| 租户基础设施实体 | `TenantOutboxMessage`、`TenantInboxMessage` | outbox/inbox 专用服务或专用 Repository | 后续从 `Core` public entity 中迁出，降低类型可见性 |
| Global 库租户相关实体 | `BackgroundJob` | Global Repository / background task service | 租户任务查询、dedup 唯一键必须包含 `TenantId` |

业务层对 `AtlasTenantDbContext` 的依赖必须收口到 Repository、QueryService 或明确的基础设施服务。基础设施实体仍应继续治理：优先建立专用 Repository，并逐步移除业务程序集对这些实体类型的引用。

租户库原生 SQL 必须通过 `ITenantSqlExecutor` 的命名方法进入受控入口。调用方不能传入完整 SQL 字符串，只能选择框架提供的基础设施动作：

```csharp
await tenantSqlExecutor.DeleteReceivedInboxMessagesAsync(
    db,
    tenantId,
    cutoff,
    batchSize,
    ct);
```

新增 SQL 动作时必须在 `ITenantSqlExecutor` 上增加窄方法，显式接收 `tenantId`，并用单元测试验证缺少租户、错误租户、正常执行三类场景。确需跨租户运维时，应建立单独命名的基础设施方法，并在 Code Review 中说明原因。

## 写入和审计

HTTP 请求内写入时，`AuditInterceptor` 会根据 `ICurrentIdentity` 填充 `TenantId`、`StoreId`、审计字段。这个机制是防线之一，但不能替代查询过滤。

仓储的当前租户写入方法会先校验租户实体：

- 当前上下文没有 `TenantId` 时，不能写入租户实体，应改用显式 `tenantId` 重载。
- `entity.TenantId == 0` 时由仓储填入当前租户。
- `entity.TenantId` 已有值时，必须与当前租户一致。

显式租户写入路径必须遵守：

- `entity.TenantId == 0` 时可由仓储填入显式 `tenantId`。
- `entity.TenantId` 已有值时，必须与显式 `tenantId` 一致。
- 不允许把 A 租户实体传给 B 租户的显式写入或删除方法。

## 消息和后台任务

租户 outbox/inbox 是租户业务表，必须按租户隔离：

- outbox 拉取消息时过滤 `TenantId`。
- outbox 抢占消息的 `UPDATE` 同时匹配 `Id` 和 `TenantId`。
- inbox 去重同时匹配 `TenantId`、`MessageId`、`ConsumerName`。
- outbox payload 中的事件 `TenantId` 必须与 outbox 行的 `TenantId` 一致。
- 清理任务删除 outbox/inbox 时必须带 `TenantId`。

后台任务存放在 Global 库，但如果任务属于租户，API 查询和 dedup 也要按 `TenantId` 限定，避免一个租户看到或复用另一个租户的任务。

## 索引和约束

共享租户库下，不要建立跨租户唯一约束。应使用租户内唯一索引：

- `TenantOutboxMessages`: `TenantId + EventId`
- `TenantInboxMessages`: `TenantId + MessageId + ConsumerName`
- `BackgroundJobs`: `TenantId + DeduplicationKey`

业务唯一键也应优先采用 `TenantId + BusinessKey`，例如订单号使用 `TenantId + OrderNo`。

## Code Review 检查清单

完整 PR 清单见 `docs/tenant_boundary_pr_review_checklist.md`。看到以下代码时必须检查租户边界：

- 业务层直接注入或使用 `AtlasTenantDbContext`
- `db.Set<租户实体>()`
- `ExecuteSqlRaw` / `ExecuteSqlInterpolated`
- `FromSql*`
- `BulkUpdateAsync` / `BulkInsertAsync`
- `IgnoreQueryFilters`
- 后台任务扫描所有租户
- consumer/outbox/inbox 幂等逻辑
- 请求体中允许传入 `TenantId`

如果不是 Global 数据，最终 SQL 必须能回答两个问题：

1. 这条语句是否限定了正确的 `TenantId`？
2. 如果实体属于门店范围，是否限定了正确的门店可见范围？
