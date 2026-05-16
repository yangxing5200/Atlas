# Atlas 缓存模块指南

本文档介绍 Atlas 项目的缓存模块设计、能力边界和集成方式。缓存模块位于 `src/Atlas.Infrastructure.Caching`，业务代码主要依赖 `ICacheService`，不直接依赖具体的内存缓存或 Redis 实现。

## 模块定位

缓存模块用于承接业务读模型、配置数据、租户连接信息、令牌版本等高频读取数据。它提供统一的缓存键定义、作用域隔离、标签失效、批量操作和多种底层缓存提供者。

核心设计目标：

- 业务层只依赖 `ICacheService`
- 所有结构化业务缓存优先使用 `CacheKeyDefinition`
- 自动把缓存键按全局、租户、门店、用户作用域隔离
- 通过标签版本实现批量逻辑失效
- 可按部署环境切换内存缓存、Redis 缓存或混合缓存

## 项目结构

```text
src/Atlas.Infrastructure.Caching/
├── Abstractions/                 # 对外接口
├── Core/                         # CacheService 和核心模型
├── Extensions/                   # 依赖注入扩展
├── Invalidation/                 # 缓存失效实现
├── Keys/                         # 缓存键生成和解析
├── Providers/
│   ├── Memory/                   # 本地内存缓存
│   ├── Redis/                    # Redis 缓存和失效通知
│   └── Hybrid/                   # 本地内存 + Redis 混合缓存
├── Scoping/                      # 当前作用域上下文
├── Serialization/                # 缓存序列化
└── Tags/                         # 标签版本管理
```

## 核心类型

### `ICacheService`

缓存服务主入口，提供同步和异步方法：

- `Get` / `Set` / `Remove` / `Exists`
- `GetAsync` / `SetAsync` / `GetOrSetAsync`
- `GetManyAsync` / `SetManyAsync` / `RemoveManyAsync`
- `InvalidateByTagAsync` / `InvalidateByTagsAsync`
- `InvalidateTenantAsync` / `InvalidateStoreAsync` / `InvalidateUserAsync`
- `GetStatisticsAsync` / `ClearAsync`

业务代码推荐使用异步方法，尤其是 `GetOrSetAsync`，可以避免重复写“先查缓存、再查数据库、再写缓存”的流程。

### `CacheKeyDefinition`

缓存键定义是业务缓存的入口。它包含缓存键模板、作用域、实例占位符、默认过期时间、标签生成器和空值缓存策略。

示例：

```csharp
public static readonly CacheKeyDefinition ProductDetail =
    CacheKeyDefinition.Create("product:detail:{id}")
        .WithScope(CacheScope.Tenant)
        .WithInstanceKey("id")
        .WithExpiration(TimeSpan.FromMinutes(30))
        .WithDescription("商品详情缓存")
        .WithTagGenerator((context, instanceValue) => new[]
        {
            $"tenant:{context.TenantId}",
            "entity:product",
            $"product:{instanceValue}"
        })
        .Build();
```

调用时传入实例值：

```csharp
var product = await cache.GetOrSetAsync(
    ProductCacheKeys.ProductDetail,
    () => productRepository.GetByIdAsync(productId, ct),
    productId,
    cancellationToken: ct);
```

### `CacheScope`

缓存作用域决定最终缓存键前缀：

| 作用域 | 用途 | 示例语义 |
| --- | --- | --- |
| `Global` | 全局共享数据 | 系统配置、租户连接信息 |
| `Tenant` | 租户内共享 | 商品列表、租户配置 |
| `Store` | 门店内共享 | 门店库存、门店经营数据 |
| `User` | 用户私有 | 用户权限、购物车、个性化数据 |

作用域值来自当前请求的身份上下文。租户、门店、用户作用域分别需要当前上下文中存在对应标识，否则缓存键生成会失败。

### 标签失效

标签不是直接存储缓存键集合，而是记录标签版本。写入缓存时会记录当前标签版本；读取时如果发现版本不一致，就视为缓存未命中并异步删除旧值。

