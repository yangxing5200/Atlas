# 登录异步租户连接解析指南

本文档说明 Atlas 登录流程中，登录方法本身是异步的，在拿到租户库连接字符串之后，系统如何保证 `DbContext` 连接被正确创建、复用、保存并释放，以及如何避免过去常见的“异步方法被同步调用”问题。

## 一、核心结论

1. **登录入口必须全链路异步**：`UserService.LoginAsync` 从租户验证、租户库查询、门店查询、Token 签发、登录日志写入到 `SaveChangesAsync` 都使用 `await`，不通过 `.Result`、`.Wait()` 或 `GetAwaiter().GetResult()` 强行同步等待。
2. **登录时不能依赖 `ICurrentIdentity.TenantId`**：登录请求还没有 AccessToken，也就没有当前身份上下文。系统先用登录域名在 Global 库查询租户，再把 `tenant.Id` 显式传给租户库仓储和工作单元。
3. **连接字符串解析和 `DbContext` 创建都在异步工厂内完成**：仓储调用 `QueryAsync(tenantId)`、`QueryTrackingAsync(tenantId)` 或 `AddAsync(entity, tenantId)` 时，会进入 `TenantDbContextFactory.GetReadonlyDbContextAsync(tenantId)` 或 `GetDbContextAsync(tenantId)`，由工厂先 `await` 连接字符串解析，再创建 EF Core `AtlasTenantDbContext`。
4. **同一个 DI Scope 内按租户缓存 `DbContext` 实例**：登录流程中多次访问同一个租户库时，同一个 `tenantId` 的主库、只读库、报表库 `DbContext` 会分别缓存在工厂的字典中，避免重复创建连接上下文，也保证写入和保存使用同一个主库上下文。
5. **并发初始化受 `SemaphoreSlim` 保护**：如果同一个请求作用域内出现多处同时初始化租户上下文，工厂会用异步锁做双重检查，确保不会因为竞态创建多个同类型上下文。
6. **预加载中间件只是暖缓存，不是正确性的前提**：已认证请求会提前异步预热主库和只读库连接字符串；登录请求没有 `TenantId`，通常不会走预加载。登录正确性来自显式 `tenantId` 重载和工厂异步创建，而不是预加载。

## 二、登录时的调用链

登录入口是：

```csharp
public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent)
```

典型调用顺序如下：

```text
POST /api/User/login
  -> UserService.LoginAsync
     -> ValidateTenantAsync(domain)
        -> _tenantRepository.QueryAsync()                         // Global 库
     -> _repository.QueryTrackingAsync(tenant.Id)                  // 显式 tenantId 查询并追踪用户
        -> TenantDbContextFactory.GetDbContextAsync(tenantId)
           -> TenantDbConnProvider.GetConnStringAsync(tenantId)
              -> GetTenantConnectionInfoAsync(tenantId)            // 缓存 + Global 库回源
           -> CreateDbContext(connectionString, isReadonly: false)
     -> GetUserAccessibleStoresAsync(user.Id, tenant.Id)
        -> _userStoreRepository.QueryAsync(tenantId)
        -> _storeRepository.QueryAsync(tenantId)
     -> _refreshTokenService.IssueAsync(tokenInfo, ...)
     -> UnitOfWork.SaveChangesAsync(tenant.Id)                     // 显式 tenantId 保存用户状态
        -> TenantDbContextFactory.GetDbContextAsync(tenantId)
           -> TenantDbConnProvider.GetConnStringAsync(tenantId)
           -> CreateDbContext(connectionString, isReadonly: false)
     -> LogLoginSuccessAsync(..., tenant.Id, ...)
        -> _userLoginLogRepository.AddAsync(loginLog, tenantId)
        -> UnitOfWork.SaveChangesAsync(tenantId)
```

关键点是：登录前半段所有依赖租户库的查询都使用 `tenant.Id` 显式重载；登录后半段所有写入和提交也使用 `tenant.Id` 显式重载。这避免了“还没登录却要从当前登录态取租户”的循环依赖。

## 三、为什么登录必须用显式 `tenantId` 重载

普通业务请求的租户上下文来自 AccessToken：

