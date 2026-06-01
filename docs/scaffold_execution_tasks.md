# Atlas 多租户脚手架任务拆分与验收标准

本文是 `scaffold_improvement_plan_final.md` 的执行版任务清单。每个任务都按可独立 review、可独立合并、合并后仓库仍可用的原则拆分。全部任务完成后，Atlas 应达到以下整体目标：

1. 业务工程师通过模板创建模块，默认使用 Repository、QueryService、Service、Controller 和测试骨架。
2. 普通业务代码不能直接访问租户 DbContext、`DbContext.Set<T>()` 或 EF raw SQL。
3. WebApi、Worker、Migration 职责明确，生产部署不会把后台执行平面误放到 Web 节点。
4. 租户初始化、迁移、后台任务、消息幂等、缓存和审计由框架提供默认能力。
5. CI 能持续验证构建、测试、Analyzer 边界、模板输出和发布资产。

## 一、通用完成标准

每个任务都必须满足以下通用标准，除非任务说明中明确豁免：

1. 任务完成后 `dotnet restore Atlas.sln` 成功。
2. 任务完成后 `dotnet build Atlas.sln --no-restore` 成功。
3. 不引入无业务意义的整文件格式化或换行 diff。
4. 不新增 `UnscopedSet` 或任何公开绕过租户范围过滤的 DbContext 入口。
5. 不在普通业务/API/Consumer 代码中新增 `AtlasTenantDbContext`、`ITenantDbContextFactory`、`DbContext.Set<T>()`、`FromSql*` 或 `ExecuteSql*` 直接使用。
6. 新增 public API 必须有文档或示例说明使用边界。
7. 涉及租户数据读写的任务必须能回答：SQL 或 LINQ 最终是否限定了正确 `TenantId`，门店范围实体是否限定了正确可见门店。

基础验证命令：

```bash
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj --no-build
dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-build
dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build
```

## 二、里程碑定义

| 里程碑 | 目标 | 完成后能力 |
| --- | --- | --- |
| M0 工程门禁 | 清理构建、换行、CI、Analyzer 测试基础。 | 后续 PR 能稳定 review，边界规则可被自动测试。 |
| M1 租户边界硬化 | 拆出 tenant runtime，收窄 Analyzer 豁免，收口高风险实体和 SQL。 | 普通业务代码无法绕过 Repository/QueryService 访问租户数据。 |
| M2 模块开发体验 | 提供模块模板和模板 CI。 | 新业务模块生成后默认符合多租户边界。 |
| M3 生产运行模式 | 拆分 WebApi/Worker/Migration，补健康检查和分布式锁。 | 生产部署职责清晰，后台任务可多实例运行。 |
| M4 租户生命周期 | CLI、schema 状态、migration job、seed 分层。 | 租户创建、迁移和初始化可运维。 |
| M5 企业能力 | RBAC、会话、审计、敏感日志。 | 权限、安全和审计达到企业项目脚手架要求。 |
| M6 发布治理 | OpenTelemetry、Docker、NuGet、发布流水线。 | 框架可观测、可打包、可发布、可升级。 |

## 三、任务清单

## M0：工程门禁

### TASK-M0-01：NuGet 源治理和中央包版本门禁

优先级：P0

目标：消除 `NU1507`，并防止项目文件重新出现内联 `PackageReference Version`。

交付物：

1. 仓库级 `NuGet.config` 或等价 source mapping。
2. CI 或脚本检查 csproj 中不得出现 `PackageReference Version`。
3. 文档说明本地私有源和公共源的使用方式。

验证标准：

1. `dotnet restore Atlas.sln` 不出现 `NU1507`。
2. `rg "PackageReference Include=.*Version=" -g "*.csproj"` 无结果，或检查脚本通过。
3. 新增一个故意带 Version 的临时 csproj 可被检查脚本拦截。

完成后能力：依赖来源清晰，后续构建输出不会被重复 NuGet 警告淹没。

### TASK-M0-02：换行和格式基线治理

优先级：P0

