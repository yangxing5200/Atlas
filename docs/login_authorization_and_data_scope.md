# 登录授权与数据范围隔离说明

本文说明 Atlas 当前登录授权、门店切换、Token 鉴权、直营共享、集团共享、门店独享数据隔离的运行方式。

## 一、本地运行方式

### 1. 初始化本地 MySQL

本地 MySQL 连接约定：

```text
Host: localhost
Port: 3306
User: root
Password: root
```

执行初始化：

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj
```

该工具会重建两个库：

```text
atlas_global
atlas
```

初始化后的演示账号：

| 用户名 | 密码 | 默认门店 | 授权门店 |
| --- | --- | --- | --- |
| `hq_admin` | `Pass1234!` | `110001` 总部 | 总部、直营一店、直营二店、加盟一店 |
| `direct_a_mgr` | `Pass1234!` | `110011` 直营一店 | 直营一店 |
| `franchise_mgr` | `Pass1234!` | `110101` 加盟一店 | 加盟一店 |

### 2. 启动 WebApi

```powershell
dotnet run --project samples\Atlas.Sample.WebApi\Atlas.Sample.WebApi.csproj --urls http://localhost:5212
```

Swagger 地址：

```text
http://localhost:5212/swagger/index.html
```

## 二、核心设计结论

当前设计把用户和门店上下文拆开：

| 概念 | 含义 | 数据来源 |
| --- | --- | --- |
| 用户 | 集团级用户主体 | `Users` |
| 可操作门店 | 用户被授权可以操作哪些门店 | `UserStores` |
| 当前操作门店 | 本次请求以哪个门店身份执行 | Token 中的 `StoreId` |
| 数据可见范围 | 当前门店能看到哪些数据 | `DataScope` 根据 `StoreId` 和 `Store.Type` 计算 |

所以，用户不是某个门店的附属账号。只要在 `UserStores` 里配置了用户可操作某门店，用户就可以切换到该门店。

切换门店会返回新 Token，因为当前操作门店写在 Token 里。后续请求必须使用新 Token，底层查询框架才能按新门店计算数据范围。

## 三、登录流程

入口：

```text
POST /api/User/login
```

请求体：

```json
{
  "domain": "demo",
  "userName": "hq_admin",
  "password": "Pass1234!",
  "rememberMe": false
}
```

登录时不需要传 `storeId`。用户第一次登录时通常还没有选择门店，因此系统会自动确定初始门店。

服务端流程在 `src/Atlas.Services/UserService.cs`：

1. 根据 `domain` 查询 Global 库的 `Tenants`，确认租户存在且状态可用。
2. 使用显式 `tenantId` 查询租户库中的 `Users`。此时还没有 Token，所以不能依赖 `ICurrentIdentity.TenantId`。
3. 校验用户状态、锁定状态、密码。
4. 查询 `UserStores` 和 `Stores`，得到用户可操作门店列表。
5. 自动确定登录门店：
   - 优先 `UserStore.IsPrimary = true` 的门店。
   - 其次用户 `DefaultStoreId`。
   - 最后使用第一个可操作门店。
6. 生成 Token，Token 中写入 `UserId`、`TenantId`、`StoreId`、`SessionId`、`TokenVersion`、过期时间。
7. 写入登录日志，返回用户信息、当前门店和可切换门店列表。

响应关键字段：

```json
{
  "success": true,
  "token": "...",
  "currentStore": {
    "id": "110001",
    "name": "总部",
    "typeName": "Headquarters"
  },
  "accessibleStores": [
    { "id": "110001", "name": "总部" },
    { "id": "110011", "name": "直营一店" },
    { "id": "110012", "name": "直营二店" },
    { "id": "110101", "name": "加盟一店" }
  ]
}
```

## 四、门店切换流程

入口：

```text
POST /api/User/switch-store
Authorization: Bearer {旧Token}
```

请求体：

```json
{
  "storeId": 110101
}
```

服务端流程：

1. 从旧 Token 解析当前用户 `UserId`。
2. 查询用户信息。
3. 查询 `UserStores`，确认目标门店在该用户授权范围内。
4. 生成一个新的 Token，新 Token 的 `StoreId` 等于目标门店。
5. 记录切换门店操作日志。
6. 将旧 Token 的 `SessionId` 加入失效黑名单。
7. 返回新 Token 和新的当前门店。

关键点：

```text
切换门店不是修改用户归属，而是更换当前操作上下文。
前端收到 switch-store 返回值后，必须替换本地保存的 token。
```

旧 Token 被拉黑后，再使用旧 Token 请求接口会得到 401。这样可以避免同一个会话继续拿旧门店上下文查询数据。

## 五、Token 鉴权机制

### 1. Token 内容

Token 由 `CustomTokenService` 生成，格式是：

```text
版本号.时间戳.加密后的TokenInfo.签名
```

`TokenInfo` 包含：

| 字段 | 说明 |
| --- | --- |
| `UserId` | 当前用户 |
| `UserName` | 用户名 |
| `TenantId` | 当前租户 |
| `StoreId` | 当前操作门店 |
| `ExpiresAt` | 过期时间 |
| `SessionId` | 会话 ID |
| `TokenVersion` | 用户 Token 版本 |

加密后的 payload 用 `ICryptoService` 处理，签名使用 HMAC-SHA256。

### 2. 请求鉴权

认证处理器是 `CustomTokenAuthenticationHandler`。它按以下顺序取 Token：

1. `Authorization: Bearer {token}`
2. Cookie：`atlas-auth-token`
3. QueryString：`access_token`
4. Header：`X-Access-Token`

校验通过后写入 Claims：

| Claim | 来源 |
| --- | --- |
| `uid` | `UserId` |
| `tid` | `TenantId` |
| `sid` | `StoreId` |
| `uname` | `UserName` |
| `session_id` | `SessionId` |
| `token_version` | `TokenVersion` |

`CurrentIdentity` 再从 Claims 中读取当前用户上下文，供服务层、数据层、审计拦截器使用。

### 3. Token 失效控制

当前有两类失效机制：

| 机制 | 作用 | 使用场景 |
| --- | --- | --- |
| Session 黑名单 | 只让某个 token/session 失效 | 退出登录、切换门店 |
| TokenVersion | 让某个用户所有旧 token 失效 | 修改密码、重置密码、强制下线、重新分配门店 |

门店授权变更时，`AssignStoresAsync` 会递增目标用户 `TokenVersion`，并更新缓存。这样用户旧 Token 会失效，必须重新登录或重新获取 Token，避免继续使用已经被撤销的门店权限。

## 六、数据范围如何计算

核心服务是 `DataScope`：

```text
src/Atlas.Data.Tenant/DataScope.cs
```

`DataScope` 从 `ICurrentIdentity` 读取：

```text
TenantId
StoreId
```

然后根据当前 `StoreId` 查询 `Stores`，计算 `ShareStoreIds`。

规则如下：

| 当前门店类型 | 共享数据可见门店 |
| --- | --- |
| `Headquarters` | 当前总部 + 所有下级直营店 |
| `FranchiseHeadquarters` | 当前加盟总部 + 所有下级直营店 |
| `DirectOperated` | 父总部 + 同父级所有直营店 |
| `Franchised` | 当前加盟店自己 |
| 未知类型 | 当前门店自己 |

例如本地种子数据：

| 当前门店 | 类型 | `ShareStoreIds` |
| --- | --- | --- |
| `110001` 总部 | `Headquarters` | `110001, 110011, 110012` |
| `110011` 直营一店 | `DirectOperated` | `110001, 110011, 110012` |
| `110101` 加盟一店 | `Franchised` | `110101` |

`DataScope.ResolveAsync()` 会返回一个快照：

```csharp
public sealed record DataScopeSnapshot(
    long? TenantId,
    long? StoreId,
    IReadOnlyList<long> ShareStoreIds);
