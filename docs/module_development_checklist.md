# 模块开发脚手架检查清单

这个清单用于约束新业务模块的默认写法，让工程师主要关注业务代码，而不是重复处理租户、门店范围、事务、消息和后台任务边界。

## 新增租户实体

1. 实体定义在 `src/Atlas.Core/Entities/Tenant`。
2. 必须实现 `ITenantEntity`；门店范围数据继续实现 `ISharedEntity` 或 `IStoreOnlyEntity`。
3. EF 映射定义在 `src/Atlas.Data.Tenant.Migrations/EntityConfigurations`，由 `AtlasTenantDbContext.ApplyConfigurationsFromAssembly` 自动加载。
4. 不在 `AtlasTenantDbContext` 增加 `DbSet<TEntity>` 属性，也不新增裸查询入口。
5. 默认使用 `IRepository<TEntity>`；复杂读模型使用 QueryService，复杂写模型使用领域服务或专用 Repository。

## API 层

1. WebApi 使用 `builder.AddAtlasWebApi()` 和 `app.UseAtlasWebApi()`。
2. Controller 不捕获通用 `Exception`，统一由 `AtlasExceptionHandler` 输出 ProblemDetails。
3. 需要租户管理员权限时使用 `AuthorizationPolicies.RequireTenantAdmin`。
4. API 入参不接受业务可篡改的 `TenantId`；租户来自 token、后台任务上下文或明确的系统初始化流程。
5. 门店范围来自当前身份、切换门店上下文或服务端校验后的显式参数，不信任请求体直接提交的 `StoreId` 扩权。

## 数据访问

1. 业务/API 项目不得引用 `AtlasTenantDbContext`、`ITenantDbContextFactory` 或 EF `DbContext`。
2. 不直接调用 `DbContext.Set<T>()`、`FromSql*`、`ExecuteSqlRaw`、`ExecuteSqlInterpolated`、`IgnoreQueryFilters`。
3. 复杂读模型新增 `I{Feature}QueryService` / `{Feature}QueryService`，实现只组合 Repository/QueryBuilder 或批准 reader。
4. 租户库 SQL 只能通过 `ITenantSqlExecutor` 的命名方法进入，不新增通用 SQL 字符串执行入口。
5. 唯一索引和幂等键按租户隔离，例如 `TenantId + BusinessKey`。

## 数据类型边界

1. Global 数据：可跨租户存放，但任何租户可见记录必须带 `TenantId`，查询、去重、后台任务领取都按 `TenantId` 限定。
2. Tenant 数据：实体实现 `ITenantEntity`，Repository 写入会填充或校验 `TenantId`，查询必须通过 Repository/QueryService。
3. Shared 数据：实体实现 `ISharedEntity`，查询默认只允许当前门店和共享组可见门店，PR 中必须说明任何显式 `StoreId` 过滤不会扩大范围。
4. StoreOnly 数据：实体实现 `IStoreOnlyEntity`，查询和写入只面向当前门店；跨门店汇总必须走批准的 QueryService 或后台任务 reader。
5. 基础设施实体：例如 tenant outbox/inbox 只能由 runtime store/service 访问，业务服务和 consumer 不直接查询这些实体。

## 自动门禁映射

1. Analyzer：`ATL001` 阻断普通业务代码引用 `AtlasTenantDbContext` / `ITenantDbContextFactory`。
2. Analyzer：`ATL002` 阻断普通业务代码调用 `DbContext.Set<T>()`。
3. Analyzer：`ATL003` 阻断普通业务代码调用 EF raw SQL。
4. 模板验收：生成的 Service、QueryService、Controller 不包含 `DbContext`、`Set<T>`、`FromSql`、`ExecuteSql`、`IgnoreQueryFilters`。
5. Review 验收：请求模型不暴露可篡改 `TenantId`，Shared/StoreOnly 查询说明范围来源，Global 租户相关表说明 `TenantId` 谓词或唯一键。

## 消息和后台任务

1. 租户事件先写租户 outbox，由 Worker 投递。
2. Consumer 继承 `TenantConsumerBase<TEvent>`，注入 `ITenantConsumerRuntime` 和 Repository/领域服务，不要重复手写 inbox、事务模板或直接访问 DbContext。
3. 租户后台任务的 Global 表查询必须按 `TenantId` 限定。
4. 生产环境 WebApi 只入队和查询；Worker 执行 consumer、outbox dispatcher 和后台任务。

## 提交前验证

```bash
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj --no-build
dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-build
dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build
```
