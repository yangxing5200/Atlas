# Atlas 系统架构改进计划

本文基于当前 `main` 分支最新代码制定，当前 HEAD 为 `495f45c Merge pull request #27 from yangxing5200/codex/scaffold-execution`。目标是把现有 Atlas 从“功能完整的多租户底座”继续推进到“边界更清晰、运行时更可拆、生产维护成本更低的框架”。

## 一、当前基线判断

已确认的正向基线：

1. `dotnet build Atlas.sln --no-restore` 当前通过，0 warning，0 error。
2. WebApi、Worker、MigrationJob 已经有独立宿主，且 `Atlas:Runtime:Mode` 已开始承担运行模式区分。
3. 租户数据主路径已经收敛到 Repository、`ScopedSet`、`EntityScopeFilter`。
4. `Atlas.Analyzers` 已接入构建，可阻断普通业务层直接引用租户 DbContext、`DbContext.Set<T>()` 和 EF raw SQL。
5. 缓存、消息、后台任务、授权、数据脱敏、模块系统、健康检查和 OpenTelemetry 已有基础实现。

仍需改进的架构问题：

1. DI 组合层过重，`Atlas.Extensions.DependencyInjection` 同时绑定 Web、数据、缓存、消息、安全、后台任务、日志和观测能力。
2. `Atlas.Data.Abstractions` 泄漏 EF Core，抽象层与具体 ORM 绑定偏深。
3. 租户数据层仍直接依赖 Global 数据、缓存和 HTTP 身份语义，Worker/Migration 等非 HTTP 场景靠约定绕开。
4. Analyzer 边界虽然有效，但仍依赖 namespace 和例外清单，缺少更硬的项目级 runtime 隔离。
5. 租户 EF 模型通过字符串加载迁移程序集，并统一移除外键约束，长期需要更显式的模型和完整性治理策略。
6. 缓存接口保留同步 API，内部存在 sync-over-async，Redis/Hybrid 场景下会增加线程池压力。
7. `UserService` 职责过大，登录、会话、用户管理、角色门店分配、安全审计都集中在一个服务内。
8. 包版本和依赖治理需要持续收敛，当前 .NET 8 项目中混用多个 Microsoft.Extensions 主版本。

## 二、改进原则

1. 保持兼容入口：现有 `builder.AddAtlasWebApi(...)` 和 `services.AddAtlasCore(...)` 不应在第一阶段破坏。
2. 先拆边界，再收紧规则：先提供新入口、新项目或新接口，再逐步收窄 Analyzer 和依赖。
3. 所有租户数据访问最终都必须能说明 `TenantId` 和门店范围如何生效。
4. 普通业务代码只接触 Repository、QueryService、Service、领域事件、后台任务客户端和模块声明。
5. 运行时基础设施可以触碰底层 EF，但必须被项目、namespace、接口和测试共同圈住。
6. 每个计划必须能用构建、测试、静态搜索或架构测试验收。

## 三、计划总览

| 计划 | 优先级 | 目标 | 主要依赖 |
| --- | --- | --- | --- |
| PLAN-01 | P0 | 拆轻 DI 组合层和宿主注册入口 | 无 |
| PLAN-02 | P0 | 移除 Data.Abstractions 对 EF Core 的直接依赖 | PLAN-01 可并行 |
| PLAN-03 | P0 | 抽象租户执行上下文和连接目录 | PLAN-01 |
| PLAN-04 | P0 | 项目级硬化租户 runtime 边界 | PLAN-01、PLAN-03 |
| PLAN-05 | P1 | 治理租户 EF 模型加载和数据完整性策略 | PLAN-03 |
| PLAN-06 | P1 | 缓存 API async 化和分布式运行治理 | PLAN-01 |
| PLAN-07 | P1 | 拆分 UserService 领域职责 | PLAN-02、PLAN-03 |
| PLAN-08 | P2 | 包版本、发布和架构测试治理 | 可并行 |

## 四、详细计划

### PLAN-01：拆轻 DI 组合层和宿主注册入口

问题：

`Atlas.Extensions.DependencyInjection` 当前引用大量实现项目和第三方包，是系统事实上的总装配层。短期能降低宿主复杂度，但长期会让任何宿主或模块都被迫携带完整运行时依赖。

实施动作：

1. 将当前私有注册方法逐步提升为可组合的公开入口，例如 `AddAtlasDatabase`、`AddAtlasCachingRuntime`、`AddAtlasMessagingRuntime`、`AddAtlasTenantRuntime`、`AddAtlasObservability`。
2. 保留 `AddAtlasCore` 作为兼容门面，由它调用更小的注册入口。
3. 将 WebApi 专属能力留在 `AddAtlasWebApi`，例如 Controller、Swagger、认证中间件、Web 健康检查响应。
4. 将 Worker/Migration 专属能力从通用 core 注册中显式拆分，运行模式只负责默认开关，不负责隐藏依赖边界。
5. 中长期拆分为更细包或项目，例如 `Atlas.Hosting.WebApi`、`Atlas.Hosting.Worker`、`Atlas.Hosting.Migration`。