```

这个快照会被仓储层用于构造查询表达式。

## 七、查询隔离是否已经由底层框架完成

结论：对通过标准仓储入口发起的查询，底层框架已经做了隔离。

标准入口包括：

```csharp
await repository.QueryAsync();
await repository.QueryTrackingAsync();
await db.ScopedSet<TEntity>(scope);
```

核心链路：

```text
Controller/Service
  -> IRepository<TEntity>.QueryAsync()
  -> RepositoryBase.QueryAsync()
  -> IDataScope.ResolveAsync()
  -> AtlasTenantDbContext.ScopedSet<TEntity>(scope)
  -> EntityScopeFilter<TEntity>.Apply(...)
  -> EF Core Where 表达式
```

也就是说，业务代码通常不需要每次手写：

```sql
WHERE TenantId = ...
WHERE StoreId = ...
WHERE StoreId IN (...)
```

这些由 `EntityScopeFilter` 根据实体实现的接口自动加上。

需要注意的边界：

| 情况 | 是否自动隔离 |
| --- | --- |
| 使用 `RepositoryBase.QueryAsync()` | 是 |
| 使用 `RepositoryBase.QueryTrackingAsync()` | 是 |
| 使用 `db.ScopedSet<TEntity>(scope)` | 是 |
| 直接使用 `db.Set<TEntity>()` | 否，业务/API 层已由 Analyzer 阻断 |
| 原生 SQL / `FromSqlRaw` | 否，租户库写 SQL 必须走 `ITenantSqlExecutor` |
| 实体没有实现任何范围接口 | 不做租户/门店隔离 |

## 八、表类型如何表达

数据隔离不是靠表名判断，而是靠实体实现的接口判断。

### 1. 集团共享表

集团共享，指租户内所有门店都可见的数据。

实现方式：实体只实现 `ITenantEntity`，不要实现 `ISharedEntity` 或 `IStoreOnlyEntity`。

过滤规则：

```sql
WHERE TenantId = 当前TenantId
```

适合：

| 表/实体 | 说明 |
| --- | --- |
| `Store` | 门店基础资料 |
| `User` | 集团级用户 |
| `UserStore` | 用户可操作门店授权 |
| `UserRole` | 用户角色关系 |
| `OperationLog` | 租户级操作日志，业务层可再按门店筛选 |

当前框架已经支持这种模式，因为 `EntityScopeFilter` 对 `ITenantEntity` 会自动加租户过滤，不会加门店过滤。

注意：如果某张集团共享表既需要 `StoreId` 作为创建来源，又希望全集团可见，当前没有单独的 `IGroupSharedEntity` 标记。此时不要直接继承 `SharedEntity` 或 `StoreOnlyEntity`，否则会被当成直营共享或门店独享。后续可以新增一个明确的 `IGroupSharedEntity` 来表达这种情况。

### 2. 直营共享表

直营共享，指总部和直营门店之间按组织体系共享的数据。

实现方式：实体实现 `ISharedEntity`，通常继承：

```csharp
SharedEntity
SharedVersionedEntity
```

过滤规则：

```sql
WHERE TenantId = 当前TenantId
  AND StoreId IN (ShareStoreIds)