这类设计适合“某一类数据变更后，让相关缓存整体失效”的场景：

```csharp
await cache.InvalidateByTagAsync("entity:product", ct);
await cache.InvalidateByTagsAsync(
    new[] { $"tenant:{tenantId}", $"product:{productId}" },
    ct);
```

## 支持的缓存模式

### 内存缓存

适合本地开发、单实例部署或不要求跨实例一致性的场景。

特点：

- 无外部依赖
- 延迟最低
- 不支持跨进程失效通知
- 应用重启后缓存全部丢失

### Redis 缓存

适合多实例部署或需要共享缓存的场景。

特点：

- 多实例共享缓存数据
- 标签版本可存储在 Redis
- 支持 Redis 发布订阅做分布式失效通知
- 依赖 Redis 可用性和网络稳定性

### 混合缓存

混合缓存使用本地内存作为一级缓存，Redis 作为二级缓存。读取时先查本地内存，未命中再查 Redis；从 Redis 命中后可提升到本地内存。

特点：

- 热点数据读取更快
- 多实例之间仍通过 Redis 共享数据
- 本地缓存需要依赖失效通知保持一致性
- 适合读多写少、热点明显的业务

## 集成方式

### 方式一：通过 `AddAtlasCore` 集成

主 Web 项目如果已经调用 `AddAtlasCore`，缓存模块会自动注册：

```csharp
builder.Services.AddAtlasCore(builder.Configuration);
```

配置入口在 `CacheSettings`：

```json
{
  "CacheSettings": {
    "Provider": "Memory"
  }
}
```

支持的 `Provider`：

- `Memory`
- `Redis`
- `Hybrid`

### 方式二：单独集成缓存模块

如果某个项目只需要缓存能力，可以直接引用 `Atlas.Infrastructure.Caching`，然后手动注册：

```csharp
builder.Services.AddAtlasCaching();
builder.Services.AddMemoryCaching();
```

使用 Redis：

```csharp
builder.Services.AddAtlasCaching();
builder.Services.AddRedisCaching(
    connectionString: "localhost:6379",
    instanceName: "atlas");
```

使用混合缓存：

```csharp
builder.Services.AddAtlasCaching();
builder.Services.AddHybridCaching(
    redisConnectionString: "localhost:6379",
    configureOptions: options =>
    {
        options.L1Expiration = TimeSpan.FromMinutes(5);
        options.EnableL1Promotion = true;
    });
```

## 配置示例

### 内存缓存

```json
{
  "CacheSettings": {
    "Provider": "Memory"
  }
}
```

### Redis 缓存

```json
{
  "CacheSettings": {
    "Provider": "Redis",
    "Redis": {
      "ConnectionString": "localhost:6379",
      "InstanceName": "atlas"
    }
  }
}
```

### 混合缓存

```json
{
  "CacheSettings": {
    "Provider": "Hybrid",
    "Hybrid": {
      "RedisConnectionString": "localhost:6379",
      "L1ExpirationMinutes": 5
    }
  }
}
```

## 业务使用示例

### 定义缓存键

建议按业务域集中定义缓存键，例如：

```csharp
public static class ProductCacheKeys
{
    public static readonly CacheKeyDefinition ProductDetail =
        CacheKeyDefinition.Create("product:detail:{id}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("id")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithDescription("商品详情缓存")
            .WithTagGenerator((context, id) => new[]
            {
                $"tenant:{context.TenantId}",
                "entity:product",
                $"product:{id}"
            })
            .Build();
}
```

### 读取或加载缓存

```csharp
public sealed class ProductQueryService
{
    private readonly ICacheService _cache;
    private readonly IRepository<Product> _products;

    public ProductQueryService(
        ICacheService cache,
        IRepository<Product> products)
    {
        _cache = cache;
        _products = products;
    }

    public async Task<Product?> GetProductAsync(long productId, CancellationToken ct)
    {
        var result = await _cache.GetOrSetAsync(
            ProductCacheKeys.ProductDetail,
            () => _products.GetByIdAsync(productId, ct),
            productId,
            cancellationToken: ct);

        return result.Value;
    }
}
```