```text
AccessToken -> Claims -> CurrentIdentity -> TenantDbConnProvider.TenantId
```

登录请求的顺序则相反：

```text
domain -> Global.Tenants -> tenant.Id -> 租户库 Users -> TokenInfo -> AccessToken
```

因此，登录阶段不能调用这些依赖当前身份的方法：

```csharp
await _repository.QueryAsync();
await UnitOfWork.SaveChangesAsync();
await _dbFactory.GetDbContextAsync();
await _connProvider.GetConnStringAsync();
```

这些方法会尝试从 `ICurrentIdentity` / `IDataScope` 获取当前租户。登录时尚未生成 Token，当前租户为空，最终可能抛出“当前上下文中没有租户信息”或把数据范围解析成空。

登录阶段应调用显式重载：

```csharp
var userQueryBuilder = await _repository.QueryTrackingAsync(tenant.Id);
var storeQueryBuilder = await _storeRepository.QueryAsync(tenant.Id);
await _userLoginLogRepository.AddAsync(loginLog, tenant.Id);
await UnitOfWork.SaveChangesAsync(tenant.Id);
```

这样连接创建完全由传入的 `tenant.Id` 决定，不依赖当前请求是否已经认证。

## 四、连接字符串如何被异步解析

`TenantDbConnProvider` 提供两类 API：

| 场景 | 方法 | 租户来源 |
| --- | --- | --- |
| 已认证普通请求 | `GetConnStringAsync()` / `GetReadonlyConnStringAsync()` / `GetReportConnStringAsync()` | `ICurrentIdentity.TenantId` |
| 登录、系统任务、无身份上下文 | `GetConnStringAsync(long tenantId)` / `GetReadonlyConnStringAsync(long tenantId)` / `GetReportConnStringAsync(long tenantId)` | 显式参数 |

显式 `tenantId` 的解析流程如下：

1. 调用 `GetTenantConnectionInfoAsync(tenantId)`。
2. 先从缓存读取 `TenantCacheKeys.TenantDbConnection`，缓存实例值是 `tenantId`。
3. 缓存未命中时，异步访问 Global 库：
   - 查询 `Tenants`；
   - `Include(t => t.DatabaseInstance)` 加载数据库实例；
   - 根据 `Database:NetworkEnv` 和数据库类型查询服务器配置；
   - 拼出主库连接字符串；
   - 查询只读库和报表库配置；
   - 构造 `TenantConnectionInfo`。
4. 调用方根据需要取主库、只读库或报表库连接字符串。
5. 如果只读库不存在，读请求 fallback 到主库；如果报表库不存在，报表请求 fallback 到只读库，再 fallback 到主库。

这一步是异步 I/O，必须 `await`。调用方拿到的是已经解析完成的连接字符串，而不是尚未完成的 `Task<string>`。

## 五、`DbContext` 如何在拿到连接字符串后被正确创建

`TenantDbContextFactory` 是租户库 `DbContext` 的唯一创建入口。工厂做了几件事：

### 1. 先 await 连接字符串，再创建上下文

工厂不会把异步连接字符串解析塞到同步配置里，而是在异步方法中先等待结果：

```csharp
var connString = await _connProvider.GetConnStringAsync(tenantId, ct);
var context = CreateDbContext(connString, isReadonly: false);
```

只读库也是同样逻辑：

```csharp
var connString = await _connProvider.GetReadonlyConnStringAsync(tenantId, ct);
var context = CreateDbContext(connString, isReadonly: true);
```

也就是说，`CreateDbContext` 接收到的已经是确定的字符串，而不是异步占位。

### 2. EF Core 选项同步创建，但输入已经准备好

`CreateDbContext` 本身是同步方法，这是合理的：EF Core 的 `DbContextOptionsBuilder` 只是用已经解析好的连接字符串配置 provider。

```csharp
optionsBuilder.UseMySql(
    connectionString,
    ServerVersion.AutoDetect(connectionString),
    mysqlOptions => mysqlOptions.EnableRetryOnFailure(...));
```

这里不会再回头等待租户解析。要注意的是，`ServerVersion.AutoDetect(connectionString)` 可能打开数据库连接探测版本，因此必须保证传入的连接字符串已经完整、正确，并且数据库可达。

