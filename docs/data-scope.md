# Data Scope 应用说明

本文说明 Atlas 中数据权限（data scope）的应用方式：如何声明数据资源、如何把权限和数据范围授权给角色，以及业务代码如何使用 data scope 查询或判断单条数据是否可访问。

相关的登录、门店切换、底层租户隔离说明见 `docs/login_authorization_and_data_scope.md`。本文聚焦权限目录和业务使用方式。

## 核心概念

Atlas 的数据权限由四层共同完成：

| 层级 | 作用 | 示例 |
| --- | --- | --- |
| Package | 租户购买或启用的一组能力 | `atlas.standard` |
| Capability | 功能能力分组 | `product.catalog`, `inventory.stock` |
| Permission | API 或业务动作权限 | `product.read`, `inventory.read` |
| DataScope | 该权限能访问的数据范围 | `CurrentStore`, `SharedStores`, `AllTenant` |

需要区分两个概念：

| 概念 | 说明 |
| --- | --- |
| `DataScope` 服务 | 根据当前 Token 的 `TenantId`、`StoreId` 计算当前门店的共享门店集合，例如 `ShareStoreIds`。 |
| `RolePermission.DataScopeType` | RBAC 授权结果，表示某个角色拥有某个 permission 时允许使用的数据范围。 |

也就是说，`DataScope` 负责算“当前门店天然能看到哪些门店”，`RolePermission.DataScopeType` 负责限制“这个权限最多能用哪种范围查询”。

## 执行链路

一次带 data scope 的请求通常按下面的顺序执行：

```text
HTTP Request
  -> CustomTokenAuthenticationHandler 解析 TenantId/UserId/StoreId
  -> [Authorize(Policy = "Permission:xxx")] 校验 permission
  -> IAtlasAuthorizationContextService 读取运行时 permission + dataScope
  -> IDataScope.ResolveAsync() 计算当前门店 ShareStoreIds
  -> IRepository.QueryDataScopeAsync(resourceCode, scopeType)
  -> AtlasDataScopePredicateBuilder 生成 data scope 谓词
  -> EF Core 查询返回已过滤数据
```

如果是单条资源访问判断，则查询阶段可以替换成：

```text
IAtlasDataAccessEvaluator.CanAccessAsync(resource, context)
```

这条链路的重点是：permission 只负责“能不能进入这个业务动作”，data scope 负责“进入后能看到哪些数据”。

## 业务场景对照

| 场景 | 实体类型 | Data resource | Permission | 推荐 DataScope | 说明 |
| --- | --- | --- | --- | --- | --- |
| 商品目录 | `Product : SharedVersionedEntity` | `product` | `product.read` | `SharedStores` | 总部和直营店共享商品；加盟店因为 `ShareStoreIds` 只有自己，天然隔离。 |
| 商品目录只看当前门店 | `Product : SharedVersionedEntity` | `product` | `product.read` | `CurrentStore` | 同一权限可以主动收窄范围，只看当前门店商品。 |
| 库存 | `Inventory : StoreOnlyEntity` | `inventory` | `inventory.read` | `CurrentStore` | 库存是门店独享数据，总部也只能看总部库存。 |
| 订单 | `Order : StoreOnlyVersionedEntity` | `order` | `order.read` | `CurrentStore` | 订单通常不应被直营共享商品范围影响。 |
| 租户后台配置 | 只实现 `ITenantEntity` | `tenant-setting` | `tenant-setting.read` | `AllTenant` | 只按 `TenantId` 隔离，不按门店隔离。 |
| 自己创建的数据 | 实体有 `OwnerId` | `customer-note` | `note.read` | `Own` | 需要在 data resource 上配置 `ownerField`。 |

这张表表达的是设计建议。实际能不能使用某个 scope，最终取决于 data resource 的 `supportedScopes` 和角色权限的 `DataScopeType`。

## 1. 声明权限目录

业务模块在 `ConfigureAuthorization` 中声明 package、capability、permission 和 data resource。

示例位置：

```text
samples/Atlas.Sample.ECommerce/SampleECommerceModule.cs
```

商品读取权限：

```csharp
builder
    .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
    .AddCapability("product.catalog", "Product catalog", "ECommerce")
    .AddPermission(
        "product.read",
        "Read products",
        "product.catalog",
        "Product",
        PermissionScope.Store,
        resource: "product",
        action: "read")
    .AddPackageCapability("atlas.standard", "product.catalog");
```

商品数据资源：

