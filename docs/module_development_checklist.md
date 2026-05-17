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
4. API 入参不接受业务可篡改的 `TenantId`，租户来自 token 或显式系统流程。

## 数据访问

1. 业务/API 项目不得引用 `AtlasTenantDbContext`、`ITenantDbContextFactory` 或 EF `DbContext`。
2. 不直接调用 `DbContext.Set<T>()`、`FromSql*`、`ExecuteSqlRaw`、`ExecuteSqlInterpolated`。
3. 租户库 SQL 必须通过 `ITenantSqlExecutor`，并在 SQL 中包含 `TenantId`。
4. 唯一索引和幂等键按租户隔离，例如 `TenantId + BusinessKey`。

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