目标：解决 `.gitattributes` 与历史文件行尾不一致问题，避免无业务内容的整文件 diff。

交付物：

1. 明确 `.gitattributes` 对 cs、json、md、yml、props、csproj 的行尾策略。
2. 如需归一化，单独提交 normalization，不混入业务修改。
3. 增加格式检查说明或脚本。

验证标准：

1. `git status -sb` 在 fresh checkout 后干净。
2. 修改并还原一个 cs 文件后不会出现整文件 diff。
3. `git diff --check` 通过。

完成后能力：PR review 只看到真实变更。

### TASK-M0-03：Warning 分层和第一批治理

优先级：P0

目标：让 warning 债务可管理，先治理 src 高风险 warning，不要求一次清零 tests。

交付物：

1. `Directory.Build.props` 中区分 src、tests、samples 的 warning 策略。
2. 处理第一批 src warning：过时 API、隐藏成员、未使用变量、明显 nullable 风险。
3. 记录暂缓治理的 warning 基线。

验证标准：

1. `dotnet build Atlas.sln --no-restore` 成功。
2. src warning 数量相比当前基线下降。
3. 不因 warning 治理改变运行时行为；涉及行为的修改必须有测试。

完成后能力：构建信号更干净，后续可逐步把关键 warning 升级为 error。

### TASK-M0-04：CI 基线门禁

优先级：P0

目标：为所有 PR 建立最低自动验证。

交付物：

1. CI workflow：restore、build、Core/Data/Services tests。
2. 中央包版本检查。
3. TRX 或等价测试报告。
4. 明确 integration tests 运行条件。

验证标准：

1. push 和 pull request 均触发 CI。
2. CI 能在构建失败、测试失败、包版本违规时给出明确错误。
3. CI 不依赖本机绝对路径和个人 NuGet 配置。

完成后能力：每个后续任务合并前都有自动质量门禁。

### TASK-M0-05：Analyzer 测试夹具

优先级：P0

目标：让 `ATL001/ATL002/ATL003` 有可持续回归测试。

交付物：

1. Analyzer test 项目或 Roslyn 测试夹具。
2. 正例：业务项目使用 Repository 编译通过。
3. 反例：业务项目直接引用 DbContext、调用 `Set<T>`、调用 raw SQL 均报错。

验证标准：

1. Analyzer 测试可在 CI 中运行。
2. 直接注入 `AtlasTenantDbContext` 触发 `ATL001`。
3. 调用 `DbContext.Set<T>()` 触发 `ATL002`。
4. 调用 `ExecuteSql*` 或 `FromSql*` 触发 `ATL003`。
5. Data/runtime 批准项目中的基础设施访问不误报。

完成后能力：租户边界规则可以被自动化测试保护。

## M1：租户边界硬化

### TASK-M1-01：Tenant runtime 与普通业务服务拆分

优先级：P0

目标：把需要接触 DbContext 的基础设施 runtime 从普通业务服务中分离。

交付物：

1. 明确 tenant runtime 范围：outbox dispatcher、domain event outbox、consumer runtime、provisioning、maintenance job。
2. 移动到独立 namespace/folder；条件成熟时独立项目，例如 `Atlas.TenantRuntime`。
3. DI 注册保持兼容。

验证标准：

1. 移动前后公开业务行为不变。
2. `rg "DbContext|\\.Set<|ITenantDbContextFactory" src/Atlas.Services.Tenant -g "*.cs"` 只命中被标记为 runtime 的位置，或普通业务目录无命中。
3. `dotnet build Atlas.sln --no-restore` 成功。

完成后能力：后续可以收窄 Analyzer 豁免，不会误伤合法基础设施代码。

### TASK-M1-02：Outbox/Inbox 专用访问入口

优先级：P0

目标：业务代码不直接引用或操作 `TenantOutboxMessage`、`TenantInboxMessage`。

交付物：

1. `ITenantOutboxStore` / `ITenantInboxStore` 或等价专用接口。
2. outbox dispatcher 和 consumer runtime 改用专用接口。
3. outbox/inbox 查询和写入集中带 `TenantId` 条件。