```csharp
builder.AddDataResource(
    "product",
    "Product",
    entityType: typeof(Product).FullName,
    storeField: "StoreId",
    supportedScopes: new[]
    {
        AtlasDataScopeType.CurrentStore,
        AtlasDataScopeType.SharedStores
    });
```

库存数据资源：

```csharp
builder.AddDataResource(
    "inventory",
    "Inventory",
    entityType: typeof(Inventory).FullName,
    storeField: "StoreId",
    supportedScopes: new[] { AtlasDataScopeType.CurrentStore });
```

`supportedScopes` 很重要。即使某个用户被授予了 `AllTenant`，如果数据资源只支持 `CurrentStore`，业务代码也不能直接用 `AllTenant` 去构造该资源的 data scope 查询，必须降级到资源支持的范围。

### 字段映射说明

`AddDataResource` 中的字段决定谓词构造方式：

| 字段 | 作用 |
| --- | --- |
| `tenantField` | 租户字段名，默认 `TenantId`。 |
| `storeField` | 门店字段名，`CurrentStore`、`SharedStores`、`AssignedStores` 会使用它。 |
| `ownerField` | 所有人字段名，`Own` 会使用它。 |
| `supportedScopes` | 当前资源允许使用的数据范围集合。 |

例如一个“客户备注”资源，只允许读取自己创建的备注：

```csharp
builder.AddDataResource(
    "customer-note",
    "Customer note",
    entityType: typeof(CustomerNote).FullName,
    storeField: "StoreId",
    ownerField: "CreatedBy",
    supportedScopes: new[]
    {
        AtlasDataScopeType.CurrentStore,
        AtlasDataScopeType.Own
    });
```

如果后续给角色授予 `note.read=Own`，`IAtlasDataAccessEvaluator` 会使用 `ownerField` 检查资源上的 `CreatedBy` 是否等于当前 `UserId`。

### 实体接口和 data resource 的关系

Atlas 有两层过滤，不要混淆：

| 层 | 来源 | 示例 |
| --- | --- | --- |
| 实体范围过滤 | 实体实现的接口或基类 | `Product : SharedVersionedEntity` 会先按 `ShareStoreIds` 过滤。 |
| 声明式 data scope 过滤 | `AddDataResource` + `QueryDataScopeAsync` | `product + CurrentStore` 会再收窄到当前门店。 |

例如 `Product` 是共享实体，默认仓储查询会先得到“当前门店共享范围内的商品”。如果再调用：

```csharp
await _products.QueryDataScopeAsync("product", AtlasDataScopeType.CurrentStore, ct);
```

结果会进一步收窄为“当前门店商品”。这就是为什么 `SharedStores` 权限可以安全地支持前端主动切换到 `CurrentStore` 视图。

## 2. 给租户启用 package 或 capability

租户必须先拥有 package/capability，permission 才会进入“可用权限”集合。

本地 demo seed 会给演示租户启用：

```text
atlas.core
atlas.standard
```

对应数据在 Global 库：

```text
TenantEntitlements
```

也可以通过授权管理接口授予：

```http
POST /api/admin/authorization/tenants/{tenantId}/entitlements
```

启用 package 后，系统会根据授权目录把 package 展开为 capabilities，再把 capabilities 展开为 permissions。

### Entitlement 示例

本地 seed 写入的是租户级 package entitlement，含义是整个租户启用该 package：

```text
TenantId    = 100001
SubjectType = Tenant
SubjectId   = 100001
PackageCode = atlas.standard
Status      = Active
```

也可以只启用某个 capability：

```text
TenantId       = 100001
SubjectType    = Tenant
SubjectId      = 100001
CapabilityCode = product.catalog
Status         = Active
```

启用 package 更适合套餐售卖；启用 capability 更适合临时开通、试用、灰度或补偿授权。

## 3. 给角色授予 permission 和 dataScope

角色权限行同时保存 permission 和数据范围：

```text
RolePermissions.PermissionId
RolePermissions.DataScopeType
```

本地 demo 中的示例授权：

| 用户 | Permission | DataScope | 说明 |
| --- | --- | --- | --- |
| `direct_a_mgr` | `product.read` | `SharedStores` | 直营一店可读取总部、直营一店、直营二店范围内的共享商品。 |
| `direct_a_mgr` | `inventory.read` | `CurrentStore` | 只能读取直营一店自己的库存。 |
| `franchise_mgr` | `product.read` | `SharedStores` | 但加盟店的 `ShareStoreIds` 只有自己，所以只能看到加盟店商品。 |
| `franchise_mgr` | `inventory.read` | `CurrentStore` | 只能读取加盟店自己的库存。 |

