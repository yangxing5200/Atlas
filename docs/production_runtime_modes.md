# Atlas 生产运行模式

Atlas 进程必须显式声明运行职责，避免 WebApi 扩容时误启动后台消费者或周期任务。

## 配置模型

运行模式配置位于 `Atlas:Runtime`：

```json
{
  "Atlas": {
    "Runtime": {
      "Mode": "WebApi",
      "EnableMessagingConsumers": false,
      "EnableTenantOutboxDispatcher": false,
      "EnableBackgroundJobWorker": false,
      "EnableRecurringTaskRunner": false
    }
  }
}
```

`Mode` 支持：

| Mode | 默认职责 |
| --- | --- |
| `WebApi` | 处理 HTTP 请求、执行业务事务、写 tenant outbox、入队 `BackgroundJobs`。不启动后台 hosted services。 |
| `Worker` | 启动 RabbitMQ consumers、`TenantOutboxDispatcher`、`BackgroundJobWorker`、`RecurringTaskRunner`。 |
| `Migration` | 预留给独立迁移宿主；默认不启动后台执行平面。 |

非法模式会在启动注册阶段抛出明确错误。单个能力可以通过 `Enable*` 字段覆盖默认值，但生产推荐按宿主拆分职责，而不是在 WebApi 中打开后台执行。

## 推荐部署

WebApi：

```json
{
  "Atlas": {
    "Runtime": {
      "Mode": "WebApi"
    }
  },
  "Messaging": {
    "Provider": "RabbitMQ"
  },
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Queues": [ "default" ]
    }
  }
}
```

Worker：

```json
{
  "Atlas": {
    "Runtime": {
      "Mode": "Worker"
    }
  },
  "Messaging": {
    "Provider": "RabbitMQ"
  },
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Queues": [ "default", "tenant" ]
    }
  }
}
```

队列拆分时部署多个 Worker 实例即可，例如 `worker-default` 只监听 `default`，`worker-tenant` 只监听 `tenant`。如果 `Queues` 配置为空，运行时会回退到 `default` 队列。

MigrationJob：

```powershell
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- plan
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- apply
```

MigrationJob 使用 `Atlas:Runtime:Mode=Migration`，只执行 schema 升级和状态记录，不启动 HTTP 或后台任务。

## 健康检查

WebApi 暴露三个健康检查端点：

| Endpoint | 用途 |
| --- | --- |
| `/health/live` | liveness，只检查进程是否存活，不依赖外部服务。 |
| `/health/ready` | readiness，检查 Global MySQL、缓存、Redis、RabbitMQ、BackgroundJobs 状态。 |
| `/health` | 汇总所有检查。 |

本地 `Memory` cache 或 `Messaging:Provider=None` 时，Redis/RabbitMQ ready check 会返回 healthy，因为这些依赖不是当前运行模式的必要依赖。

## Redis 分布式锁

`RecurringTaskRunner` 通过 `IDistributedLockProvider` 防止多实例重复执行同一个周期任务。

- `CacheSettings:Provider=Memory` 时使用内存锁，只适合本地单实例。
- `CacheSettings:Provider=Redis` 或 `Hybrid` 时自动切换为 Redis 锁。
- Redis 锁使用唯一 token 释放，只有持有者能释放自己的锁，避免误删其他实例刚拿到的新锁。

生产多实例 Worker 应使用 Redis 或 Hybrid cache provider。

## 容器化部署

生产 Dockerfile 和 compose 样例见 `docs/deployment_guide.md`。WebApi、Worker 和 MigrationJob 使用独立镜像，环境变量显式声明 `Atlas:Runtime:Mode`，避免不同进程职责混用。