### 更新后失效缓存

```csharp
public async Task UpdateProductAsync(long productId, UpdateProductRequest request, CancellationToken ct)
{
    // 更新数据库...

    await _cache.RemoveAsync(ProductCacheKeys.ProductDetail, productId, ct);
    await _cache.InvalidateByTagsAsync(
        new[] { $"product:{productId}", "entity:product" },
        ct);
}
```

### 批量读取

```csharp
var products = await _cache.GetManyAsync<ProductDto>(
    ProductCacheKeys.ProductDetail,
    productIds.Cast<object>(),
    ct);
```

## 缓存键规范

推荐规范：

- 使用小写英文和冒号分隔，例如 `product:detail:{id}`
- 使用 `{id}`、`{key}`、`{groupId}` 这类清晰占位符
- 作用域信息不要手动写进 `Name`，交给 `CacheScope` 处理
- 不要在缓存键中放入敏感信息
- 过期时间和标签跟随缓存键定义集中管理

最终缓存键由 `CacheKeyGenerator` 生成，格式大致如下：

```text
G:{baseKey}
T:{tenantId}:{baseKey}
S:{tenantId}:{storeId}:{baseKey}
U:{tenantId}:{userId}:{baseKey}
```

## 失效策略建议

业务写路径建议按下面优先级选择失效方式：

1. 精确删除：知道具体实例值时，优先使用 `RemoveAsync(definition, instanceValue)`。例如商品详情更新后删除 `product:detail:{id}`。
2. 标签失效：某类实体、列表或聚合发生变化时，使用 `InvalidateByTagAsync` 或 `InvalidateByTagsAsync`。这是当前缓存系统中最推荐的批量失效方式，因为它通过版本号做逻辑失效，不需要扫描 Redis key。
3. 作用域失效：租户、门店或用户范围需要整体刷新时，使用 `InvalidateTenantAsync`、`InvalidateStoreAsync`、`InvalidateUserAsync` 或 `InvalidateScopeAsync`。这些方法最终会按 key pattern 查找并删除缓存，适合低频管理操作，不适合作为高频业务写入后的常规动作。
4. 全量清空：`ClearAsync` 只用于测试、运维或明确的系统级重置。生产环境不要在普通业务流程中调用。

不建议在业务模块中直接拼 raw string key 做缓存失效。结构化业务缓存应优先通过 `CacheKeyDefinition` 描述 key、scope、TTL 和 tags。

典型标签：

```text
tenant:{tenantId}
store:{storeId}
user:{userId}
entity:product
product:{productId}
list:product
```

## 注意事项

- 多租户业务缓存应优先选择 `Tenant`、`Store` 或 `User` 作用域。
- `Global` 作用域只用于真正跨租户共享的数据。
- 使用 `Tenant`、`Store`、`User` 作用域时，请确保当前请求身份中有对应上下文。
- `AllowNull(false)` 是默认行为，工厂方法返回空值时不会写入缓存。
- Redis 模式下 `ClearAsync`、作用域失效和通配符失效都可能触发 key pattern 扫描，生产环境避免高频调用。
- 混合缓存适合热点读场景；写入后应主动做精确删除或标签失效。
- 标签失效是逻辑失效，旧缓存项可能在下次读取时才被发现并清理。它不会返回旧数据，因为读取路径会校验标签版本。

## 当前项目中的使用点

当前项目已经在 `AddAtlasCore` 中自动注册缓存模块，并根据 `CacheSettings:Provider` 选择底层提供者。

已有示例：

- `TenantDbConnProvider` 使用缓存保存租户数据库连接信息。
- `DataScope` 使用缓存保存门店访问范围。
- `StoreService` 使用缓存保存共享门店关系。
- `TokenCacheService` 使用缓存管理用户令牌版本和会话黑名单。

这些场景可以作为业务模块接入缓存的参考。