注意：permission 授权和 data scope 授权只是运行时上下文的一部分，不会自动替所有查询选择范围。业务代码需要在合适的位置使用 `QueryDataScopeAsync` 或 `IAtlasDataAccessEvaluator`。

### RolePermission 行示例

直营一店店长读取商品：

```text
Role.Code                 = demo-direct-reader
Permission.Code           = product.read
RolePermission.Effect     = Allow
RolePermission.DataScope  = SharedStores
UserRole.StoreId          = 110011
```

同一个角色读取库存：

```text
Role.Code                 = demo-direct-reader
Permission.Code           = inventory.read
RolePermission.Effect     = Allow
RolePermission.DataScope  = CurrentStore
UserRole.StoreId          = 110011
```

这表达了一个常见业务规则：商品可以在总部/直营体系内共享，库存只能看当前门店。

### DataScope 宽窄关系

业务代码允许用户请求比授权更窄的范围，但不能请求更宽的范围：

| 授权范围 | 可以请求 | 不应允许请求 |
| --- | --- | --- |
| `AllTenant` | `AllTenant`, `SharedStores`, `CurrentStore` | 无，前提是资源支持。 |
| `SharedStores` | `SharedStores`, `CurrentStore` | `AllTenant` |
| `CurrentStore` | `CurrentStore` | `SharedStores`, `AllTenant` |
| `Own` | `Own` | `CurrentStore`, `SharedStores`, `AllTenant` |

例如 `direct_a_mgr` 被授予 `product.read=SharedStores`，所以可以调用：

```http
GET /api/scope-demo/products
GET /api/scope-demo/products?scopeType=CurrentStore
```

但不应该允许调用：

```http
GET /api/scope-demo/products?scopeType=AllTenant
```

因为这会比角色授权更宽。

## 4. API 入口先声明 permission 策略

Controller action 应先用 permission policy 控制入口访问：

```csharp
[Authorize(Policy = AuthorizationPolicies.PermissionPrefix + "product.read")]
public async Task<ActionResult> GetProducts(CancellationToken ct)
{
    ...
}
```

这一步只回答“用户有没有这个 permission”。数据范围要在查询阶段继续应用。

完整一点的 Controller 例子：

```csharp
[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductController : ControllerBase
{
    private readonly IProductQueryService _queries;

    public ProductController(IProductQueryService queries)
    {
        _queries = queries;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + "product.read")]
    public Task<ProductListResponse> GetProducts(
        [FromQuery] AtlasDataScopeType? scopeType,
        CancellationToken ct)
    {
        return _queries.GetProductsAsync(scopeType, ct);
    }
}
```

这个 action 的责任是声明入口权限，真正选择和应用 data scope 应放在 query service 或 service 中。

## 5. 查询列表时应用 dataScope

列表查询使用仓储的声明式数据范围入口：

```csharp
var query = await _products.QueryDataScopeAsync(
    "product",
    AtlasDataScopeType.SharedStores,
    ct);
```

这条链路会做两类过滤：

1. 仓储默认的实体范围过滤，例如 `TenantId`、共享实体的 `ShareStoreIds`、门店独享实体的当前 `StoreId`。
2. `AtlasDataScopePredicateBuilder` 根据 `resourceCode` 和 `AtlasDataScopeType` 生成的 data scope 谓词。

商品例子：

```text
resourceCode = product
scopeType    = SharedStores
```

库存例子：

```text
resourceCode = inventory
scopeType    = CurrentStore
```

不要在业务/API 层直接使用 `db.Set<TEntity>()` 绕过仓储。项目 analyzer 会阻止这类直接访问，因为它容易漏掉租户和门店边界。

### Query service 模板

服务层通常需要三步：

1. 读取当前用户运行时授权上下文。
2. 根据 permission 授权和请求参数选择最终 scope。
3. 使用 `QueryDataScopeAsync` 查询。

示例：