验证标准：

1. 业务服务和 Consumer 目录不直接 `db.Set<TenantOutboxMessage>` 或 `db.Set<TenantInboxMessage>`。
2. outbox 拉取包含 `TenantId == tenantId`。
3. inbox 幂等包含 `TenantId + MessageId + ConsumerName`。
4. 相关单元测试覆盖重复消费和跨租户隔离。

完成后能力：高风险基础设施实体不会成为业务层随手可用的数据入口。

### TASK-M1-03：收窄 Analyzer 豁免

优先级：P0

目标：让普通业务代码直接访问租户 DbContext 时编译失败。

交付物：

1. 更新 `TenantBoundaryAnalyzer.ShouldEnforce`。
2. 只豁免 Data、Migrations、Analyzers、LocalSetup、DI composition 和明确 runtime 项目。
3. 补充 Analyzer 正反例测试。

验证标准：

1. 在普通业务服务中注入 `ITenantDbContextFactory` 会触发 `ATL001`。
2. 在普通业务服务中调用 `DbContext.Set<T>()` 会触发 `ATL002`。
3. 在普通业务服务中调用 EF raw SQL 会触发 `ATL003`。
4. runtime 项目合法访问通过，并有租户谓词测试。

完成后能力：业务层绕过 Repository/QueryService 的风险被编译期阻断。

### TASK-M1-04：租户 SQL Executor 强化

优先级：P0

目标：将租户 raw SQL 从字符串包含检查升级为更受控的 API。

交付物：

1. 设计更窄的 executor API，减少调用方手写完整 SQL 的机会。
2. 对无法模板化的运维 SQL，提供命名明确的 approved infrastructure 方法。
3. 所有租户 SQL 调用点迁移到新 API。

验证标准：

1. 新 API 必须显式接收 `tenantId`。
2. 删除、更新、claim 类 SQL 的最终语句包含 `TenantId` 谓词。
3. 缺少 tenantId 或 tenantId <= 0 时抛异常。
4. 单元测试覆盖缺少 TenantId、错误 TenantId、正常执行三类场景。

完成后能力：租户库原生 SQL 有集中、可测试的边界。

### TASK-M1-05：QueryService 标准入口

优先级：P1

目标：给复杂读模型一个标准入口，避免开发者为了复杂查询回退到裸 DbContext。

交付物：

1. QueryService 接口和命名规范。
2. 示例 QueryService，内部使用 Repository/ScopedSet 或批准 reader。
3. 模块开发清单更新。

验证标准：

1. 示例复杂查询不引用 `AtlasTenantDbContext`。
2. 门店范围实体查询仍走 `ScopedSet` 或等价范围过滤。
3. Analyzer 对 QueryService 中裸 DbContext 访问仍能拦截。

完成后能力：复杂查询也有安全默认路径。

### TASK-M1-06：租户边界 Review Checklist 固化

优先级：P1

目标：把数据边界要求固化到开发和 review 流程。

交付物：

1. 更新 `module_development_checklist.md`。
2. 增加 PR review checklist。
3. 明确 Global 数据、Tenant 数据、StoreOnly、Shared 的检查点。

验证标准：

1. checklist 明确禁止普通业务代码使用 `DbContext.Set<T>()`、`FromSql*`、`ExecuteSql*`、`IgnoreQueryFilters`。
2. checklist 明确 API 入参不接受可篡改 TenantId。
3. checklist 能映射到 Analyzer 和模板验收。

完成后能力：人工 review 和自动门禁对齐。

## M2：模块开发体验

### TASK-M2-01：最小模块模板

优先级：P1

目标：提供可生成、可编译的最小业务模块模板。

交付物：

1. `dotnet new atlas-module` 模板。
2. 模块项目、Module 类、最小 Service、Controller、测试项目或测试骨架。
3. 模板安装和使用文档。

验证标准：