```

适合：

| 表/实体 | 当前示例 |
| --- | --- |
| 商品 | `Product : SharedVersionedEntity` |
| 会员 | `Member : SharedVersionedEntity` |
| 促销 | `Promotion : SharedVersionedEntity` |

可见性示例：

| 当前门店 | 可见商品 |
| --- | --- |
| 总部 `110001` | 总部商品 + 直营一店商品 + 直营二店商品 |
| 直营一店 `110011` | 总部商品 + 直营一店商品 + 直营二店商品 |
| 加盟一店 `110101` | 加盟一店商品 |

加盟店虽然也是 `ISharedEntity` 查询路径，但它的 `ShareStoreIds` 只有自己，所以效果是加盟店独享。

### 3. 门店独有表

门店独有，指无论总部还是直营店，都只能看当前操作门店自己的数据。

实现方式：实体实现 `IStoreOnlyEntity`，通常继承：

```csharp
StoreOnlyEntity
StoreOnlyVersionedEntity
```

过滤规则：

```sql
WHERE TenantId = 当前TenantId
  AND StoreId = 当前StoreId
```

适合：

| 表/实体 | 当前示例 |
| --- | --- |
| 订单 | `Order : StoreOnlyVersionedEntity` |
| 库存 | `Inventory : StoreOnlyEntity` |
| 收银记录 | `CashierRecord : StoreOnlyEntity` |

可见性示例：

| 当前门店 | 可见库存 |
| --- | --- |
| 总部 `110001` | 总部库存 |
| 直营一店 `110011` | 直营一店库存 |
| 加盟一店 `110101` | 加盟一店库存 |

即使总部能看到直营共享商品，也不会自动看到直营门店库存。商品和库存属于不同数据分类。

## 九、写入时如何保证 TenantId 和 StoreId

查询隔离解决的是读取问题。写入时，当前框架通过 `AuditInterceptor` 做默认填充：

| 实体接口 | 新增时自动填充 |
| --- | --- |
| `ITenantEntity` | `TenantId = 当前Token.TenantId` |
| `IStoreEntity` | `StoreId = 当前Token.StoreId` |
| `IAuditable` | `CreatedBy = 当前UserId` |
| `IBaseEntity` | `CreatedAt = 当前时间` |
| `ISnowflakeId` | 自动生成雪花 ID |

因此，业务接口在某个门店上下文下创建 `Product`、`Inventory` 等数据时，默认会落到当前 Token 的 `StoreId`。

如果是登录、租户初始化这类没有 Token 的场景，需要使用显式 `tenantId` 的仓储方法：

```csharp
repository.QueryAsync(tenantId)
repository.AddAsync(entity, tenantId)
unitOfWork.SaveChangesAsync(tenantId)
```

## 十、直营共享如何实现

以商品 `Product` 为例：

```csharp
public class Product : SharedVersionedEntity, ISnowflakeId
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public long? SourceStoreId { get; set; }
    public bool IsCustomized { get; set; }
}
```

因为 `Product` 继承了 `SharedVersionedEntity`，所以它实现了 `ISharedEntity`。

查询商品时，仓储层会自动变成类似逻辑：

```sql
SELECT *
FROM Products
WHERE TenantId = 当前TenantId
  AND StoreId IN (当前门店的ShareStoreIds)
