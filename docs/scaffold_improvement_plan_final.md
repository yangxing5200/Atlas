# Atlas 多租户脚手架改进计划（定稿）

本文基于当前 `main` 分支 `098c9c8 chore: harden scaffold baseline` 制定。目标是把 Atlas 从“已有多租户底座”推进到“可稳定复用的多租户业务脚手架”，让开发工程师主要关注业务实体、业务服务、DTO、Controller 和测试，而不是重复处理租户隔离、门店共享范围、消息幂等、后台任务、配置和部署细节。

## 一、当前基线

### 已具备能力

1. WebApi 和 Worker 宿主足够薄，启动逻辑集中在 `Atlas.Extensions.DependencyInjection`。
2. 已有模块系统，`IAtlasModule` 可以声明服务注册、Controller 程序集、Consumer 程序集和 AutoMapper 程序集。
3. 租户库 `AtlasTenantDbContext` 不公开 `DbSet<TEntity>` 属性，也没有 `UnscopedSet` 之类的逃生入口。
4. 租户查询主路径为 `Repository -> ScopedSet -> EntityScopeFilter`，可按 `TenantId`、`StoreId` 和共享门店范围过滤。
5. `RepositoryBase` 在写入租户实体时会校验或补齐 `TenantId`。
6. `Atlas.Analyzers` 已经接入构建，可阻断业务层直接引用 `AtlasTenantDbContext`、`ITenantDbContextFactory`、`DbContext.Set<T>()` 和 EF raw SQL。
7. 可靠消息路径已经从直接 publish 收敛为 RabbitMQ + MassTransit + tenant outbox/inbox。
8. 后台任务已经区分为持久化一次性任务、周期任务和 RabbitMQ consumer。
9. 配置模板、环境变量样例、模块开发清单、多租户隔离最佳实践等文档已存在。

### 当前主要缺口

1. Analyzer 豁免范围仍偏大，`Atlas.Services.Tenant` 既包含业务服务，也包含 outbox/inbox/provisioning 等运行时基础设施，因此普通业务代码仍有机会在豁免项目内直接使用 `DbContext.Set<T>()`。
2. `TenantOutboxMessage`、`TenantInboxMessage` 等基础设施实体仍是 public entity，后续业务代码可以引用类型并误用。
3. SQL 入口已升级为 `ITenantSqlExecutor` 命名方法；后续新增 SQL 动作仍必须避免回退到通用 SQL 字符串执行。
4. WebApi 和 Worker 的职责拆分主要靠配置和文档约束，默认运行模式还没有从框架层强制收敛。
5. 周期任务默认使用内存锁，只适合单实例；生产多实例需要 Redis 或数据库级锁。
6. 工程基线还有 warning 债务和 NuGet package source mapping 警告。
7. Product、Order 等示例业务仍在框架核心内，长期会让脚手架和业务样例边界变模糊。
8. 租户迁移、schema version、migration job、生产 Docker、发布打包还未形成完整闭环。

## 二、目标架构原则

### 业务开发者应该看到的世界

业务开发者默认只接触以下入口：

1. 模块模板生成的 Entity、Configuration、DTO、Service、Controller 和测试。
2. `IRepository<TEntity>`、QueryService 或专用 Repository。
3. 领域服务、领域事件、`IBackgroundJobClient`。
4. 模块注册类 `IAtlasModule`。

业务开发者不应直接接触：

1. `AtlasTenantDbContext`。
2. `ITenantDbContextFactory`。
3. `DbContext.Set<T>()`。
4. EF raw SQL。
5. outbox/inbox 明细表。
6. 后台任务 claim、消息 inbox 幂等、迁移执行器等基础设施模板代码。

### 防线顺序

1. 模板默认正确：生成代码不暴露危险入口。
2. 依赖方向正确：业务项目不引用低层 EF 实现。
3. Analyzer 编译期阻断：错误写法直接不能编译。
4. Repository/runtime 兜底：即使基础设施代码必须使用 EF，也显式带租户边界。
5. 测试和 CI 防回归：违规样例必须在 CI 中失败。

仅仅不声明 `DbSet<TEntity>` 不足以防止误用。只要调用方能拿到 `DbContext` 和实体类型，`db.Set<TEntity>()` 就仍然成立。因此最终治理必须靠依赖方向、Analyzer、基础设施隔离和模板约束共同完成。