1. `dotnet new atlas-module -n Demo.Module -o .tmp/Demo.Module` 成功。
2. 生成项目可 `dotnet restore` 和 `dotnet build`。
3. 生成代码不包含 `DbContext`、`Set<T>`、`FromSql`、`ExecuteSql`。

完成后能力：工程师可以用模板创建新模块。

### TASK-M2-02：租户实体和 EF 配置模板

优先级：P1

目标：模板生成的实体默认具备租户隔离语义。

交付物：

1. `ITenantEntity`、`ISharedEntity`、`IStoreOnlyEntity` 三类实体模板。
2. EF configuration 模板。
3. 不修改 `AtlasTenantDbContext` 增加 `DbSet`。

验证标准：

1. 生成的租户实体必须有明确隔离接口。
2. 生成的 EF configuration 可被 `ApplyConfigurationsFromAssembly` 加载。
3. `rg "DbSet<" .tmp/Demo.Module` 无结果。

完成后能力：新实体默认符合共享租户库隔离要求。

### TASK-M2-03：Service、Repository、QueryService 模板

优先级：P1

目标：模板生成的业务代码默认通过安全数据访问入口。

交付物：

1. CRUD Service 模板。
2. QueryService 模板。
3. 可选专用 Repository 模板。
4. UnitOfWork 和领域事件 outbox 示例。

验证标准：

1. 生成的 Service 只依赖 Repository/QueryService/UnitOfWork。
2. 生成代码中无 `AtlasTenantDbContext` 和 `ITenantDbContextFactory`。
3. 写入租户实体时由 Repository 校验或补齐 `TenantId`。

完成后能力：业务服务默认不绕过租户边界。

### TASK-M2-04：Controller 和 API 模板

优先级：P1

目标：模板生成的 API 不暴露可篡改租户上下文。

交付物：

1. Controller 模板。
2. Request/Response DTO 模板。
3. ProblemDetails、权限策略、分页查询示例。

验证标准：

1. Request DTO 不包含可由外部随意传入的 `TenantId`。
2. 需要管理员权限的接口使用统一授权策略。
3. Controller 不捕获通用 `Exception`，交给统一异常处理。

完成后能力：API 默认符合租户上下文和异常处理规范。

### TASK-M2-05：模板 CI 验收

优先级：P1

目标：模板不会随着框架演进悄悄损坏。

交付物：

1. CI 中生成模板模块。
2. 对模板输出执行 restore/build/test。
3. 对模板输出执行 Analyzer 违规反例验证。

验证标准：

1. 模板生成项目 build 成功。
2. 模板生成项目无禁止 API。
3. 故意加入 `DbContext.Set<T>()` 的模板反例会导致 Analyzer 测试失败。

完成后能力：脚手架开发体验可持续维护。

### TASK-M2-06：示例业务从核心迁出

优先级：P1

目标：框架核心只保留平台底座和可复用能力，不承载 Product/Order demo 概念。

交付物：

1. 将 Product/Order 等 demo 迁移到 sample 或独立 `Atlas.Modules.ECommerce`。
2. 保持 sample API 行为不变。
3. 更新文档和模块注册。

验证标准：

1. 核心框架不再依赖示例业务服务。
2. Sample WebApi 仍能启动并注册示例模块。
3. 相关 tests 通过。

完成后能力：框架和业务样例边界清晰。

## M3：生产运行模式

### TASK-M3-01：Runtime Mode 配置模型

优先级：P1

目标：用强类型配置表达当前进程是 WebApi、Worker 还是 Migration。

交付物：

1. `AtlasRuntimeModeOptions` 或等价类型。
2. `WebApi`、`Worker`、`Migration` 枚举或常量。
3. 启动校验和配置文档。

验证标准：

1. 非法模式启动时报明确错误。
2. 默认 WebApi/Worker 配置符合宿主职责。
3. 单元测试覆盖模式解析。

完成后能力：运行职责不再完全依赖人工配置约定。

### TASK-M3-02：WebApi 默认只处理 HTTP

优先级：P1

目标：WebApi 默认不运行后台执行平面。

交付物：