```csharp
public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
    AtlasDataScopeType? requestedScope,
    CancellationToken ct)
{
    var runtime = new AtlasAuthorizationRuntimeContext(
        _currentIdentity.TenantId!.Value,
        _currentIdentity.UserId!.Value,
        _currentIdentity.StoreId);

    var auth = await _authorizationContext.GetContextAsync(runtime, ct);
    var appliedScope = ResolveScope(
        auth,
        "product.read",
        requestedScope,
        supportedScopes: new[]
        {
            AtlasDataScopeType.SharedStores,
            AtlasDataScopeType.CurrentStore
        });

    var query = await _products.QueryDataScopeAsync("product", appliedScope, ct);

    return await query
        .OrderBy(x => x.StoreId)
        .SelectToListAsync(x => new ProductDto
        {
            Id = x.Id,
            Name = x.Name,
            StoreId = x.StoreId
        }, ct);
}
```

`ResolveScope` 不一定要全局抽象，简单场景可以先写在具体 service 中。核心规则是：不能让请求的 scope 超过授权的 `RolePermission.DataScopeType`，也不能使用 data resource 不支持的 scope。

### 查询结果例子

本地 demo 中，`direct_a_mgr` 当前门店是 `110011`，`ShareStoreIds` 为：

```text
110001, 110011, 110012
```

调用：

```http
GET /api/scope-demo/products
```

等价于读取共享商品范围：

```text
TenantId = 100001
StoreId IN (110001, 110011, 110012)
```

调用：

```http
GET /api/scope-demo/products?scopeType=CurrentStore
```

等价于主动收窄到：

```text
TenantId = 100001
StoreId = 110011
```

库存查询：

```http
GET /api/scope-demo/inventories/current-store
```

只允许：

```text
TenantId = 100001
StoreId = 110011
```

即使直营一店能看直营二店商品，也不能看直营二店库存。

## 6. 单条数据访问判断

如果业务已经拿到一个资源对象，或者需要解释某条数据为什么可访问，可以使用：

```csharp
var decision = await _dataAccessEvaluator.CanAccessAsync(
    resource,
    new AtlasDataAccessContext(
        tenantId,
        userId,
        storeId,
        "product",
        AtlasDataScopeType.SharedStores,
        sharedStoreIds,
        assignedStoreIds),
    ct);
```

返回值：

```csharp
public sealed record AtlasDataAccessDecision(
    bool Allowed,
    string Reason);
```

常见结果：

| 结果 | 含义 |
| --- | --- |
| `Allowed = true` | 资源的 `TenantId`、`StoreId` 等字段满足当前 data scope。 |
| `Tenant mismatch` | 资源不属于当前租户。 |
| `Store mismatch` | `CurrentStore` 范围下资源不属于当前门店。 |
| `Store is outside the allowed data scope` | `SharedStores` 或 `AssignedStores` 范围下资源门店不在允许集合内。 |
| `Data scope ... is not supported` | 数据资源声明不支持请求的 scope。 |

### 决策例子

`direct_a_mgr` 使用 `product.read=SharedStores` 检查直营二店商品 `140012`：

```text
当前 StoreId       = 110011
SharedStoreIds     = 110001, 110011, 110012
Product.StoreId    = 110012
Requested scope    = SharedStores
Decision.Allowed   = true
Decision.Reason    = Shared store scope allowed.
```

检查加盟店商品 `140101`：

```text
当前 StoreId       = 110011
SharedStoreIds     = 110001, 110011, 110012
Product.StoreId    = 110101
Requested scope    = SharedStores
Decision.Allowed   = false
Decision.Reason    = Store is outside the allowed data scope.
```

如果同一个用户请求 `CurrentStore` 检查直营二店商品：

```text
当前 StoreId       = 110011
Product.StoreId    = 110012
Requested scope    = CurrentStore
Decision.Allowed   = false
Decision.Reason    = Store mismatch.
```

这个判断适合用在“详情页是否可看”“操作前二次校验”“诊断接口”等场景。列表页仍优先使用 `QueryDataScopeAsync`，避免先查出过多数据再内存过滤。

### Own 范围例子

如果要做“只能看自己创建的客户备注”，资源可以这样声明：

```csharp
builder.AddDataResource(
    "customer-note",
    "Customer note",
    entityType: typeof(CustomerNote).FullName,
    storeField: "StoreId",
    ownerField: "CreatedBy",
    supportedScopes: new[]
    {
        AtlasDataScopeType.Own,
        AtlasDataScopeType.CurrentStore
    });
```

授权：

```text
Permission.Code          = customer-note.read
RolePermission.ScopeType = Own
```

判断：

```text
Current UserId       = 120011
CustomerNote.CreatedBy = 120011
Decision.Allowed     = true
```

如果 `CreatedBy` 是其他用户，则 `Own` 会返回 `Owner mismatch.`。