## 三、实施阶段

## 阶段 1：工程基线和门禁

目标：先把仓库变成可稳定 review、可持续演进的工程基线。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 1.1 | NuGet 源治理 | 为 `nuget.org` 和 `Linkedcare` 增加 package source mapping，或在仓库级 `NuGet.config` 明确单源策略。 | `dotnet restore Atlas.sln` 不再出现 `NU1507`。 |
| 1.2 | Warning 分层 | 把 src、tests、samples 的 warning 策略分层；先治理 src 高价值 warning。 | `dotnet build Atlas.sln --no-restore` 通过，src warning 明显下降。 |
| 1.3 | 换行治理 | 修正 `.gitattributes` 和历史文件行尾不一致问题，必要时单独做 normalization PR。 | 不再出现无业务内容的整文件 diff。 |
| 1.4 | CI 基线 | CI 增加 restore、build、Core/Data/Services unit tests、中央包版本检查。 | PR 自动执行，失败信息能直接定位。 |
| 1.5 | Analyzer 测试基础 | 为 `Atlas.Analyzers` 增加 Roslyn analyzer test 项目或测试夹具。 | 可写正反例验证 `ATL001/ATL002/ATL003`。 |

推荐 PR 拆分：

1. PR-A01：NuGet source mapping 和 package version 检查。
2. PR-A02：换行治理和 `.editorconfig`/`.gitattributes` 收敛。
3. PR-A03：warning 第一批治理，不改变运行时行为。
4. PR-A04：CI 基线门禁。
5. PR-A05：Analyzer 测试夹具。

## 阶段 2：租户边界硬化

目标：普通业务代码不能绕过 Repository/QueryService 直接访问租户库。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 2.1 | 拆分运行时基础设施 | 将 outbox dispatcher、inbox runtime、domain event outbox、tenant provisioning、tenant maintenance job 从普通业务服务中分离出来。短期可先按 namespace/folder 分层，长期可独立为 `Atlas.TenantRuntime` 或类似项目。 | 普通业务服务项目不需要 Analyzer 豁免。 |
| 2.2 | 收窄 Analyzer 豁免 | `ShouldEnforce` 不再整体豁免 `Atlas.Services.Tenant`；只豁免 Data、Migrations、Analyzers、LocalSetup、DI composition 和明确 runtime 项目。 | 普通 service 中直接 `DbContext.Set<T>()` 编译失败。 |
| 2.3 | 高风险实体访问收口 | 为 `TenantOutboxMessage`、`TenantInboxMessage` 建立专用 runtime repository/service，业务层不直接引用。 | 业务程序集无 outbox/inbox 类型引用。 |
| 2.4 | SQL 入口升级 | 将 `ITenantSqlExecutor` 维持为按动作命名的窄 API，由 executor 统一拼接关键 `TenantId` 谓词。 | 租户 SQL 调用点不能回退到手写完整 SQL 字符串。 |
| 2.5 | QueryService 规范 | 对复杂读模型引入 QueryService 模板，内部仍通过 Repository/ScopedSet 或批准的 infrastructure reader。 | 业务复杂查询有标准入口，不回退到裸 DbContext。 |
| 2.6 | Code Review 规则固化 | 将禁止项写入 checklist：`DbContext.Set`、`FromSql*`、`ExecuteSql*`、`IgnoreQueryFilters`、请求体 TenantId。 | 新模块 PR 必须按 checklist 验收。 |

推荐 PR 拆分：

1. PR-B01：移动/拆分 tenant runtime 类，保持行为不变。
2. PR-B02：新增 outbox/inbox 专用 repository/service。
3. PR-B03：收窄 Analyzer 豁免并补违规测试。
4. PR-B04：升级 `ITenantSqlExecutor` API。
5. PR-B05：补 QueryService 模板和文档。

关键注意点：

1. 不应把 `DbContext.Set<TEntity>()` 整体标记为 `Obsolete`，这会误伤 EF 基础设施实现。
2. 不应新增 `UnscopedSet` 或类似公开逃生口。
3. 不应把所有基础设施代码塞进业务服务项目后再靠人工约定区分。
4. 对必须扫描所有租户的后台任务，应采用“Global 查租户列表，每次打开单个租户库，并且租户库 SQL 显式限定 TenantId”的固定模式。