1. WebApi 模式默认关闭 consumer 注册、tenant outbox dispatcher、BackgroundJobWorker、RecurringTaskRunner。
2. 保留显式配置覆盖能力。
3. 文档说明生产推荐配置。

验证标准：

1. WebApi 模式下不会注册后台 hosted services。
2. WebApi 仍能写 tenant outbox 和入队 BackgroundJobs。
3. 集成测试或服务注册测试覆盖默认行为。

完成后能力：HTTP 节点扩容不会意外执行后台任务。

### TASK-M3-03：Worker 默认处理后台执行平面

优先级：P1

目标：Worker 是 consumer、outbox dispatcher、后台任务和周期任务的默认宿主。

交付物：

1. Worker 模式默认启用后台能力。
2. 支持按队列启用不同 Worker。
3. Worker 启动文档和配置样例。

验证标准：

1. Worker 模式注册 `TenantOutboxDispatcher`、`BackgroundJobWorker`、`RecurringTaskRunner`。
2. Consumer assembly 能被模块系统加载。
3. 队列配置为空时使用默认队列。

完成后能力：后台执行平面可独立部署和扩容。

### TASK-M3-04：健康检查分层

优先级：P2

目标：支持生产容器/K8s readiness 和 liveness。

交付物：

1. `/health/live`。
2. `/health/ready`。
3. `/health` 汇总。
4. MySQL、Redis、RabbitMQ、BackgroundJobs 状态检查。

验证标准：

1. live 不依赖外部服务，进程活着即可通过。
2. ready 在必要依赖不可用时失败。
3. Memory/None 本地模式不因 Redis/RabbitMQ 未配置而 ready 失败。

完成后能力：生产编排系统可正确判断进程状态。

### TASK-M3-05：Redis 分布式锁

优先级：P1

目标：周期任务在多实例环境中同一时间只由一个实例执行。

交付物：

1. Redis `IDistributedLockProvider`。
2. 使用 token 校验释放锁，避免误删其他实例锁。
3. 缓存 provider 为 Redis/Hybrid 时默认替换内存锁。

验证标准：

1. 多并发实例只有一个能拿到同一 resource 锁。
2. 锁过期后可重新获取。
3. 非持有者不能释放锁。
4. Memory 模式仍可用于本地单实例。

完成后能力：多实例 Worker 可安全运行周期任务。

## M4：租户数据生命周期

### TASK-M4-01：LocalSetup CLI 命令化

优先级：P2

目标：把本地初始化工具变成可重复执行的 CLI。

交付物：

1. `init-global`、`create-tenant-db`、`seed-demo`、`reset-demo` 等命令。
2. 参数和环境变量说明。
3. 输出敏感信息脱敏。

验证标准：

1. 命令重复执行不会因已存在资源失败。
2. 错误输出不包含明文密码。
3. 本地 demo 可以按文档初始化。

完成后能力：新人和 CI 可稳定初始化环境。

### TASK-M4-02：租户 Schema 状态模型

优先级：P2

目标：记录每个租户库的迁移版本和状态。

交付物：

1. Global 库 schema 状态实体和 migration。
2. 状态字段：TenantId、CurrentVersion、TargetVersion、Status、LastError、RetryCount、UpdatedAt。
3. Repository 或 service 查询入口。

验证标准：

1. 每个租户迁移状态可查询。
2. 迁移失败记录错误和重试次数。
3. 状态表唯一键包含 TenantId。

完成后能力：租户迁移可观测、可恢复。

### TASK-M4-03：批量迁移执行器

优先级：P2

目标：按租户批量执行 schema migration，单租户失败不阻塞其他租户。

交付物：

1. 迁移执行 service。
2. 分批扫描 active/trial 租户。
3. 失败重试和状态更新。

验证标准：

1. 一个租户迁移失败不会中断后续租户。
2. 成功、失败、跳过、重试都有状态记录。
3. 支持 cancellation token。

完成后能力：租户库可批量安全升级。

### TASK-M4-04：Migration Job 宿主

优先级：P2

目标：发布流水线可独立运行迁移，而不是 WebApi/Worker 启动时自动迁移。

