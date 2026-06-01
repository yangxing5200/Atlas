# Atlas Deployment Guide

Atlas 生产部署按进程职责拆分为 WebApi、Worker 和 MigrationJob。WebApi 只处理请求和写入 outbox；Worker 负责消息消费、tenant outbox 分发、一次性后台任务和周期任务；MigrationJob 负责租户 schema 升级。

## Docker 镜像

仓库提供三个生产 Dockerfile：

| Process | Dockerfile | Entry |
| --- | --- | --- |
| WebApi | `src/Atlas.WebApi/Dockerfile` | `dotnet Atlas.WebApi.dll` |
| Worker | `src/Atlas.Worker/Dockerfile` | `dotnet Atlas.Worker.dll` |
| MigrationJob | `src/Atlas.MigrationJob/Dockerfile` | `dotnet Atlas.MigrationJob.dll plan` |

从仓库根目录构建：

```powershell
docker build -f src\Atlas.WebApi\Dockerfile -t atlas-webapi:local .
docker build -f src\Atlas.Worker\Dockerfile -t atlas-worker:local .
docker build -f src\Atlas.MigrationJob\Dockerfile -t atlas-migration-job:local .
```

## Compose 样例

生产拓扑样例位于 `deploy/docker-compose.production.yml`，包含 MySQL、Redis、RabbitMQ、Seq、WebApi、Worker 和带 profile 的 MigrationJob。

启动基础服务和应用：

```powershell
docker compose -f deploy\docker-compose.production.yml up -d mysql redis rabbitmq seq webapi worker
```

执行迁移计划：

```powershell
docker compose -f deploy\docker-compose.production.yml --profile migration run --rm migration plan
```

应用迁移：

```powershell
docker compose -f deploy\docker-compose.production.yml --profile migration run --rm migration apply
```

## 配置注入

生产配置通过环境变量注入，使用 .NET 双下划线层级命名：

```text
ConnectionStrings__AtlasGlobal=Server=mysql;Port=3306;Database=atlas_global;User=atlas;Password=...
Security__Crypto__Key=...
Security__Token__SecretKey=...
CacheSettings__Provider=Redis
Messaging__Provider=RabbitMQ
Observability__OpenTelemetry__Enabled=true
```

Compose 文件中的 `change-me-*` 和 `replace-with-*` 只用于本地样例，生产必须由密钥系统或 CI/CD 注入真实随机值。

## 发布顺序

1. 构建并推送 WebApi、Worker、MigrationJob 镜像。
2. 部署或运行 MigrationJob `plan`。
3. 对生产备份和迁移计划做人工确认。
4. 运行 MigrationJob `apply`。
5. 滚动部署 Worker。
6. 滚动部署 WebApi。
7. 观察 `/health/ready`、Seq 日志和 OpenTelemetry trace。

WebApi 和 Worker 不应共享同一个进程。需要扩容时分别扩容 WebApi 或 Worker，避免 HTTP 扩容同时增加后台任务执行并发。