验收标准：

1. 现有 `src/Atlas.WebApi/Program.cs` 和 `samples/Atlas.Sample.WebApi/Program.cs` 仍可使用 `AddAtlasWebApi` 启动。
2. 现有 `src/Atlas.Worker/Program.cs` 仍可使用 `AddAtlasCore` 或新的 Worker 注册入口启动。
3. 新增单元测试覆盖 WebApi、Worker、Migration 三种 runtime mode 的关键服务注册差异。
4. 能写出一个只注册缓存或只注册数据库的最小测试宿主，不需要启动消息、Swagger 或后台任务。
5. `dotnet build Atlas.sln --no-restore` 通过。

风险与缓解：

1. 风险：一次性拆包容易造成大范围引用变更。  
   缓解：第一阶段只拆公开注册方法，不立即拆 csproj。
2. 风险：运行模式默认值变化导致生产行为改变。  
   缓解：保留旧入口语义，用测试锁定 WebApi/Worker 默认能力。

### PLAN-02：移除 Data.Abstractions 对 EF Core 的直接依赖

问题：

`Atlas.Data.Abstractions` 当前引用 EF Core，`QueryBuilder` 也直接依赖 EF 的 Include、ThenInclude 和异步执行扩展。这样会让服务抽象层、测试和未来非 EF 查询实现都被 EF 绑定。

实施动作：

1. 在抽象层定义更小的查询接口，例如 `IQueryBuilder<TEntity>` 或 `IRepositoryQuery<TEntity>`，只保留业务层需要的 Where、OrderBy、Skip、Take、Select、ToListAsync、FirstOrDefaultAsync、CountAsync、AnyAsync。
2. 将 EF 具体实现移动到 `Atlas.Data.EntityFramework` 或 `Atlas.Data.Tenant`，例如 `EfQueryBuilder<TEntity>`。
3. 对 `Include`、`ThenInclude` 这类 EF 特性建立专门策略：普通业务尽量用投影 QueryService，确需 Include 的位置放在专用 Repository 或 EF runtime 实现中。
4. 清理 `IUnitOfWork.cs` 中无意义的 EF using。
5. 保留过渡类型或适配器，避免一次性修改全部服务。

验收标准：

1. `src/Atlas.Data.Abstractions/Atlas.Data.Abstractions.csproj` 不再包含 `Microsoft.EntityFrameworkCore` 包引用。
2. `src/Atlas.Data.Abstractions` 下不再出现 `using Microsoft.EntityFrameworkCore`。
3. `src/Atlas.Services` 普通服务不需要引用 EF Core 仅为了调用查询执行方法。
4. 现有 Repository 行为保持不变，租户、门店和软删除过滤仍在创建查询时生效。
5. Core/Data/Services 单元测试通过。

风险与缓解：

1. 风险：现有服务代码依赖 `Include`。  
   缓解：先统计 Include 使用点，逐个迁移到 QueryService 或专用 Repository。
2. 风险：抽象过度导致查询能力不足。  
   缓解：只抽当前业务需要的最小集合，复杂查询不强行通用化。

### PLAN-03：抽象租户执行上下文和连接目录

问题：

当前 `CurrentIdentity` 直接从 `IHttpContextAccessor` 读取声明，`TenantDbConnProvider` 同时依赖 `ICurrentIdentity`、`AtlasGlobalDbContext`、`IConfiguration` 和缓存。这样 Web 请求路径很顺，但 Worker、Migration、后台任务和登录前流程需要显式 tenantId 重载或特殊身份实现。

实施动作：

1. 引入统一的 `IAtlasExecutionContext` 或强化现有 `ICurrentIdentity`，让 HTTP、Worker、Migration、系统任务都能设置或读取同一套身份、租户和门店上下文。
2. 将 HTTP claims 解析实现留在 WebApi 组合层，非 HTTP 宿主使用可显式设置的系统上下文实现。
3. 抽出 `ITenantConnectionCatalog`，负责按 tenantId 返回主库、只读库和报表库连接信息。
4. 将基于 GlobalDbContext 的连接目录实现放在 Global 或 composition 层，Tenant 数据层只依赖抽象。
5. 将缓存作用域和租户连接解析统一绑定到执行上下文，避免同一流程里身份和 cache scope 不一致。

验收标准：

1. `Atlas.Data.Tenant` 不再直接引用 `Atlas.Data.Global`。
2. `Atlas.Data.Tenant` 不再需要 `Microsoft.AspNetCore.Http.Abstractions`。
3. WebApi 请求中仍能从 token claims 解析 tenantId、storeId、userId。
4. Worker、MigrationJob、后台任务可以在无 HttpContext 的情况下设置租户执行上下文并访问租户库。
5. 新增测试覆盖同一后台任务依次处理两个租户时，连接字符串和缓存 scope 不串租。