交付物：

1. `Atlas.MigrationJob` 或等价入口。
2. 支持 dry-run。
3. 支持输出 migration plan。

验证标准：

1. dry-run 不修改数据库。
2. apply 模式执行迁移并更新状态表。
3. Docker/CI 可独立运行。

完成后能力：生产发布可以先迁移再启动应用。

### TASK-M4-05：Seed 分层

优先级：P2

目标：区分 demo、本地和生产 seed，避免生产误写 demo 数据。

交付物：

1. demo seed 独立命令。
2. 生产 seed 白名单。
3. seed 幂等策略。

验证标准：

1. 生产模式不会执行 demo seed。
2. seed 重复执行结果一致。
3. 文档明确各环境 seed 用法。

完成后能力：初始化数据可控、可审计。

## M5：企业安全能力

### TASK-M5-01：RBAC 数据模型

优先级：P3

目标：替代单一 TenantAdmin 策略，支持租户、平台、门店权限。

交付物：

1. Role、Permission、UserRole、RolePermission 实体和配置。
2. 租户内唯一索引。
3. 初始权限 seed。

验证标准：

1. 权限唯一键包含 TenantId 或明确全局范围。
2. 用户角色查询不会跨租户。
3. migration 和 tests 通过。

完成后能力：权限模型可配置。

### TASK-M5-02：授权策略接入 RBAC

优先级：P3

目标：Controller 权限检查通过 RBAC，而不是硬编码角色。

交付物：

1. Permission requirement。
2. Authorization handler。
3. 权限缓存和失效逻辑。

验证标准：

1. 缺少权限返回 403。
2. 权限变更后旧缓存失效。
3. 跨租户权限不可见。

完成后能力：业务权限可扩展。

### TASK-M5-03：会话和 Token 增强

优先级：P3

目标：支持 refresh token、会话列表、撤销和强制下线。

交付物：

1. Refresh token 模型和接口。
2. Session list API。
3. Revoke/force logout。

验证标准：

1. refresh token 可换取新 access token。
2. 被撤销 session 不能继续访问。
3. TokenVersion 变化使旧 token 失效。

完成后能力：账号安全事件可主动处置。

### TASK-M5-04：审计事件标准化

优先级：P3

目标：统一关键业务和安全操作的审计记录。

交付物：

1. Audit event 模型。
2. 审计写入服务。
3. 登录、登出、切换门店、权限变更、租户管理接入。

验证标准：

1. 关键安全操作均有审计记录。
2. 审计记录包含 TenantId、UserId、StoreId、TraceId。
3. 审计失败不吞掉主业务异常。

完成后能力：关键操作可追踪。

### TASK-M5-05：敏感数据脱敏测试

优先级：P3

目标：确保日志不输出明文敏感数据。

交付物：

1. 敏感字段测试。
2. request/response body 日志脱敏测试。
3. token、password、secret、phone、email 等字段规则。

验证标准：

1. 日志中不出现明文 password、token、secret。
2. phone/email 按策略脱敏。
3. 新增敏感字段可配置。

完成后能力：日志默认符合安全要求。

## M6：可观测性和发布治理

### TASK-M6-01：OpenTelemetry 基线

优先级：P3

目标：建立 trace、metrics、log correlation。

交付物：

1. HTTP、EF、Redis、RabbitMQ、BackgroundJobs instrumentation。
2. OTLP exporter 配置。
3. trace id 写入日志。

验证标准：

1. 一次 HTTP 请求到消息消费可关联同一 trace。
2. BackgroundJob 执行有 span。
3. 本地可导出到 console 或 OTLP collector。

完成后能力：生产问题可追踪。

### TASK-M6-02：生产 Docker 资产

优先级：P2

目标：提供 WebApi、Worker、Migration 的生产镜像和 compose 样例。

交付物：

1. WebApi Dockerfile。
2. Worker Dockerfile。
3. Migration Dockerfile 或 entrypoint。
4. 生产 compose 样例。

验证标准：