```

当当前门店是总部 `110001`：

```text
ShareStoreIds = [110001, 110011, 110012]
```

所以总部能看到总部商品和直营店商品。

当当前门店是加盟一店 `110101`：

```text
ShareStoreIds = [110101]
```

所以加盟店只能看到自己的商品。

## 十一、集团级用户和门店上下文的关系

集团级用户的授权模型是：

```text
Users
  1:N
UserStores
  N:1
Stores
```

`UserStores` 只决定用户可切换哪些门店，不直接决定本次查询的数据范围。

本次查询的数据范围由 Token 中的 `StoreId` 决定：

```text
登录或切换门店
  -> 生成包含 StoreId 的 Token
  -> 请求时解析 StoreId 到 CurrentIdentity
  -> DataScope 根据 StoreId 计算 ShareStoreIds
  -> Repository 自动加查询过滤
```

这样做的好处是：

1. 用户可以是集团级的，不被固定到一个门店。
2. 前端有明确的当前门店上下文。
3. 数据层不需要知道用户授权了多少门店，只需要知道当前操作门店。
4. 权限变更后可以通过 TokenVersion 让旧 token 失效。

## 十二、本地验证方式

### 1. 登录总部账号

```http
POST http://localhost:5212/api/User/login
Content-Type: application/json
```

```json
{
  "domain": "demo",
  "userName": "hq_admin",
  "password": "Pass1234!",
  "rememberMe": false
}
```

预期：

```text
currentStore = 110001 总部
accessibleStores = 110001, 110011, 110012, 110101
```

### 2. 查看总部上下文的数据范围

```http
GET http://localhost:5212/api/scope-demo/visibility
Authorization: Bearer {登录返回Token}
```

预期：

```text
currentStore = 110001
resolvedSharedStoreIds = 110001, 110011, 110012
visibleSharedProducts = 3
visibleStoreOnlyInventories = 1
```

### 3. 切换到加盟一店

```http
POST http://localhost:5212/api/User/switch-store
Authorization: Bearer {登录返回Token}
Content-Type: application/json
```

```json
{
  "storeId": 110101
}
```

预期：

```text
success = true
currentStore = 110101 加盟一店
返回新的 token
旧 token 失效
```

### 4. 使用新 Token 查看加盟店数据范围

```http
GET http://localhost:5212/api/scope-demo/visibility
Authorization: Bearer {切换门店返回的新Token}
```

预期：

```text
currentStore = 110101
resolvedSharedStoreIds = 110101
visibleSharedProducts = 1
visibleStoreOnlyInventories = 1
```

## 十三、当前实现边界

1. 数据隔离依赖实体接口。新表必须正确选择 `ITenantEntity`、`ISharedEntity`、`IStoreOnlyEntity`。
2. 直接 `db.Set<TEntity>()` 查询不会自动套范围，业务/API 层不得使用，基础设施代码必须显式限定 `TenantId`。
3. 原生 SQL 不会自动隔离，租户库写 SQL 必须通过 `ITenantSqlExecutor` 并包含 `TenantId` 条件。
4. 当前没有独立的 `IGroupSharedEntity`。集团共享表建议只实现 `ITenantEntity`。如果业务需要“有 StoreId 归属但全集团可见”的表，建议新增专门标记接口。
5. `DataScope` 计算的是当前门店的业务共享范围，不是用户所有授权门店范围。用户所有授权门店只用于门店选择和切换。

## 十四、代码位置索引

| 主题 | 文件 |
| --- | --- |
| 登录、切换门店、用户门店授权 | `src/Atlas.Services/UserService.cs` |
| 登录请求模型 | `src/Atlas.Models.Tenant/Requests/CreateUserRequest.cs` |
| 切换门店请求模型 | `src/Atlas.Models.Tenant/Requests/SwitchStoreRequest.cs` |
| Token 数据结构 | `src/Atlas.Infrastructure.Security/TokenInfo.cs` |
| Token 生成和验证 | `src/Atlas.Infrastructure.Security/CustomTokenService.cs` |
| Token 认证处理器 | `src/Atlas.Infrastructure.Security/CustomTokenAuthenticationHandler.cs` |
| 当前身份解析 | `src/Atlas.Data.Tenant/Identity/CurrentIdentity.cs` |
| 数据范围服务 | `src/Atlas.Data.Tenant/DataScope.cs` |
| 范围过滤器 | `src/Atlas.Data.Tenant/EntityScopeFilter.cs` |
| 仓储统一查询入口 | `src/Atlas.Data.Tenant/Repositories/RepositoryBase.cs` |
| ScopedSet 入口 | `src/Atlas.Data.Tenant/Context/AtlasTenantDbContext.cs` |
| SQL 安全入口 | `src/Atlas.Data.Tenant/Sql/TenantSqlExecutor.cs` |
| 实体范围接口 | `src/Atlas.Core/Entities/Interfaces/IEntity.cs` |
| 共享/独享实体基类 | `src/Atlas.Core/Entities/Base/BaseEntity.cs` |
| 本地数据初始化 | `tools/Atlas.LocalSetup/Program.cs` |
| 数据范围演示接口 | `samples/Atlas.Sample.WebApi/Controllers/ScopeDemoController.cs` |
