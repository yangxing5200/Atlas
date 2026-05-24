# Atlas 脚手架产品化 PR 路线图

本文把脚手架补全工作拆成可独立 review、可逐步合并的 PR。每个 PR 都应保持单一目标，避免把架构调整、运行时行为和测试基线混在一起。

## 阶段一：工程基线和配置治理

### PR-01：脚手架路线图和仓库文本约束

目标：
- 建立脚手架产品化的 PR 总路线图。
- 明确文档和源码按 UTF-8 维护，避免 Windows PowerShell 默认编码造成误判。
- 增加 Git 文本属性，降低跨平台换行差异。

验收：
- 新增路线图文档。
- 新增或确认 `.editorconfig` / `.gitattributes` 覆盖源码、Markdown、JSON、YAML。
- 不改运行时代码。

### PR-02：集中包版本管理

目标：
- 引入 `Directory.Packages.props`。
- 移除项目文件里的重复版本号。
- 统一测试包、EF Core、MassTransit、Serilog、Microsoft.Extensions 等版本。

验收：
- `dotnet restore Atlas.sln` 成功。
- `dotnet build Atlas.sln --configuration Release` 成功。
- 不改变业务行为。

### PR-03：构建属性分层

目标：
- 整理 `Directory.Build.props`。
- 区分 src、tests、samples 的 warning 策略。
- 保留 `Atlas.Analyzers` 全局接入。

验收：
- src 默认开启 nullable、implicit usings、latest analysis。
- tests 可保留较宽松 warning 策略。
- 不立即把所有 warning 升级为 error。

### PR-04：配置模板和环境变量样例

目标：
- 新增 `appsettings.Template.json`。
- 新增 `.env.example`。
- 统一 WebApi、Worker、Sample 的配置说明。

验收：
- 新人可根据模板启动本地环境。
- 敏感值只出现占位符，不出现真实密钥。

### PR-05：Options 强类型绑定和启动校验

目标：
- 为 Security、Cache、Messaging、BackgroundTasks、Snowflake 增加 Options 绑定。
- 对关键配置启用 `ValidateOnStart()`。
- 把散落的 `configuration["..."]` 读取逐步收敛。

验收：
- 缺少 Token/Crypto/ConnectionStrings 时启动期给出明确错误。
- Memory/None 模式允许本地最小启动。

### PR-06：CI 基线门禁

目标：
- CI 增加中央包版本管理验证，防止项目文件重新出现内联 `PackageReference Version`。
- CI 跑 Core/Data/Services 非集成测试。
- 增加 TRX 测试报告和可选 coverage 输出。
- 记录当前 format 历史基线问题，完整 format 门禁放到独立格式化基线 PR。

验收：
- push 和 pull request 都执行。
- 失败信息能直接定位到包版本、构建或测试问题。
- CI 不引入已知会被历史格式问题误触发的失败门禁。

## 阶段二：模块化和模板生成

### PR-07：模块契约

目标：
- 引入 `IAtlasModule`。
- 支持模块声明服务注册、控制器程序集、消费者程序集。
- 让核心 DI 不再手工知道每个业务服务。

验收：
- 现有注册行为不变。
- 新模块可通过程序集注册接入。

### PR-08：模块扫描和启动扩展

目标：
- 扩展 `AddAtlasWebApi()` / `AddAtlasCore()` 支持模块程序集。
- 支持模块内 Controller、MassTransit Consumer、AutoMapper Profile 自动接入。

验收：
- Sample WebApi 不需要手工注册业务 Controller。
- Worker 可按模块加载 Consumer。

### PR-09：示例业务模块迁移

目标：
- 将 Product/Order 等示例业务从框架核心迁到 sample 或 `Atlas.Modules.ECommerce`。
- 框架核心只保留可复用底座和平台模块。

验收：
- 原 sample API 行为保持。
- 核心框架不再依赖示例业务概念。

### PR-10：`dotnet new atlas-module`

目标：
- 新增模块模板。
- 生成 Entity、Configuration、DTO、Service、Controller、测试骨架。

验收：
- 本地安装模板后可生成一个最小可编译模块。
- 模板默认符合多租户边界约束。

### PR-11：`dotnet new atlas-webapi` 和 `atlas-worker`