风险与缓解：

1. 风险：身份上下文改造触及范围广。  
   缓解：先新增上下文抽象和适配器，再逐步替换直接依赖。
2. 风险：登录前流程没有完整身份。  
   缓解：保留显式 tenantId 查询重载，作为受控系统流程入口。

### PLAN-04：项目级硬化租户 runtime 边界

问题：

Analyzer 目前已经能保护普通业务代码，但 runtime 例外仍主要靠 namespace 判定。随着 runtime 代码增加，namespace 约定的保护力度不如项目级边界清晰。

实施动作：

1. 将 `Atlas.Services.Tenant.Runtime` 下的 outbox、inbox、migration、provisioning、background job runtime 逐步迁入独立项目，例如 `Atlas.TenantRuntime` 或 `Atlas.Services.Tenant.Runtime`。
2. 普通 `Atlas.Services.Tenant` 仅保留业务服务和业务抽象，不直接引用 `AtlasTenantDbContext` 或 `ITenantDbContextFactory`。
3. Analyzer 从 namespace 例外逐步调整为项目级例外，并保留少量明确 namespace 兼容。
4. 为 outbox/inbox/runtime SQL 建立专用接口，普通业务只能依赖 `ITenantDomainEventOutbox`、`ITenantConsumerRuntime` 等抽象。
5. 增加架构测试，验证业务项目不引用 runtime 实现项目，runtime 项目不被 API 直接当作业务服务使用。

验收标准：

1. 普通业务服务项目中直接注入 `AtlasTenantDbContext` 触发 `ATL001`。
2. 普通业务服务项目中调用 `DbContext.Set<T>()` 触发 `ATL002`。
3. 普通业务服务项目中调用 `FromSql*` 或 `ExecuteSql*` 触发 `ATL003`。
4. Runtime 项目中的每个租户库查询、更新、删除都有显式 tenantId 条件或通过受控 Repository 进入。
5. Analyzer 测试覆盖普通业务失败样例和 runtime 合法样例。

风险与缓解：

1. 风险：移动项目导致循环引用暴露。  
   缓解：先移动抽象到 `Atlas.Services.Tenant` 或 `Atlas.Messaging.Abstractions`，实现放 runtime。
2. 风险：Analyzer 收紧过快影响开发。  
   缓解：分两步收紧，先 warning 或测试证明，再升级为 error。

### PLAN-05：治理租户 EF 模型加载和数据完整性策略

问题：

`AtlasTenantDbContext` 当前通过 `Assembly.Load("Atlas.Data.Tenant.Migrations")` 加载配置，并统一移除外键约束。字符串加载对包拆分和模块扩展不够友好，移除 FK 也会把完整性更多交给应用层。

实施动作：

1. 通过 options 或模块目录显式传入租户模型配置程序集，替代硬编码字符串加载。
2. 为模块实体配置提供稳定注册方式，例如模块声明 `EntityConfigurationAssemblies`。
3. 将 `RemoveAllForeignKeyConstraints` 改为显式策略：禁用、仅租户库禁用、仅指定实体禁用。
4. 增加数据完整性巡检任务或 CLI，例如检查孤儿 UserStore、UserRole、Order 明细等。
5. 对必须无 FK 的表补充应用层一致性测试和唯一索引策略。

验收标准：

1. 租户 DbContext 不再依赖硬编码 `"Atlas.Data.Tenant.Migrations"` 字符串。
2. 新模块可通过模块声明加入 EF configuration，不需要修改 DbContext。
3. FK 移除策略有配置项和文档说明。
4. 关键租户表的唯一约束均包含 `TenantId`。
5. 至少一个完整性巡检命令或测试能发现跨租户/孤儿引用样例。

风险与缓解：

1. 风险：改变模型加载方式影响 migration snapshot。  
   缓解：先引入新方式并保持旧方式兼容，迁移稳定后再移除旧路径。
2. 风险：恢复 FK 不适合分库或历史数据。  
   缓解：默认仍可保持无 FK，但必须有显式策略和巡检补位。

### PLAN-06：缓存 API async 化和分布式运行治理

问题：

缓存服务曾保留同步 API，内部通过 `.GetAwaiter().GetResult()` 包装异步 provider。对于 Redis 和 Hybrid 缓存，这会增加线程池阻塞风险，也让调用方误以为远程缓存可以安全同步调用。由于当前仍是脚手架阶段，可以直接删除同步缓存 API，不需要兼容过渡。

实施动作：