### 3. 主库上下文和只读上下文行为不同

- 主库上下文：用于写入和追踪实体，会挂载 `AuditInterceptor`。
- 只读/报表上下文：设置 `QueryTrackingBehavior.NoTracking`，避免不必要的追踪。

登录流程中：

- 查询并更新用户登录状态：走显式 `tenantId` 的主库追踪上下文。
- 查询门店列表等只读数据：通常走显式 `tenantId` 的只读上下文。
- 写登录日志、审计日志：走显式 `tenantId` 的主库上下文。

## 六、如何保证同一请求内连接可复用且不乱租户

`TenantDbContextFactory` 在一个 DI Scope 内缓存上下文：

| 缓存字段 | 用途 |
| --- | --- |
| `_cachedDbContext` | 当前身份租户的主库上下文 |
| `_cachedReadonlyDbContext` | 当前身份租户的只读上下文 |
| `_cachedReportDbContext` | 当前身份租户的报表上下文 |
| `_explicitTenantContexts[tenantId]` | 显式租户主库上下文 |
| `_explicitReadonlyTenantContexts[tenantId]` | 显式租户只读上下文 |
| `_explicitReportTenantContexts[tenantId]` | 显式租户报表上下文 |

登录使用的是显式租户字典。这样即便登录流程内部多次查询用户、门店、登录日志，也会按 `tenantId` 复用对应上下文，不会因为当前身份为空而混到别的租户。

同时，仓储写入时还会校验实体上的 `TenantId`：

- 如果实体实现 `ITenantEntity` 且 `TenantId == 0`，显式重载会自动把它设置为传入的 `tenantId`。
- 如果实体已有 `TenantId` 但与传入的 `tenantId` 不一致，会抛出异常。

这让“连接到了 A 租户库，却写入 B 租户 ID 数据”的错误更早暴露。

## 七、如何避免异步同步问题

过去容易出问题的写法通常是以下几类。

### 1. 不要同步等待异步连接字符串

错误示例：

```csharp
var connString = _connProvider.GetConnStringAsync(tenantId).Result;
```

或：

```csharp
var connString = _connProvider.GetConnStringAsync(tenantId).GetAwaiter().GetResult();
```

风险：

- 在线程池压力高时可能造成线程饥饿；
- 如果上层存在同步上下文，可能形成死锁；
- 异常会被包装或调用栈变差，排查困难；
- 取消令牌无法自然向下传递。

正确示例：

```csharp
var connString = await _connProvider.GetConnStringAsync(tenantId, ct);
var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
```

### 2. 不要在 `AddDbContext` 的同步 options 回调中做异步租户解析

错误思路：

```csharp
services.AddDbContext<AtlasTenantDbContext>(options =>
{
    var connString = connProvider.GetConnStringAsync().Result;
    options.UseMySql(connString, ServerVersion.AutoDetect(connString));
});
```

`AddDbContext` 的 options 配置回调是同步的，不适合做异步 I/O。Atlas 当前采用的是“注册工厂，运行时异步创建上下文”的方案，即所有租户库上下文都通过 `ITenantDbContextFactory` 获取。

### 3. 不要 fire-and-forget 预加载后马上假设连接已准备好

错误示例：

```csharp
_ = connProvider.GetConnStringAsync(tenantId);
var db = await _dbFactory.GetDbContextAsync(tenantId);
```

预加载只能提升缓存命中率，不能作为依赖条件。需要连接时必须 `await _dbFactory.GetDbContextAsync(...)`，让工厂自己保证“解析连接字符串 -> 创建上下文”的顺序。

### 4. 不要并发使用同一个 `DbContext`

工厂的锁只保护上下文初始化，不代表 EF Core `DbContext` 可以被多个并发任务同时使用。以下写法不安全：

```csharp
var db = await _dbFactory.GetDbContextAsync(tenantId);
await Task.WhenAll(
    db.Users.FirstOrDefaultAsync(...),
    db.Stores.ToListAsync());
```

如果确实需要并行数据库操作，应使用独立的 DI Scope / 独立工厂实例 / 独立 `DbContext`，或改成顺序 await。

## 八、登录写入为什么能正常保存

