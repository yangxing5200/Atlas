# Atlas 配置指南

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

`CacheSettings:Provider` 支持 `Memory`、`Redis`、`Hybrid`。本地最小启动可以使用 `Memory`，生产多实例部署应使用 Redis 或 Hybrid。

`Messaging:Provider` 支持 `None` 和 `RabbitMQ`。WebApi 可以使用 `None` 或只入队，Worker 负责消费和投递。

`BackgroundTasks:*:Enabled` 控制后台任务是否在当前进程运行。生产环境建议 WebApi 关闭，Worker 开启。

`Snowflake:WorkerId` 和 `Snowflake:DatacenterId` 必须在多实例部署中保持唯一组合，也可以通过 `SNOWFLAKE_WORKER_ID` 和 `SNOWFLAKE_DATACENTER_ID` 注入。

## 环境变量命名

.NET 配置系统使用双下划线表示层级，例如：

```bash
ConnectionStrings__AtlasGlobal=Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;
Security__Crypto__Key=replace-with-at-least-32-characters
Security__Token__SecretKey=replace-with-a-long-random-token-secret
CacheSettings__Provider=Redis
CacheSettings__Redis__ConnectionString=localhost:6379,abortConnect=false,allowAdmin=true
Messaging__Provider=RabbitMQ
Messaging__RabbitMQ__Host=localhost
```

## 本地启动建议

1. 复制 `.env.example` 为 `.env`。
2. 执行 `docker compose up -d mysql redis rabbitmq`。
3. 执行 `dotnet restore Atlas.sln`。
4. 执行 `dotnet build Atlas.sln`.
5. 启动 WebApi 或 Worker。

`.env` 和 `.env.*` 默认被 `.gitignore` 忽略，避免密钥误提交。