1. 删除 `ICacheService` 同步方法，统一缓存抽象只保留 async API。
2. 删除 `ITokenCacheService` 同步缓存兼容方法，token version 和 session blacklist 统一异步访问。
3. 业务路径、DataScope、权限和 token 相关调用优先迁移到 async API。
4. 缩小 raw key API 使用范围，业务缓存必须优先使用 `CacheKeyDefinition`。
5. 为 Redis/Hybrid 模式补充分布式失效和 L1 清理测试。
6. 明确 `IDistributedLockProvider` 的生产默认策略，避免多实例仍使用内存锁。

验收标准：

1. `ICacheService` 不暴露不以 `Async` 结尾的缓存操作方法。
2. `ITokenCacheService` 不暴露同步 token version/session cache 方法。
3. `rg "GetAwaiter\\(\\)\\.GetResult" src/Atlas.Infrastructure.Caching` 无命中。
4. Redis 或 Hybrid 集成测试覆盖 L1 提升、远程失效和 tag version 失效。
5. 生产配置使用 Redis/数据库分布式锁时，多实例周期任务不会重复执行。
6. 缓存键定义覆盖租户、门店、用户作用域的关键业务缓存。

风险与缓解：

1. 风险：删除同步 API 影响调用方。  
   缓解：当前项目仍是脚手架，直接删除 API 并通过构建暴露所有调用点。
2. 风险：全部 async 迁移导致调用链扩散。  
   缓解：优先迁移 Redis/Hybrid 真实远程路径，内存-only 测试路径后移。

### PLAN-07：拆分 UserService 领域职责

问题：

`UserService` 当前超过 1200 行，集中处理登录、刷新 token、切换门店、用户 CRUD、角色门店分配、密码、安全审计和会话管理。它已经成为变更高耦合点。

实施动作：

1. 拆出 `AuthService`：登录、刷新 token、切换门店、退出登录。
2. 拆出 `UserManagementService`：创建、更新、删除、状态、解锁、密码重置。
3. 拆出 `UserAssignmentService`：门店分配、角色分配。
4. 拆出 `UserSessionService`：活跃会话、强制退出、session revoke。
5. 保留 `IUserService` 兼容门面一段时间，由门面委托到新服务。
6. 将安全审计和操作日志写入路径统一成小型领域服务，避免每个方法散落重复审计代码。

验收标准：

1. 对外 Controller 路由、请求、响应保持兼容。
2. `IUserService` 旧接口在过渡期仍可注入和调用。
3. 每个新服务构造函数依赖数量明显小于当前 `UserService`。
4. 单个新服务文件目标不超过 400 行，复杂流程可用私有协作类继续拆分。
5. 登录、刷新、切店、创建用户、分配角色、强制退出均有独立测试。

风险与缓解：

1. 风险：拆分时改变认证安全行为。  
   缓解：先补 characterization tests，锁定现有响应和审计副作用。
2. 风险：事务边界被拆散。  
   缓解：每个用例服务明确拥有自己的 UnitOfWork 边界，跨服务只通过应用服务协调。

### PLAN-08：包版本、发布和架构测试治理

问题：

当前项目基于 .NET 8，但中央包版本中存在 Microsoft.Extensions 8、9、10 混用。虽然当前可构建，但长期可能带来运行时行为差异、依赖冲突和包发布困扰。

实施动作：

1. 盘点所有 Microsoft.Extensions、EF Core、Serilog、OpenTelemetry、MassTransit 包版本。
2. 制定版本策略：运行时主线优先跟随 .NET 8 LTS，个别高版本必须记录原因。
3. 增加依赖治理文档和检查脚本，禁止无说明引入跨主版本依赖。
4. 增加架构测试或脚本，验证关键项目引用方向，例如 Core 不依赖 Infrastructure，Abstractions 不依赖 EF。
5. 补齐 pack/release dry-run，确保拆分后的包边界可发布。

验收标准：

1. `Directory.Packages.props` 中跨主版本依赖有明确说明或已收敛。
2. `dotnet restore Atlas.sln` 和 `dotnet build Atlas.sln --no-restore` 无新增依赖 warning。
3. 架构测试能阻断 `Atlas.Data.Abstractions` 重新引用 EF Core。
4. `dotnet pack` 对可打包项目成功。
5. 发布说明记录 breaking changes、migration notes 和升级路径。

风险与缓解：

1. 风险：包版本收敛引发编译或运行时变化。  
   缓解：单独 PR 处理版本收敛，不与架构重构混合。
2. 风险：架构测试过严影响模板和测试项目。  
   缓解：测试、samples、tools 可有单独豁免清单，但必须显式声明。

## 五、推荐执行顺序

第一批 P0：

1. PLAN-01 拆轻 DI 注册入口，保留兼容门面。
2. PLAN-02 移除抽象层 EF 依赖，先做适配器。
3. PLAN-03 抽出租户执行上下文和连接目录。
4. PLAN-04 将 tenant runtime 项目级隔离，并收紧 Analyzer。