## 阶段 3：模块脚手架产品化

目标：新业务模块默认生成正确结构，让开发工程师少做架构选择。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 3.1 | `dotnet new atlas-module` | 生成模块项目、Module 类、Entity、Configuration、DTO、Service、Controller、测试骨架。 | 生成后可直接 restore/build/test。 |
| 3.2 | 实体模板 | 按业务选择 `ITenantEntity`、`ISharedEntity`、`IStoreOnlyEntity`；默认不生成无租户语义实体。 | 模板实体具备明确隔离语义。 |
| 3.3 | EF 映射模板 | EF configuration 放在迁移/模型配置程序集，通过 `ApplyConfigurationsFromAssembly` 加载。 | 不修改 Tenant DbContext 增加 `DbSet`。 |
| 3.4 | 服务模板 | 普通 CRUD 使用 `IRepository<TEntity>`；复杂查询使用 QueryService；写操作支持 UnitOfWork 和领域事件 outbox。 | 模板不出现 `DbContext` 或 `Set<T>`。 |
| 3.5 | Controller 模板 | 不接收可篡改 `TenantId`；租户来自 token 或显式系统流程。 | API 入参默认安全。 |
| 3.6 | 模板验收测试 | CI 生成一个示例模块并编译；另生成违规代码验证 Analyzer 报错。 | 模板和 Analyzer 同时防回归。 |

推荐 PR 拆分：

1. PR-C01：模块模板最小可用版本。
2. PR-C02：租户实体/Repository/Controller 模板完善。
3. PR-C03：模板生成结果编译测试。
4. PR-C04：Analyzer 违规模板测试。
5. PR-C05：模板文档和模块开发指南整合。

## 阶段 4：WebApi/Worker 运行模式拆分

目标：生产部署时 Web 节点只处理 HTTP，Worker 节点处理后台执行平面。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 4.1 | Runtime Mode | 增加 `Atlas:Runtime:Mode` 或等价强类型配置，支持 `WebApi`、`Worker`、`Migration`。 | 不同宿主默认启用不同能力。 |
| 4.2 | WebApi 默认关闭后台执行 | WebApi 默认不启动 consumer、tenant outbox dispatcher、BackgroundJobWorker、RecurringTaskRunner。 | 生产 WebApi 不会误跑后台任务。 |
| 4.3 | Worker 默认执行后台能力 | Worker 默认注册 consumer assembly，启用 outbox dispatcher、后台任务和周期任务。 | 后台执行平面可独立扩容。 |
| 4.4 | 健康检查拆分 | 增加 `/health/live`、`/health/ready`、`/health`。ready 检查 MySQL、Redis、RabbitMQ、后台任务状态。 | 容器和 K8s 可直接使用。 |
| 4.5 | 分布式锁 | 增加 Redis 或数据库实现的 `IDistributedLockProvider`。 | 多实例周期任务不会重复执行。 |

推荐 PR 拆分：

1. PR-D01：运行模式 options 和默认开关。
2. PR-D02：WebApi/Worker 默认职责拆分。
3. PR-D03：健康检查分层。
4. PR-D04：Redis distributed lock provider。

## 阶段 5：租户数据生命周期

目标：租户创建、初始化、迁移、种子数据和版本状态可运维。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 5.1 | LocalSetup 正式 CLI | 将 `Atlas.LocalSetup` 扩展为正式 CLI，支持 init global、create tenant db、seed、reset demo。 | 命令可重复执行，输出不泄露连接串密码。 |
| 5.2 | 租户 schema 状态 | 在 Global 库增加租户 schema version、迁移状态、最后错误、重试次数。 | 每个租户迁移状态可查询。 |
| 5.3 | Migration Job | 提供独立 migration host/container entrypoint，发布时先迁移再启动业务进程。 | 支持 CI/CD 独立运行。 |
| 5.4 | Dry-run | migration job 支持 dry-run，输出计划和影响租户列表。 | 生产变更前可审计。 |
| 5.5 | Seed 分层 | demo seed、本地 seed、生产 seed 分离。 | 生产不会误写 demo 数据。 |

