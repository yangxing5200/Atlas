# Atlas 模块系统接入指南

本文说明 PR-07/PR-08 引入的模块接入方式。模块用于把业务能力从宿主启动代码中拆出来，由模块自行声明服务注册、Controller 程序集、MassTransit Consumer 程序集和 AutoMapper Profile 程序集。

## 显式模块

业务模块可以继承 `AtlasModule`：

```csharp
using Atlas.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

public sealed class BillingModule : AtlasModule
{
    public override void AddServices(AtlasModuleContext context)
    {
        context.Services.AddScoped<IBillingService, BillingService>();
    }
}
```

默认情况下，`AtlasModule` 会把模块类所在程序集同时声明为 Controller、Consumer 和 AutoMapper Profile 程序集。需要拆分程序集时，可以覆盖 `ControllerAssemblies`、`ConsumerAssemblies` 或 `AutoMapperAssemblies`。

## 授权目录声明

模块可以覆盖 `ConfigureAuthorization` 声明自己的产品能力、权限点、菜单、套餐片段和数据资源。框架会聚合所有模块声明并注册为 `IAtlasAuthorizationCatalog`，后续 RBAC seed、菜单过滤、权益计算和数据权限都从这个目录消费。

```csharp
public sealed class BillingModule : AtlasModule
{
    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability("billing.charge", "Charge", "Billing")
            .AddPermission(
                "billing.charge.read",
                "Read charges",
                "billing.charge",
                "Billing",
                PermissionScope.Store,
                resource: "charge",
                action: "read")
            .AddPackageCapability("atlas.standard", "billing.charge")
            .AddMenuItem(
                "billing.charges",
                "Charges",
                "/billing/charges",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission("billing.charge.read"));
    }
}
```

声明规则：

1. `Capability` 回答模块提供什么产品能力。
2. `Permission` 回答用户能执行什么操作。
3. `PackageCapability` 回答套餐包含哪些能力。
4. `MenuItem` 只声明入口展示条件，不替代后端授权。
5. `DataResource` 声明数据资源如何被框架数据权限识别。

框架已提供统一运行时入口：

1. `GET /api/auth/context` 返回当前用户有效 permissions、capabilities、featureFlags 和 dataScopes。
2. `GET /api/menus/me` 按当前授权上下文过滤菜单树。
3. `GET /api/admin/authorization/catalog` 查看聚合后的授权目录。
4. `GET /api/admin/authorization/diagnostics/users/{userId}/permissions/{permissionCode}` 解释某用户为什么有或没有某权限。
5. `GET/POST/PUT /api/admin/authorization/tenants/{tenantId}/entitlements` 管理租户/门店权益。
6. `GET/PUT /api/admin/authorization/tenants/{tenantId}/roles/{roleId}/permissions` 管理角色权限和数据范围。

查询服务需要显式使用数据权限入口，而不是在业务代码里手写门店/本人过滤：

```csharp
var builder = await repository.QueryDataScopeAsync(
    "billing.charge",
    AtlasDataScopeType.SharedStores,
    ct);
```

当 `Department` 或 `Custom` 这类领域规则无法表达成通用字段谓词时，业务模块实现 `IAtlasDataScopeContributor<TResource>` 补充目标资源级判断。

## 程序集模块

如果一个程序集暂时没有显式模块类，也可以用 `AddModuleAssembly()` 按约定接入：

```csharp
builder.Services.AddAtlasCore(
    builder.Configuration,
    modules => modules.AddModuleAssembly(typeof(OrderPlacedEventConsumer).Assembly));
```

这种方式不会注册额外业务服务，但会把该程序集加入 Consumer 和 AutoMapper Profile 扫描范围。WebApi 入口使用时也会加入 MVC ApplicationPart，用于发现模块 Controller。

## WebApi 接入

```csharp
builder.AddAtlasWebApi(
    options => options.ApiTitle = "Atlas API",
    modules => modules.AddModulesFromAssembly(typeof(BillingModule).Assembly));
```

`AddModulesFromAssembly()` 会扫描程序集内公开、非抽象、带公开无参构造函数的 `IAtlasModule` 实现。

## Worker 接入

Worker 可以通过模块程序集加载 Consumer：

```csharp
builder.Services.AddAtlasCore(
    builder.Configuration,
    modules => modules.AddModuleAssembly(typeof(OrderPlacedEventConsumer).Assembly));
```

这样 Worker 启动代码不需要直接把 Consumer 程序集传给消息注册逻辑，后续模块迁移时只需要替换为对应业务模块。