第二批 P1：

1. PLAN-05 模型加载和完整性治理。
2. PLAN-06 缓存 async 化和分布式锁生产治理。
3. PLAN-07 拆分 UserService。

第三批 P2：

1. PLAN-08 包版本、架构测试、pack/release dry-run。

## 六、两次自我技术性验证

### 技术验证 1：静态事实核验

验证目的：确认计划针对的是当前代码真实存在的问题，而不是文档层面的假设。

核验点：

1. `Atlas.Extensions.DependencyInjection.csproj` 同时引用 BackgroundTasks、Data.Global、Data.Tenant、RabbitMQ、Logging、Security、Services、Services.Tenant，证明 PLAN-01 的组合层拆轻是必要的。
2. `Atlas.Data.Abstractions.csproj` 引用 `Microsoft.EntityFrameworkCore`，且 `QueryBuilder.cs` 使用 EF Core API，证明 PLAN-02 的抽象层净化是必要的。
3. `TenantDbConnProvider` 依赖 `AtlasGlobalDbContext` 和缓存，`CurrentIdentity` 依赖 `IHttpContextAccessor`，证明 PLAN-03 的执行上下文和连接目录抽象是必要的。
4. `TenantBoundaryAnalyzer` 已有 `ATL001` 到 `ATL005`，但 runtime 例外仍依赖 namespace，证明 PLAN-04 应该从 namespace 约定升级到项目级边界。
5. `AtlasTenantDbContext` 使用 `Assembly.Load("Atlas.Data.Tenant.Migrations")`，并调用 `RemoveAllForeignKeyConstraints`，证明 PLAN-05 直接对应当前实现。
6. `CacheService` 曾通过同步 API 包装异步 provider，证明 PLAN-06 的 async-only 方向正确。
7. `UserService.cs` 超过 1200 行并承担多类用例，证明 PLAN-07 的拆分具备明确收益。
8. `Directory.Packages.props` 中存在 Microsoft.Extensions 8、9、10 主版本混用，证明 PLAN-08 的依赖治理有现实输入。

验证结论：

计划中的每一项都能在当前仓库找到对应事实依据，没有要求先推翻现有架构。计划采用兼容门面、适配器和分阶段收紧，因此可以逐步落地。

### 技术验证 2：可行性和正确性核验

验证目的：确认计划之间没有明显循环依赖，且每项验收可以被自动化验证。

核验点：

1. PLAN-01 先拆注册入口但保留旧门面，因此不会阻塞 WebApi、Worker、MigrationJob 现有启动方式。
2. PLAN-02 可先引入抽象接口和 EF 适配器，再迁移服务调用，不需要一次性替换所有查询代码。
3. PLAN-03 先抽连接目录和执行上下文，不立即改变租户数据库结构，因此能与 PLAN-04 并行准备。
4. PLAN-04 在 runtime 独立后再收紧 Analyzer，避免合法基础设施代码被误杀。
5. PLAN-05 涉及模型和约束策略，排在 P1，等上下文和 runtime 边界稳定后执行，降低 migration 风险。
6. PLAN-06 在脚手架阶段直接删除同步缓存入口，避免后续调用方依赖错误模型。
7. PLAN-07 通过兼容门面保持 Controller 和外部调用不变，可先补测试再拆实现。
8. PLAN-08 可并行做检查脚本和文档，不阻塞业务架构拆分。

自动化验收可行性：

1. 构建验收：`dotnet build Atlas.sln --no-restore`。
2. 单元测试验收：Core、Data、Services、Analyzer 测试项目。
3. 静态搜索验收：`rg "using Microsoft.EntityFrameworkCore" src/Atlas.Data.Abstractions`、`rg "GetAwaiter\\(\\)\\.GetResult" src/Atlas.Infrastructure.Caching`。
4. 项目引用验收：检查 csproj 中禁止的 `ProjectReference` 和 `PackageReference`。
5. Analyzer 验收：新增正反例代码片段，验证 `ATL001`、`ATL002`、`ATL003`、`ATL004`、`ATL005`。

验证结论：

计划顺序从低风险组合层和抽象层开始，再进入运行时边界、模型治理和业务服务拆分，依赖关系可控。每个计划都有可执行的验收信号，具备落地可行性。

## 七、本次生成后的校验记录

本次创建文档后已完成以下校验：

1. 文档结构校验：`PLAN` 章节数量为 8，`验收标准` 章节数量为 8，技术验证章节数量为 2。
2. 静态事实校验：已用源码搜索确认组合层依赖、抽象层 EF 引用、HTTP 身份依赖、字符串加载迁移程序集、移除外键约束、缓存 sync-over-async、`UserService` 过大、包版本混用等事实依据均存在。
3. 格式校验：`git diff --check` 通过。
4. 构建校验：早期批次 `dotnet build Atlas.sln --no-restore` 通过；最终执行后的完整构建、测试和 pack 结果见下方最终测试报告。