## 7. 本地验证

初始化 demo 数据：

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- seed-demo
```

如果本地库结构已经漂移，使用：

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- reset-demo
```

启动 Sample WebApi：

```powershell
dotnet run --project samples\Atlas.Sample.WebApi\Atlas.Sample.WebApi.csproj --urls http://localhost:5212
```

登录直营一店店长：

```http
POST http://localhost:5212/api/User/login
Content-Type: application/json
```

```json
{
  "domain": "demo",
  "userName": "direct_a_mgr",
  "password": "Pass1234!",
  "rememberMe": false
}
```

查看授权上下文：

```http
GET http://localhost:5212/api/scope-demo/authorization
Authorization: Bearer {token}
```

预期重点：

```text
RuntimeStandardCapabilities 包含 product.catalog, inventory.stock
RuntimeStandardPermissions 包含 product.read, inventory.read
RuntimeStandardDataScopes 包含 product.read=SharedStores, inventory.read=CurrentStore
```

响应中可以重点看这些字段：

```json
{
  "standardPackage": {
    "code": "atlas.standard",
    "capabilityCodes": [
      "inventory.stock",
      "order.sales",
      "product.catalog"
    ],
    "permissionCodes": [
      "inventory.read",
      "order.place",
      "product.create",
      "product.delete",
      "product.read",
      "product.update"
    ],
    "dataResources": [
      {
        "code": "inventory",
        "storeField": "StoreId",
        "supportedScopes": ["CurrentStore"]
      },
      {
        "code": "product",
        "storeField": "StoreId",
        "supportedScopes": ["CurrentStore", "SharedStores"]
      }
    ]
  },
  "runtimeStandardDataScopes": [
    {
      "permissionCode": "inventory.read",
      "scopeType": "CurrentStore"
    },
    {
      "permissionCode": "product.read",
      "scopeType": "SharedStores"
    }
  ]
}
```

实际 JSON 中 enum 可能按数字或字符串呈现，取决于当前序列化配置；语义以字段名和值对应的 enum 为准。

查询共享商品：

```http
GET http://localhost:5212/api/scope-demo/products
Authorization: Bearer {token}
```

直营一店预期看到：

```text
总部商品
直营一店商品
直营二店商品
```

响应重点字段：

```json
{
  "requestedScope": null,
  "appliedScope": "SharedStores",
  "resolvedSharedStoreIds": [110001, 110011, 110012],
  "products": [
    { "id": 140001, "storeId": 110001, "name": "总部标准套餐" },
    { "id": 140011, "storeId": 110011, "name": "直营一店限定套餐" },
    { "id": 140012, "storeId": 110012, "name": "直营二店限定套餐" }
  ]
}
```

查询当前门店商品：

```http
GET http://localhost:5212/api/scope-demo/products?scopeType=CurrentStore
Authorization: Bearer {token}
```

直营一店预期只看到：

```text
直营一店商品
```

响应中的 `appliedScope` 应为：

```text
CurrentStore
```

查询库存：

```http
GET http://localhost:5212/api/scope-demo/inventories/current-store
Authorization: Bearer {token}
```

预期只看到当前门店库存。

例如直营一店只能看到：

```text
Inventory.StoreId = 110011
```

检查单个商品访问：

```http
GET http://localhost:5212/api/scope-demo/products/140012/access?scopeType=SharedStores
Authorization: Bearer {token}
```

直营一店访问直营二店商品时预期允许，因为 `product.read=SharedStores` 且直营门店共享范围包含兄弟直营店。

```http
GET http://localhost:5212/api/scope-demo/products/140101/access?scopeType=SharedStores
Authorization: Bearer {token}
```

直营一店访问加盟店商品时预期拒绝，因为加盟店不在直营共享范围内。

### 加盟店账号验证

登录加盟店店长：

```json
{
  "domain": "demo",
  "userName": "franchise_mgr",
  "password": "Pass1234!",
  "rememberMe": false
}
```

调用：

```http
GET http://localhost:5212/api/scope-demo/products
Authorization: Bearer {token}
```

预期：

```text
resolvedSharedStoreIds = 110101
products 只包含 加盟一店自有套餐
```

虽然 `franchise_mgr` 的 `product.read` 也是 `SharedStores`，但加盟店的 `ShareStoreIds` 只有自己，所以不会看到总部和直营店商品。这说明 data scope 不是简单的角色范围枚举，还会叠加当前门店组织规则。

