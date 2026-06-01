# Atlas 配置指南

## 运行模式

每个进程必须配置 `Atlas:Runtime:Mode`：

```json
{
  "Atlas": {
    "Runtime": {
      "Mode": "WebApi"
    }
  }
}
```

支持 `WebApi`、`Worker`、`Migration`。WebApi 默认不启动后台 hosted services；Worker 默认启动 consumers、tenant outbox dispatcher、`BackgroundJobWorker` 和 `RecurringTaskRunner`。详见 `docs/production_runtime_modes.md`。

本地初始化命令见 `docs/local_setup_cli.md`；生产租户库升级命令见 `docs/tenant_migration_lifecycle.md`。
OpenTelemetry 配置见 `docs/observability_guide.md`；生产 Docker/Compose 拓扑见 `docs/deployment_guide.md`；版本和发布规则见 `docs/release_and_versioning.md`。

Atlas 的宿主项目默认通过 `appsettings.json`、环境变量和命令行参数读取配置。新项目可以从根目录的 `appsettings.Template.json` 开始复制配置结构，并用 `.env.example` 准备本地环境变量。

## 配置分层

推荐顺序：

1. `appsettings.json` 保存非敏感默认值。
2. `appsettings.Development.json` 保存本地开发值。
3. 环境变量保存生产连接串、密钥、账号密码。
4. CI/CD 或容器编排系统注入最终运行配置。

## 关键配置

`ConnectionStrings:AtlasGlobal` 是全局控制库连接串，WebApi、Worker、迁移工具都需要。

`Security:Crypto:Key` 和 `Security:Token:SecretKey` 必须使用生产级随机值，不能使用模板里的占位符。
`Security:Token:RefreshTokenExpirationDays` 控制 refresh token 有效期，默认 7 天；退出登录、切换门店、修改密码、重置密码和强制下线会主动撤销相关 refresh token。

`CacheSettings:Provider` 支持 `Memory`、`Redis`、`Hybrid`。本地最小启动可以使用 `Memory`，生产多实例部署应使用 Redis 或 Hybrid。

`Messaging:Provider` 支持 `None` 和 `RabbitMQ`。WebApi 可以使用 `None` 或只入队，Worker 负责消费和投递。

`BackgroundTasks:*:Enabled` 控制后台任务是否在当前进程运行。生产环境建议 WebApi 关闭，Worker 开启。

`Snowflake:WorkerId` 和 `Snowflake:DatacenterId` 必须在多实例部署中保持唯一组合，也可以通过 `SNOWFLAKE_WORKER_ID` 和 `SNOWFLAKE_DATACENTER_ID` 注入。

`Observability:OpenTelemetry:*` 默认关闭。需要链路追踪时设置 `Enabled=true`，并将 `Exporter` 设置为 `Console` 或 `Otlp`。框架默认只使用稳定版 OpenTelemetry 包；数据库和 Redis 的细粒度 instrumentation 由宿主应用按需显式引入。

## 环境变量命名

.NET 配置系统使用双下划线表示层级，例如：

```bash
ConnectionStrings__AtlasGlobal=Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;
Security__Crypto__Key=replace-with-at-least-32-characters
Security__Token__SecretKey=replace-with-a-long-random-token-secret
Security__Token__RefreshTokenExpirationDays=7
CacheSettings__Provider=Redis
CacheSettings__Redis__ConnectionString=localhost:6379,abortConnect=false,allowAdmin=true
Messaging__Provider=RabbitMQ
Messaging__RabbitMQ__Host=localhost
Observability__OpenTelemetry__Enabled=true
Observability__OpenTelemetry__Exporter=Console
```

## 本地启动建议

1. 复制 `.env.example` 为 `.env`。
2. 执行 `docker compose up -d mysql redis rabbitmq`，或按 `docs/deployment_guide.md` 使用生产 compose 样例。
3. 执行 `dotnet restore Atlas.sln`。
4. 执行 `dotnet build Atlas.sln`.
5. 启动 WebApi 或 Worker。

`.env` 和 `.env.*` 默认被 `.gitignore` 忽略，避免密钥误提交。