## 八、执行记录

### 2026-06-09：PLAN-01 第一批执行

已完成：

1. 将 `AddAtlasRuntimeOptions` 提升为公开组合入口，用于单独注册并解析 runtime/cache/messaging options。
2. 将 `AddAtlasDatabase`、`AddAtlasIdentity`、`AddAtlasCache`、`AddAtlasBackgroundTasks`、`AddAtlasOpenTelemetry` 提升为公开组合入口。
3. 新增 `AddAtlasMessagingRuntime` 公开组合入口，保留 `AddAtlasCore` 兼容门面语义。
4. `AddAtlasIdentity` 现在会自行确保 `IHttpContextAccessor` 注册，单独使用时不再依赖调用方记住前置注册。
5. 为可组合入口补充 `RuntimeModeRegistrationTests`，覆盖 runtime options、identity、database、cache 和 NoOp messaging runtime 的最小注册行为。

本批验收：

1. `dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-restore` 通过，31 个测试全部通过。
2. `dotnet build Atlas.sln --no-restore` 通过，0 error。当前 Integration.Tests 存量 warning 仍会打印，未在本批处理。
3. 现有 `AddAtlasCore`、`AddAtlasWebApi` 调用路径保持兼容。

下一批建议：

1. 继续 PLAN-01：为 Worker/Migration 增加更语义化的宿主注册门面。
2. 启动 PLAN-02：设计 `IRepositoryQuery<TEntity>` 或同等抽象，为移除 `Atlas.Data.Abstractions` 的 EF Core 依赖做适配层。

### 2026-06-09：PLAN-01 第二批执行

已完成：

1. 新增 `AddAtlasWorker` 语义化注册门面。未显式配置 `Atlas:Runtime:Mode` 时，默认按 Worker 模式启用 messaging outbox dispatcher、background job worker 和 recurring task runner。
2. 新增 `AddAtlasMigration` 语义化注册门面。未显式配置 `Atlas:Runtime:Mode` 时，默认按 Migration 模式关闭 messaging consumers、tenant outbox dispatcher、background job worker 和 recurring task runner。
3. 为 Worker/Migration 门面补充运行模式注册测试，验证默认执行平面符合预期。

本批测试报告：

| 命令 | 结果 | 通过数 | 备注 |
| --- | --- | ---: | --- |
| `dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-restore` | 通过 | 33 | 覆盖新增 Worker/Migration 注册门面。 |
| `dotnet test tests/Atlas.Analyzers.Tests/Atlas.Analyzers.Tests.csproj --no-restore` | 通过 | 15 | Analyzer 边界测试通过。 |
| `dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj --no-restore` | 通过 | 44 | Core 单元测试通过。 |
| `dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-restore` | 通过 | 217 | Data 单元测试通过。 |
| `dotnet build Atlas.sln --no-restore` | 通过 | - | 0 error；Integration.Tests 仍有 75 个存量 warning。 |

未执行：

1. `tests/Atlas.Integration.Tests` 的集成测试未在本批运行；本批只通过全量 build 编译到该项目。

下一批建议：

1. PLAN-01 收尾：将 `src/Atlas.Worker/Program.cs` 和 `src/Atlas.MigrationJob/Program.cs` 切换到新语义门面，保持行为一致。
2. PLAN-02 启动：先增加抽象查询接口和 EF 适配器，不立即移除旧 `QueryBuilder`。

### 2026-06-09：PLAN-01 收尾执行

已完成：

1. `src/Atlas.Worker/Program.cs` 已从通用 `AddAtlasCore` 切换为 `AddAtlasWorker`。
2. `src/Atlas.MigrationJob/Program.cs` 已从通用 `AddAtlasCore` 切换为 `AddAtlasMigration`。
3. 现有 `AddAtlasCore` 和 `AddAtlasWebApi` 兼容路径仍保留。

本批测试报告：

| 命令 | 结果 | 通过数 | 备注 |
| --- | --- | ---: | --- |
| `dotnet build Atlas.sln --no-restore` | 通过 | - | 0 warning，0 error。 |
| `dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build` | 通过 | 33 | Worker/Migration 注册测试继续通过。 |

PLAN-01 当前状态：

1. 可组合注册入口已建立。
2. Worker/Migration 语义化门面已建立并被实际宿主使用。
3. 后续如果继续拆包，可在不改变宿主语义的前提下，把这些公开入口迁移到更细的 hosting 项目。

下一批建议：

1. 启动 PLAN-02：增加 `IRepositoryQuery<TEntity>` 或同等抽象，并实现 EF 适配器。
2. 先保持现有 `QueryBuilder<TEntity>` 兼容，逐步迁移服务和测试，再移除 `Atlas.Data.Abstractions` 对 EF Core 的直接引用。