推荐 PR 拆分：

1. PR-E01：LocalSetup CLI 命令结构。
2. PR-E02：租户 schema 状态表和模型。
3. PR-E03：批量迁移执行器。
4. PR-E04：migration job host。
5. PR-E05：seed 分层。

## 阶段 6：安全、权限和审计

目标：从基础 token 和租户管理员策略升级到企业级权限与审计。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 6.1 | RBAC | 增加 Role、Permission、UserRole、RolePermission。 | 租户管理员、平台管理员、门店角色可配置。 |
| 6.2 | 权限缓存失效 | 权限变更后递增 TokenVersion 并清理相关缓存。 | 老 token 不能继续使用旧权限。 |
| 6.3 | 会话增强 | 支持 refresh token、session list、revoke、force logout。 | 泄露 token 可主动失效。 |
| 6.4 | 审计事件模型 | 定义业务审计事件和统一写入入口。 | 关键安全操作和租户管理操作都有审计记录。 |
| 6.5 | 敏感数据策略 | 统一 password、token、secret、phone、email 等字段脱敏。 | 日志中不出现明文敏感信息。 |

推荐 PR 拆分：

1. PR-F01：RBAC 数据模型和迁移。
2. PR-F02：授权策略从硬编码迁移到 RBAC。
3. PR-F03：refresh token 和会话管理。
4. PR-F04：审计事件模型。
5. PR-F05：敏感日志测试。

## 阶段 7：可观测性、打包和发布

目标：让脚手架可以被团队长期使用、发布和升级。

| 编号 | 事项 | 具体动作 | 验收标准 |
| --- | --- | --- | --- |
| 7.1 | OpenTelemetry | 接入 trace、metrics、log correlation，覆盖 HTTP、EF、Redis、RabbitMQ、BackgroundJobs。 | trace id 可贯穿 HTTP 到消息消费。 |
| 7.2 | Docker 资产 | 增加 WebApi、Worker、Migration Dockerfile 和生产 compose 样例。 | 本地可模拟生产拓扑。 |
| 7.3 | NuGet 打包 | 为框架库补 PackageId、Description、RepositoryUrl、License。 | `dotnet pack` 成功，包引用 sample 可运行。 |
| 7.4 | 发布流水线 | tag 触发 NuGet 包、Docker 镜像、release notes。 | dry-run 发布成功。 |
| 7.5 | 版本策略 | 明确 SemVer、migration notes、breaking changes 记录方式。 | 升级路径可预期。 |

推荐 PR 拆分：

1. PR-G01：OpenTelemetry 基线。
2. PR-G02：生产 Docker 资产。
3. PR-G03：NuGet metadata 和 pack。
4. PR-G04：发布流水线 dry-run。
5. PR-G05：版本策略文档。

## 四、优先级排序

### P0：必须先做

1. NuGet source mapping 和 CI 基线。
2. 换行治理，避免无意义 diff。
3. Analyzer 测试夹具。
4. 拆分 tenant runtime 和普通业务服务。
5. 收窄 Analyzer 豁免。

原因：这些工作决定后续改动能否被稳定 review，也决定业务代码能否被编译期边界保护。

### P1：脚手架可用性

1. `dotnet new atlas-module`。
2. 模块生成结果编译测试。
3. QueryService/Repository 模板。
4. WebApi/Worker 运行模式默认值。
5. Redis 分布式锁。

原因：这些工作直接影响开发者日常写业务模块的体验和生产部署安全。

### P2：生产生命周期

1. LocalSetup 正式 CLI。
2. 租户 schema version 和 migration job。
3. 健康检查分层。
4. Docker 资产。
5. seed 分层。

原因：这些工作让系统从“能跑 demo”升级为“能被环境和发布流程管理”。

### P3：企业能力和发布治理

1. RBAC。
2. 会话增强。
3. 审计事件标准化。
4. OpenTelemetry。
5. NuGet 打包和发布流水线。

原因：这些能力提升企业可用性，但应建立在前面边界和运行时职责清晰之后。

## 五、推荐执行顺序