这些请求也已经写入：

```text
samples/Atlas.Sample.WebApi/Atlas.Sample.WebApi.http
```

## 8. 常见使用规则

1. 新业务模块先声明 package/capability/permission/data resource，再写 Controller。
2. API action 使用 permission policy 控制入口。
3. 列表查询优先使用 `QueryDataScopeAsync(resourceCode, scopeType)`。
4. 单条资源判断使用 `IAtlasDataAccessEvaluator`。
5. `scopeType` 必须被 data resource 的 `supportedScopes` 支持。
6. 查询只读数据用 `QueryAsync`；查出来要修改并保存时用 `QueryTrackingAsync`。
7. 登录、初始化等没有 Token 的场景使用显式 `tenantId` overload。
8. 不要在业务/API 层直接调用 `db.Set<TEntity>()` 或手写跨租户 SQL。

## 9. 常见错误和处理

### 只有 permission，没有 entitlement

现象：

```text
用户角色里有 product.read，但 API 仍然 403。
```

原因：

```text
租户没有启用包含 product.catalog 的 package/capability。
```

检查：

```http
GET /api/scope-demo/authorization
```

或查看 Global 库 `TenantEntitlements`。

处理：

```text
给租户启用 atlas.standard，或单独启用 product.catalog。
```

### entitlement 存在，但用户没有 permission

现象：

```text
TenantEntitlements 已有 atlas.standard，但普通店长仍然 403。
```

原因：

```text
普通用户还需要角色授权 RolePermission。TenantAdmin/SystemAdmin 有兼容旁路，普通用户没有。
```

处理：

```text
给用户所在角色授予 product.read，并设置合适的 DataScopeType。
```

### 请求了资源不支持的 data scope

现象：

```text
inventory 资源请求 SharedStores 失败。
```

原因：

```text
inventory data resource 只声明支持 CurrentStore。
```

处理：

```text
要么改用 CurrentStore，要么重新评估库存是否真的应该支持共享范围。
```

### 用了 QueryAsync 但忘了 QueryDataScopeAsync

`QueryAsync` 会应用实体基础范围过滤，比如租户和门店共享规则；但它不会读取 `RolePermission.DataScopeType` 来选择权限范围。

如果某个页面需要体现“同一个资源在不同 permission dataScope 下的可见差异”，应使用：

```csharp
QueryDataScopeAsync(resourceCode, scopeType)
```

### 修改实体却使用 QueryAsync

`QueryAsync` 是 no-tracking 查询。查出来后修改实体并 `SaveChangesAsync` 不会稳定落库。

需要修改并保存时使用：

```csharp
QueryTrackingAsync()
QueryTrackingAsync(tenantId)
```

登录流程中查询用户后会更新失败次数、最后登录时间、最后登录 IP，因此使用 `QueryTrackingAsync(tenantId)`。

## 10. 代码位置

| 主题 | 文件 |
| --- | --- |
| 授权目录模型 | `src/Atlas.Core/Authorization/AtlasAuthorizationCatalog.cs` |
| 运行时授权上下文 | `src/Atlas.Core/Authorization/AtlasAuthorizationRuntimeContracts.cs` |
| 数据访问判断模型 | `src/Atlas.Core/Authorization/AtlasDataAuthorization.cs` |
| data scope 谓词构造 | `src/Atlas.Infrastructure.Security/Permissions/AtlasDataScopePredicateBuilder.cs` |
| 单条数据访问判断 | `src/Atlas.Infrastructure.Security/Permissions/AtlasDataAccessEvaluator.cs` |
| RBAC permission 检查 | `src/Atlas.Infrastructure.Security/Permissions/RbacPermissionService.cs` |
| package/capability 展开 | `src/Atlas.Services.Tenant/Runtime/Authorization/EntitlementService.cs` |
| 授权上下文服务 | `src/Atlas.Services.Tenant/Runtime/Authorization/AuthorizationRuntimeService.cs` |
| 仓储 data scope 查询 | `src/Atlas.Data.Tenant/Repositories/RepositoryBase.cs` |
| 当前门店共享范围计算 | `src/Atlas.Data.Tenant/DataScope.cs` |
| sample 授权目录 | `samples/Atlas.Sample.ECommerce/SampleECommerceModule.cs` |
| sample data scope API | `samples/Atlas.Sample.WebApi/Controllers/ScopeDemoController.cs` |
| demo seed | `tools/Atlas.LocalSetup/Program.cs` |