### 2026-06-09：PLAN-02 执行

已完成：

1. 将抽象层 `QueryBuilder<TEntity>` 替换为 `IQueryBuilder<TEntity>`，仅保留业务查询组合所需的表达式 API 和异步物化方法。
2. 新增 `EfQueryBuilder<TEntity>`，把 EF Core 的 `Include`、`ThenInclude`、排序、分页和异步执行实现迁移到 `Atlas.Data.EntityFramework`。
3. 将 `IRepository<TEntity>`、全局仓储和租户仓储的查询返回类型统一迁移为 `IQueryBuilder<TEntity>`。
4. 移除 `Atlas.Data.Abstractions` 对 `Microsoft.EntityFrameworkCore` 的包引用，`IUnitOfWork` 同步清理无效 EF using。
5. 保持租户查询行为不变：仓储仍在返回 query builder 前应用 tenantId、store scope、soft-delete 过滤。

本批测试报告：

| 命令 | 结果 | 通过数 | 备注 |
| --- | --- | ---: | --- |
| `dotnet build Atlas.sln --no-restore` | 通过 | - | 0 warning，0 error。 |
| `dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-build` | 通过 | 217 | 覆盖仓储、租户查询和数据模型相关路径。 |
| `dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build` | 通过 | 33 | DI/runtime 注册测试继续通过。 |
| `rg 'using Microsoft.EntityFrameworkCore|PackageReference Include="Microsoft.EntityFrameworkCore"' .\src\Atlas.Data.Abstractions -g '*.cs' -g '*.csproj'` | 通过 | - | 无输出，抽象层不再直接依赖 EF Core。 |

PLAN-02 当前状态：

1. `Atlas.Data.Abstractions` 已恢复为 provider-agnostic 抽象层。
2. EF Core 细节被收拢到 `Atlas.Data.EntityFramework` 的适配器实现。
3. 后续如需接入非 EF 查询 provider，可以基于 `IQueryBuilder<TEntity>` 增加实现，不再修改业务抽象。

下一批建议：

1. 执行 PLAN-03：抽象租户执行上下文和连接目录。
2. 保持现有 `CurrentIdentity`、`TenantDbConnProvider` 兼容注册，先通过 adapter 降低调用方迁移风险。

### 2026-06-09：PLAN-03 至 PLAN-08 执行汇总

PLAN-03 已完成：

1. 新增 `ITenantExecutionContext`，让租户执行上下文从 HTTP 身份实现中抽象出来。
2. 新增 `ITenantConnectionDirectory` 和 `TenantConnectionDirectory`，将租户连接信息读取、缓存和校验从 `TenantDbConnProvider` 中拆出。
3. `TenantDbConnProvider` 保留兼容门面语义，只负责根据当前租户或显式租户请求连接目录。
4. 补充 `TenantDbConnProviderTests`，覆盖当前租户、缺失租户、只读连接和报表连接 fallback。

PLAN-04 已完成：

1. 新增 `Atlas.Services.Tenant.Runtime` 项目，将租户授权 runtime、outbox/inbox、后台任务、租户迁移和 provisioning 运行时代码迁入独立程序集。
2. 收窄 `Atlas.Services.Tenant` 的依赖，让业务服务程序集不再携带 runtime 基础设施依赖。
3. Analyzer 不再按 namespace 放行 runtime 代码，改为按 `Atlas.Services.Tenant.Runtime` 程序集显式放行。
4. Analyzer 测试新增业务程序集反例和 runtime 程序集正例。

PLAN-05 已完成：

1. 将租户实体配置从迁移项目移动到 `Atlas.Data.Tenant/EntityConfigurations`。
2. `AtlasTenantDbContext` 改为从自身程序集加载模型配置，不再字符串加载 `Atlas.Data.Tenant.Migrations`。
3. 用 `ApplyForeignKeyConstraintPolicy` 替代运行时默认移除外键约束名，保留测试场景的约束抑制能力。
4. `EnterpriseSecurityModelTests` 新增配置程序集和外键 delete behavior 验证。

PLAN-06 已完成：

1. `ICacheService` 增加 raw-key async API：`GetAsync`、`SetAsync`、`RemoveAsync`、`ExistsAsync`。
2. 删除 `ICacheService` 和 `CacheService` 的同步缓存 API，避免脚手架形成 sync-over-async 兼容包袱。
3. 删除 `ITokenCacheService` 同步 token version/session cache 方法，安全撤销相关缓存统一走 async。
4. Hybrid cache pattern 查询去除 `.Result`。
5. Token、RBAC、Entitlement、RefreshToken、TokenVersion middleware 等远程缓存路径迁移到 async API。
6. 补充缓存、RBAC 和 token 相关测试。

PLAN-07 已完成：