登录中既有只读查询，也有写入保存。需要注意主库和只读库上下文是分开的：

1. `_repository.QueryTrackingAsync(tenant.Id)` 使用显式租户主库上下文，返回的用户实体会被该上下文追踪。
2. 修改用户失败次数、最后登录时间等状态后，`UnitOfWork.SaveChangesAsync(tenant.Id)` 会通过同一个工厂取回相同的显式租户主库上下文，并调用 `SaveChangesAsync`。
3. `LogLoginSuccessAsync` / `LogLoginFailureAsync` 使用 `_userLoginLogRepository.AddAsync(loginLog, tenantId)` 把登录日志加入同一个显式租户主库上下文，再调用 `UnitOfWork.SaveChangesAsync(tenantId)` 提交。
4. 门店列表等只读数据仍可以通过 `QueryAsync(tenantId)` 走只读上下文，避免不必要的追踪。

如果未来调整登录用户查询为只读 `AsNoTracking` 后又要修改同一个用户实体，需要特别检查实体追踪关系。可选做法包括：

- 使用 `QueryTrackingAsync(tenantId)` 查询后再修改；
- 或在写入前显式 attach/update；
- 或提供专门的仓储方法，在主库上下文中按主键加载并更新登录状态。

## 九、预加载中间件的角色

`TenantConnectionPreloadMiddleware` 只在 `connProvider.TenantId != null` 时运行预加载逻辑。它会并行预热：

```csharp
var masterTask = connProvider.GetConnStringAsync(ct);
var readonlyTask = connProvider.GetReadonlyConnStringAsync(ct);
await Task.WhenAll(masterTask, readonlyTask);
```

然后在有门店上下文时预热数据范围：

```csharp
if (dataScope?.StoreId.HasValue == true)
{
    await dataScope.ResolveAsync(ct);
}
```

它的设计原则是：

- 预加载失败只记录 warning，请求继续执行；
- 后续真实查询仍会自己异步解析所需连接；
- 因此预加载不影响登录正确性，也不应该被当作登录前置步骤。

## 十、排查清单

如果再次遇到“异步同步”或“租户连接创建失败”的问题，可以按下面顺序检查：

1. **调用链是否全程 await**：确认没有 `.Result`、`.Wait()`、`GetAwaiter().GetResult()`。
2. **登录路径是否使用显式 `tenantId`**：登录前没有 Token，不能调用依赖 `ICurrentIdentity` 的无参重载。
3. **写入是否使用主库上下文**：新增、更新、登录日志、审计日志必须走 `GetDbContextAsync(tenantId)` 和 `SaveChangesAsync(tenantId)`。
4. **是否把同一个 `DbContext` 并发使用**：EF Core 上下文不是线程安全对象。
5. **缓存是否隐藏了配置错误**：如果连接字符串配置改过，注意清理 `TenantDbConnection` 和 `DatabaseServerConfig` 相关缓存。
6. **只读库 fallback 是否符合预期**：没有只读库时会 fallback 主库；如果错误连接到主库，需要确认 Global 库中只读服务器配置是否存在且 `IsReport` 标记正确。
7. **`Database:NetworkEnv` 是否正确**：网络环境不匹配时会 fallback default；如果 default 也没有配置，会返回空字符串并抛出未找到服务器配置。
8. **`ServerVersion.AutoDetect` 是否能连接数据库**：该步骤可能主动连接数据库探测版本，数据库不可达时会在创建上下文阶段暴露。

## 十一、推荐开发规则

1. 新增登录、刷新 Token、后台任务、迁移任务等无用户身份的租户库访问时，优先设计成显式 `tenantId` API。
2. 所有租户库访问都通过 `ITenantDbContextFactory` 或仓储，不要手写 `new AtlasTenantDbContext(...)`。
3. 所有数据库 I/O 都保留 async/await，不要为了适配同步接口而阻塞等待。
4. 只把预加载当性能优化，不把它当正确性保证。
5. 涉及写入时，确认实体 `TenantId` 和显式 `tenantId` 一致，避免跨租户污染。
6. 如果需要并行数据库读取，优先创建独立上下文，不共享同一个 `DbContext`。
