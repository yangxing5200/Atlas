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