目标：
- 生成标准 WebApi 宿主。
- 生成标准 Worker 宿主。
- 默认引用框架包和模块包。

验收：
- 新项目能用 docker-compose 基础设施启动。
- WebApi 和 Worker 职责分离。

### PR-12：模块开发验收测试

目标：
- 为模板生成结果增加编译测试。
- 增加 analyzer 违规用例测试。

验收：
- CI 能验证模板输出可 restore/build/test。
- 违规直接访问 tenant DbContext 会被 analyzer 阻断。

## 阶段三：运行时和数据生命周期

### PR-13：WebApi/Worker 运行模式拆分

目标：
- WebApi 默认只处理请求、查询、入队。
- Worker 默认处理 consumer、outbox dispatcher、后台任务。
- 避免生产环境 Web 节点误跑后台任务。

验收：
- 配置项能明确控制运行模式。
- 默认生产配置符合职责拆分。

### PR-14：数据库初始化 CLI

目标：
- 扩展 `Atlas.LocalSetup` 为正式 CLI。
- 支持初始化全局库、创建租户库、执行 seed。

验收：
- 命令可重复执行。
- 输出清晰，不泄露连接串密码。

### PR-15：租户迁移状态管理

目标：
- 增加租户 schema 版本和迁移状态表。
- 支持按租户批量迁移。

验收：
- 迁移成功、失败、重试都有状态记录。
- 失败不会影响其他租户继续迁移。

### PR-16：Migration Job

目标：
- 提供独立 migration job 宿主或容器入口。
- 发布时可先迁移再启动 WebApi/Worker。

验收：
- Docker/CI 可独立运行 migration job。
- 支持 dry-run。

### PR-17：健康检查分层

目标：
- 拆分 `/health/live`、`/health/ready`、`/health`。
- ready 检查 MySQL、Redis、RabbitMQ、后台任务状态。

验收：
- Kubernetes readiness/liveness 可直接使用。
- 本地 Memory/None 模式仍可通过 live。

### PR-18：生产 Docker 资产

目标：
- 新增 WebApi Dockerfile。
- 新增 Worker Dockerfile。
- 新增生产 compose 样例。

验收：
- 镜像可 build。
- compose 能启动 WebApi、Worker、MySQL、Redis、RabbitMQ、Seq。

## 阶段四：企业能力和发布治理

### PR-19：RBAC 模型

目标：
- 增加 Role、Permission、UserRole、RolePermission。
- 定义租户管理员、平台管理员、门店角色边界。

验收：
- 现有 TenantAdmin 策略迁移到 RBAC。
- 权限变更可及时生效。

### PR-20：认证会话增强

目标：
- 增加 refresh token、token revoke、会话列表、强制下线。
- 收敛 token 缓存 key 和 token version 逻辑。

验收：
- 登录、刷新、登出、强制下线有完整测试。
- Token 泄露后可主动失效。

### PR-21：OpenTelemetry

目标：
- 接入 trace、metrics、log correlation。
- 覆盖 HTTP、EF Core、Redis、RabbitMQ、后台任务。

验收：
- 本地可导出到 OTLP collector 或控制台。
- trace id 能贯穿 HTTP 到消息消费。

### PR-22：审计和操作日志标准化

目标：
- 定义业务审计事件模型。
- 统一操作日志入口。
- 明确敏感字段脱敏规则。

验收：
- 关键安全操作和租户管理操作有审计记录。
- 日志中不出现明文密码、token、密钥。

### PR-23：NuGet 打包

目标：
- 为框架库补 PackageId、Description、RepositoryUrl、License。
- 生成可发布包。

验收：
- `dotnet pack` 成功。
- 包引用方式能运行 sample。

### PR-24：发布流水线和版本策略

目标：
- 增加 tag 触发发布。
- 生成 NuGet 包、Docker 镜像、release notes。
- 明确 SemVer 和 migration 兼容策略。

验收：
- dry-run 发布成功。
- release notes 自动列出 breaking changes、migration notes。

## 推荐执行顺序

1. 先完成 PR-01 到 PR-06，稳定工程基线。
2. 再完成 PR-07 到 PR-12，形成真正的模块脚手架。
3. 然后完成 PR-13 到 PR-18，补齐生产运行和数据生命周期。
4. 最后完成 PR-19 到 PR-24，补企业级能力和发布治理。