1. 三个镜像可 build。
2. compose 可启动 MySQL、Redis、RabbitMQ、Seq、WebApi、Worker。
3. 配置通过环境变量注入，不包含真实密钥。

完成后能力：部署拓扑可复制。

### TASK-M6-03：NuGet 打包

优先级：P3

目标：框架库可作为 NuGet 包引用。

交付物：

1. PackageId、Description、RepositoryUrl、License。
2. pack 配置。
3. package smoke test。

验证标准：

1. `dotnet pack Atlas.sln` 或指定项目 pack 成功。
2. 新 sample 通过包引用可 build。
3. 包不包含 appsettings 中的真实密钥。

完成后能力：脚手架可分发复用。

### TASK-M6-04：发布流水线

优先级：P3

目标：tag 触发 NuGet、Docker 和 release notes。

交付物：

1. release workflow。
2. dry-run 发布。
3. changelog/release notes 模板。

验证标准：

1. dry-run 不推送真实包但生成完整产物。
2. tag 版本和包版本一致。
3. release notes 包含 breaking changes 和 migration notes。

完成后能力：发布过程可自动化。

### TASK-M6-05：版本和升级策略

优先级：P3

目标：明确 SemVer、迁移兼容和升级说明规则。

交付物：

1. 版本策略文档。
2. migration note 模板。
3. breaking change 标记规范。

验证标准：

1. 每次 release 都能说明是否需要 migration。
2. breaking change 有明确升级步骤。
3. 文档能指导从上一版本升级到当前版本。

完成后能力：使用方可安全升级脚手架。

## 四、跨任务验收矩阵

| 整体目标 | 覆盖任务 | 最终验收 |
| --- | --- | --- |
| 业务代码不能绕过租户边界 | M1-01 到 M1-06、M2-03、M2-05 | Analyzer 反例失败；业务目录无 DbContext/Set/raw SQL；Repository/ScopedSet 测试通过。 |
| 新模块默认正确 | M2-01 到 M2-06 | `dotnet new atlas-module` 输出可 build，且无禁止 API。 |
| WebApi/Worker 职责清晰 | M3-01 到 M3-03 | WebApi 不注册后台 hosted services；Worker 注册后台能力。 |
| 多实例后台安全 | M3-05 | 并发锁测试通过，周期任务不会重复执行。 |
| 租户生命周期可运维 | M4-01 到 M4-05 | CLI 可初始化，migration job 可 dry-run/apply，状态表可追踪失败。 |
| 企业安全能力 | M5-01 到 M5-05 | RBAC、会话撤销、审计、脱敏测试通过。 |
| 可观测和发布 | M6-01 到 M6-05 | trace 可串联，镜像可 build，包可 pack，release dry-run 成功。 |

## 五、执行约束

1. 每个任务应单独分支和 PR，不把架构移动、行为修改、格式化混在一起。
2. 每个 PR 都必须说明是否影响运行时行为、数据库 schema、公共 API 和部署配置。
3. 涉及 migration 的任务必须说明回滚或失败恢复策略。
4. 涉及 Analyzer 收紧的任务必须先保证合法 runtime 代码已经被拆出或被明确豁免。
5. 涉及模板的任务必须同时更新文档和模板验收测试。

## 六、任务完成后的整体目标判断

每个任务完成后，都应让系统至少获得一个可独立使用的能力：

1. M0 任务让仓库更稳定，后续所有任务可被自动验证。
2. M1 任务让租户数据边界更强，减少跨租户和跨共享组访问风险。
3. M2 任务让新模块开发路径默认正确，降低工程师理解底层架构的成本。
4. M3 任务让生产运行职责清晰，避免 Web 节点误跑后台任务。
5. M4 任务让租户数据库生命周期可操作、可追踪、可恢复。
6. M5 任务让安全、权限和审计满足企业项目要求。
7. M6 任务让框架可观测、可打包、可发布、可升级。

如果某个任务完成后不能提供上述任一能力，说明任务拆分过细或目标不完整，应合并到相邻任务重新定义。