1. 保留 `IUserService` 兼容门面，外部 Controller 和调用方无需改路由、请求或响应。
2. 拆出 `IUserAuthService`、`IUserManagementService`、`IUserAssignmentService`、`IUserSessionService`。
3. 新增 `UserStoreAccessService`、`UserSecurityAuditWriter`、`UserLoginAuditWriter`、`UserPasswordService` 收敛共享门店访问、审计和密码会话失效逻辑。
4. 新服务文件行数均低于 400 行：Auth 387、Management 339、Assignment 171、Session 202、Password 195、Facade 133。
5. `UserServiceFacadeTests` 覆盖登录、刷新、切店、创建用户、分配角色、强制退出等关键兼容入口。

PLAN-08 已完成：

1. `Directory.Packages.props` 收敛 Microsoft.Extensions runtime 包到 .NET 8 主线。
2. 对 AutoMapper 16.1.1 所需的 `Microsoft.Extensions.Logging.Abstractions` 10.0.0 和 `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 建立文档化、测试化白名单。
3. 新增 `docs/dependency_governance.md` 和 `docs/release_notes_architecture_improvements_2026-06-09.md`。
4. 新增 `tools/check-architecture-governance.ps1`。
5. 新增 `ArchitectureGovernanceTests`，阻断抽象层重新引用 EF、Core 反向引用 Infrastructure/Data/Services、未记录的 Microsoft.Extensions 跨主版本依赖。

### 2026-06-09：执行后技术验证 1

验证目标：确认架构边界和静态约束与计划一致。

| 验证项 | 结果 | 说明 |
| --- | --- | --- |
| `rg "Microsoft\\.EntityFrameworkCore|Atlas\\.Data\\.EntityFramework" src\\Atlas.Data.Abstractions -g "*.cs" -g "*.csproj"` | 通过 | 无输出，抽象层未重新依赖 EF。 |
| `rg "GetAwaiter\\(\\)\\.GetResult|\\.Result" src\\Atlas.Infrastructure.Caching -g "*.cs"` | 通过 | 无输出，缓存层无同步阻塞远程调用。 |
| `rg "<PackageReference[^>]+Version=" src tests samples tools -g "*.csproj"` | 通过 | 无输出，包版本继续由中央文件治理。 |
| `tools/check-architecture-governance.ps1` | 通过 | 包主版本白名单和项目引用边界均通过。 |
| 新 User 服务文件行数 | 通过 | 所有拆分服务文件均低于 400 行。 |

结论：PLAN-02、PLAN-06、PLAN-07、PLAN-08 的关键静态验收信号均可重复验证。

### 2026-06-09：执行后技术验证 2

验证目标：确认改动后的构建、测试和发布 dry-run 可执行。

| 命令 | 结果 | 通过数/产物 | 备注 |
| --- | --- | ---: | --- |
| `dotnet restore Atlas.sln` | 通过 | - | 无 NuGet downgrade/vulnerability warning。 |
| `dotnet build Atlas.sln --no-restore` | 通过 | - | 0 error；Integration.Tests 仍有 75 个既有 nullable/EF warning。 |
| `dotnet test tests\\Atlas.Data.Tests\\Atlas.Data.Tests.csproj --no-build` | 通过 | 227 | 数据抽象、租户连接、模型配置、缓存测试通过。 |
| `dotnet test tests\\Atlas.Core.Tests\\Atlas.Core.Tests.csproj --no-build` | 通过 | 44 | Token/RBAC 相关测试通过。 |
| `dotnet test tests\\Atlas.Analyzers.Tests\\Atlas.Analyzers.Tests.csproj --no-build` | 通过 | 19 | Analyzer 和架构治理测试通过。 |
| `dotnet test tests\\Atlas.Services.Tests\\Atlas.Services.Tests.csproj --no-build` | 通过 | 40 | Runtime 注册和 UserService 门面测试通过。 |
| `dotnet pack Atlas.sln -c Release -o artifacts\\packages` | 通过 | 22 nupkg | WebApi/sample WebApi 不可打包提示为预期；可打包项目全部成功。 |

结论：计划执行后的代码可恢复、可构建、可测试、可打包。未执行真实外部数据库依赖的 Integration 测试运行，仅通过解决方案构建验证其编译状态。

## 九、通用完成定义

每个计划完成时必须满足：

1. `dotnet restore Atlas.sln` 成功。
2. `dotnet build Atlas.sln --no-restore` 成功。
3. 涉及业务行为的改动必须有单元测试或集成测试。
4. 涉及租户数据访问的改动必须说明 tenantId 和门店范围如何生效。
5. 不引入新的公开逃生口，例如 `UnscopedSet`、通用 raw SQL 字符串执行器、业务层直接 DbContext 注入。
6. 新增 Analyzer 或架构规则必须有正反例测试。
7. 文档同步更新，尤其是模块开发、生产运行模式、缓存、租户隔离和部署说明。