1. 完成 P0 工程门禁：source mapping、换行治理、CI、Analyzer 测试。
2. 拆分 tenant runtime，确保 outbox/inbox/provisioning 等基础设施代码不和普通业务服务混在同一豁免域。
3. 收窄 Analyzer 豁免，让业务层违规直接不能编译。
4. 生成模块模板，并用 CI 验证模板输出。
5. 固化 WebApi/Worker 默认运行模式。
6. 补生产多实例必需的 Redis 分布式锁和健康检查分层。
7. 推进租户迁移生命周期和 LocalSetup CLI。
8. 最后补 RBAC、会话、审计、OpenTelemetry、打包发布。

## 六、每阶段统一验收命令

基础命令：

```bash
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj --no-build
dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-build
dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build
```

模板阶段额外验收：

```bash
dotnet new atlas-module -n Demo.Module -o .tmp/Demo.Module
dotnet build .tmp/Demo.Module
```

Analyzer 阶段额外验收：

1. 业务项目中直接注入 `AtlasTenantDbContext` 应触发 `ATL001`。
2. 业务项目中调用 `DbContext.Set<T>()` 应触发 `ATL002`。
3. 业务项目中调用 `ExecuteSql*` 或 `FromSql*` 应触发 `ATL003`。
4. Data/runtime 批准项目中的基础设施访问应通过，并由单元测试覆盖租户谓词。

## 七、风险和处置

| 风险 | 影响 | 处置 |
| --- | --- | --- |
| 过早收窄 Analyzer 豁免 | 现有 runtime 代码大量报错，PR 难 review。 | 先拆 runtime，再收窄。 |
| 模板过度复杂 | 新人生成后不知道该改哪里。 | 先做最小 CRUD 模板，再加事件/后台任务模板。 |
| SQL executor 设计过窄 | 维护任务无法表达复杂 SQL。 | 保留 approved infrastructure API，但要求命名明确并有测试。 |
| 分布式锁实现不严谨 | 多实例周期任务重复执行。 | 优先使用 Redis `SET NX PX` 加 token 校验释放，或成熟库。 |
| 示例业务继续留在核心 | 框架边界模糊，后续模板和模块职责混乱。 | 将 Product/Order 迁到 sample 或独立 module。 |
| warning 一次性清零 | PR 过大且容易引入行为变化。 | 分批治理，先 src 高风险 warning。 |

## 八、自我验证记录

### 自检 1：租户隔离闭环

本计划同时覆盖实体语义、Repository 查询、写入校验、Analyzer 阻断、raw SQL 入口和 outbox/inbox 幂等。它没有把“不声明 DbSet”当作唯一防线，也没有建议增加 `UnscopedSet`。结论：隔离闭环完整。

### 自检 2：执行顺序

计划先做工程基线，再拆分 tenant runtime，之后收窄 Analyzer。如果先收窄 Analyzer，当前 `Atlas.Services.Tenant` 中合法的 outbox/inbox/provisioning 代码会被误伤。结论：顺序合理。

### 自检 3：脚手架目标

计划最终让业务开发者通过模板生成模块，并默认使用 Repository/QueryService/Service/Controller。底层 DbContext、消息幂等、后台任务 claim、迁移和部署交给框架能力。结论：符合“让工程师专注业务代码”的目标。

### 自检 4：可验收性

每个阶段都有命令、测试或结构性验收，例如 warning 数量、Analyzer 正反例、模板 build、运行模式默认值、健康检查端点、迁移状态表。结论：不是纯架构口号，可按 PR 执行。

### 自检 5：与现有文档一致性

本计划与 `scaffold_pr_roadmap.md` 的 PR 分阶段方向一致，并补充了 `tenant_data_isolation_best_practices.md` 中的数据访问边界治理细节。结论：新增文档是对现有路线图的定稿补充，不是另起一套方向。

## 九、定稿结论

Atlas 当前的技术方向正确，已经具备多租户脚手架的核心骨架。接下来最重要的不是继续增加业务 demo，而是先把工程门禁和租户边界收紧。

最终执行判断：

1. 先完成 P0，避免后续所有 PR 被 warning、换行和边界豁免拖累。
2. 再做 P1，让正确写法通过模板成为默认路径。
3. 然后补 P2/P3，把本地 demo 能力升级为生产部署、运维和发布能力。

本计划作为当前阶段定稿版本，后续实际开发应按 P0 到 P3 顺序拆 PR 推进。
