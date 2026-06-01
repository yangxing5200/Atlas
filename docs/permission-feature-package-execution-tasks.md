# 权限、菜单、功能包、数据权限落地任务拆分

本文把 `docs/permission-feature-package-ideal-design.md` 拆成可执行任务。目标是：平台机制进 Atlas 框架，业务模块只声明自己的权限、能力、菜单、套餐片段和数据资源。

## 当前执行状态

- 阶段 1 已落地：模块授权声明、目录聚合、校验、内置/示例/模板声明已接入。
- 阶段 2 已落地：RBAC 从 catalog 物化权限，权限元数据、allow/deny、具体策略示例已接入。
- 阶段 3 已落地：全局能力/套餐/权益表、catalog 同步服务、权益服务、RBAC 权益裁剪、权益变更缓存版本失效已接入。
- 阶段 4 已落地：数据范围类型、角色授权数据范围字段、模块数据资源声明、目标资源访问评估器、查询谓词构造、Repository/QueryService 统一裁剪入口已接入。
- 阶段 5 已落地：`/api/auth/context` 和 `/api/menus/me` 已接入，菜单按统一授权上下文过滤。
- 阶段 6 已落地基础闭环：`ExplainPermission`、授权目录/租户权益/角色权限管理 API、Analyzer 权限声明门禁已接入；可视化管理后台页面仍由宿主或前端应用实现。

## 阶段 1：模块声明与目录聚合

目标：业务模块能声明自己的授权元数据，框架统一聚合和校验。

任务：

1. 在框架定义 `AuthorizationCatalog` 模型，覆盖 `Capability`、`Permission`、`Package`、`PackageCapability`、`MenuItem`、`DataResource`。
2. 扩展 `IAtlasModule` / `AtlasModule`，增加 `ConfigureAuthorization(...)` 扩展点。
3. `AtlasModuleCatalog` 聚合所有模块声明，注册为单例 `IAtlasAuthorizationCatalog`。
4. 增加目录校验：编码唯一、权限引用能力存在、套餐引用能力存在、菜单可见条件引用存在。
5. 更新内置模块、示例模块和模块模板，让权限声明从业务模块进入 catalog。

验收：

- 模块不需要写 seed 服务即可声明权限。
- 重复权限码或悬空引用在启动/测试阶段失败。
- 现有 RBAC seed 可以从 catalog 读取权限定义。

## 阶段 2：RBAC 物化与后端授权闭环

目标：现有租户库 RBAC 复用 catalog，避免静态权限清单扩散。

任务：

1. `RbacPermissionService.SeedTenantAsync` 从 `IAtlasAuthorizationCatalog.Permissions` 物化租户权限。
2. 扩展租户 `Permission` 字段：`CapabilityCode`、`Resource`、`Action`、`RiskLevel`、`IsAssignable`、`IsSystem`。
3. `RolePermission` 增加 `Effect`，支持 `Allow` / `Deny`。
4. 改造权限合并逻辑：先合并角色 allow/deny，再输出用户角色授权集合。
5. Controller 示例从宽泛 `[Authorize]` 收敛到具体 `Permission:` 策略。

验收：

- 新模块声明权限后，租户 seed 自动写入权限表。
- 缺少具体权限返回 403。
- deny 能覆盖 allow。

## 阶段 3：权益层与套餐能力

目标：租户/门店开通状态裁剪角色权限，能力开通不等于用户授权。

任务：

1. 新增全局表：`Capabilities`、`Packages`、`PackageCapabilities`、`Entitlements`。
2. 实现 `IEntitlementService`：计算 `AvailableCapabilitySet`。
3. 实现 `AvailablePermissionSet`：由 `Capability -> Permission` 展开。
4. 改造 `IPermissionChecker`：`EffectivePermission = RolePermission ∩ AvailablePermission`。
5. 管理员规则调整：租户管理员拥有全部可用权限，但不能越过套餐权益。
6. 实现权益变更缓存失效。

验收：

- 租户未开通能力时，即使角色有权限也不能访问。
- 开通/暂停/过期权益后，用户有效权限可靠更新。
- 支持 tenant/store 主体差异。

## 阶段 4：数据权限框架

目标：框架负责数据权限机制，业务模块只声明资源和少量领域规则。

任务：

1. 定义 `DataScopeType`：`AllTenant`、`CurrentStore`、`SharedStores`、`AssignedStores`、`Department`、`Own`、`Custom`。
2. 角色授权支持为权限点绑定数据范围。
3. 模块声明 `DataResource`：资源编码、实体类型、租户字段、门店字段、所有者字段、支持的数据范围。
4. 提供 `IDataAccessEvaluator`：判断当前用户是否能访问目标资源。
5. 提供 `IDataScopePredicateBuilder`：为查询构造数据范围谓词。
6. 对 `Repository` / `QueryService` 提供统一裁剪入口。
7. 业务模块通过 `IDataScopeContributor<T>` 补充领域规则，例如本人客户、医生病历。

验收：

- 读查询、更新、删除都能做目标数据范围校验。
- 业务模块不再散落手写门店/本人/部门过滤。
- 诊断能说明数据权限拦截原因。

## 阶段 5：菜单与前端授权上下文

目标：菜单只做展示入口，API 授权仍以后端为准。

任务：

1. 实现授权表达式模型和解析器，支持 permission/capability/featureFlag/all/any/not。
2. 实现 `/api/auth/context`，返回 permissions、capabilities、featureFlags、dataScope。
3. 实现 `/api/menus/me`，按当前用户上下文过滤菜单树。
4. 前端路由和按钮只消费统一上下文。

验收：

- 菜单可见不代表 API 可访问。
- 菜单引用不存在权限/能力时 catalog 校验失败。

## 阶段 6：诊断、治理和管理后台

目标：权限问题可解释，配置问题可提前阻断。

任务：

1. 实现 `ExplainPermission`，覆盖套餐、能力、角色、数据范围、有效期、门店主体。
2. 增加后台接口：产品目录、租户权益、角色权限、权限诊断。
3. 增加 Analyzer/测试门禁：新增 API 必须配置明确权限，禁止 `package.*` 作为操作权限。
4. 更新文档和模块模板。

验收：

- 能回答“为什么某用户有/没有某权限”。
- 新模块 PR 能通过静态检查发现权限声明缺失。
