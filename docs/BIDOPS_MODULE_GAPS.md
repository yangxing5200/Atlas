# BidOps Module Gaps

> 更新日期：2026-06-12
> 对照文档：`docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md`
> 对照范围：`src/Atlas.Modules.BidOps`、`frontend/atlas-admin/src/modules/bidops`、`frontend/atlas-admin/src/modules/operations`

## 1. 状态口径

| 状态 | 含义 |
|---|---|
| 已实现 | 对照当前 `src/Atlas.Modules.BidOps`，已有实体、API、服务/查询、后台任务或必要文件处理能力，能支撑该模块 MVP 闭环。 |
| 部分实现 | 当前源码已有相关能力或基础约束，但还不是完整业务模块，缺核心实体、API、权限、任务或页面。 |
| 未实现 | 当前源码中没有该模块的实体、Controller、QueryService、业务服务或后台任务，只能按蓝图规划。 |
| 规划中 | 只应文档化或做禁用占位，不得调用不存在 API，不得展示伪造业务数据。 |

本文只记录差异，不要求一次性补齐所有实体、Controller、迁移和页面。后续实现必须继续遵守 Atlas 模块边界、Tenant DB 归属、Worker 长任务边界和合规红线。

后续实现还必须遵守非破坏性演进原则：

- 保留现有 `src/Atlas.Modules.BidOps/Controllers` 下的 Controller，不因 13 模块拆分而删除或改名。
- 保留当前 `/api/bidops/*` 和 `/api/ops/background-jobs/*` 接口路径、请求/响应语义和授权可用性。
- 保留旧权限码：`bidops.crawl.read`、`bidops.crawl.manage`、`bidops.crawl.import`、`bidops.review.read`、`bidops.review.approve`、`bidops.business.read`。
- 新模块路由、细粒度权限、目录分组和实体扩展只能增量追加。若需要新旧命名切换，必须先并行兼容，再单独评估迁移。
- 前端旧路由继续可访问；新菜单分组不能让现有可用页面变成 404，也不能调用不存在的 API。

后端未实现 API 的处理原则：

- 当前缺口清单中的规划 API 在实现前可以不存在，也可以明确返回 `501 NotImplemented`。
- 不得返回 `200 OK` + 空对象、空数组、默认 DTO、`success: true` 或“后端接口待补充”来伪装成功。
- 真实已实现查询在数据为空时可以返回空列表，但必须有真实 Controller、QueryService、实体或明确的数据来源支撑。
- 前端 ComingSoon 占位不调用未实现 API；后端也不为了 ComingSoon 页面创建假成功接口。

## 2. 当前实现清单

### 2.1 当前后端实体

已存在的 BidOps tenant entity：

```text
CrawlSource
CrawlChannel
CrawlRunLog
RawNotice
RawAttachment
NoticeStaging
PackageStaging
RequirementStaging
ReviewTask
Notice
TenderPackage
RequirementItem
Opportunity
OpportunityStageHistory
OpportunityWatch
Supplier
SupplierContact
SupplierCapability
SupplierEvidenceDocument
SupplierMatchRun
SupplierMatchResult
MissingEvidenceCheck
GoNoGoDecision
```

### 2.2 当前后端 API

```http
GET    /api/bidops/crawl-sources
POST   /api/bidops/crawl-sources
PUT    /api/bidops/crawl-sources/{id}
POST   /api/bidops/crawl-sources/{id}/enable
POST   /api/bidops/crawl-sources/{id}/disable

GET    /api/bidops/crawl-channels
POST   /api/bidops/crawl-channels
PUT    /api/bidops/crawl-channels/{id}
POST   /api/bidops/crawl-channels/{id}/scan-now

GET    /api/bidops/raw-notices
GET    /api/bidops/raw-notices/{id}
GET    /api/bidops/raw-notices/{id}/pipeline
GET    /api/bidops/raw-notices/{id}/attachments
GET    /api/bidops/raw-notices/{id}/attachments/{attachmentId}/text
GET    /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file
POST   /api/bidops/raw-notices/{id}/reparse
POST   /api/bidops/raw-notices/import-url
GET    /api/bidops/crawl-run-logs
GET    /api/bidops/crawl-run-logs/{id}

GET    /api/bidops/review-tasks
GET    /api/bidops/review-tasks/{id}
POST   /api/bidops/review-tasks/{id}/approve
POST   /api/bidops/review-tasks/{id}/ignore
GET    /api/bidops/processing/failures

GET    /api/bidops/dashboard/summary

GET    /api/bidops/notices

GET    /api/bidops/packages
GET    /api/bidops/packages/{id}
GET    /api/bidops/packages/{id}/timeline
GET    /api/bidops/packages/{id}/requirements

GET    /api/bidops/opportunities
POST   /api/bidops/opportunities
GET    /api/bidops/opportunities/{id}
PUT    /api/bidops/opportunities/{id}
POST   /api/bidops/opportunities/{id}/watch
POST   /api/bidops/opportunities/{id}/assess
POST   /api/bidops/opportunities/{id}/stage

GET    /api/bidops/suppliers
POST   /api/bidops/suppliers
GET    /api/bidops/suppliers/{id}
PUT    /api/bidops/suppliers/{id}
POST   /api/bidops/suppliers/{id}/contacts
POST   /api/bidops/suppliers/{id}/capabilities
POST   /api/bidops/suppliers/{id}/evidence-documents

POST   /api/bidops/packages/{id}/match-suppliers
GET    /api/bidops/matching/runs
GET    /api/bidops/matching/runs/{id}
GET    /api/bidops/matching/runs/{id}/results
POST   /api/bidops/packages/{id}/decisions
GET    /api/bidops/packages/{id}/decisions

GET    /api/bidops/operations/dashboard
GET    /api/bidops/operations/jobs
GET    /api/bidops/operations/config-check
GET    /api/bidops/operations/channels/health
GET    /api/bidops/operations/worker-heartbeats
GET    /api/bidops/operations/raw-notices/{id}/pipeline
POST   /api/bidops/operations/jobs/{id}/retry
POST   /api/bidops/operations/jobs/{id}/cancel

GET    /api/ops/background-jobs
GET    /api/ops/background-jobs/summary
GET    /api/ops/background-jobs/{id}
POST   /api/ops/background-jobs/{id}/retry
POST   /api/ops/background-jobs/{id}/cancel
GET    /api/ops/workers
```

### 2.3 当前后台任务

```text
bidops.raw.manual-url-import
bidops.crawl.mock-scan
bidops.crawl.state-grid-ecp-scan
bidops.document.attachment-process
bidops.ai.structured-parse
bidops.ai.mock-parse
bidops.scheduled-scan
bidops.recovery
bidops.opportunity.value-assessment
bidops.opportunity.deadline-reminder
bidops.opportunity.watch-reminder
bidops.opportunity.stale-state-scan
bidops.supplier.evidence-expiry-scan
bidops.matching.supplier-match-run

Recurring tasks:
BidOpsScheduledScanTask
BidOpsRecoveryTask
BidOpsOpportunityMaintenanceTask
BidOpsSupplierMaintenanceTask
```

### 2.4 当前权限

```text
bidops.crawl.read
bidops.crawl.manage
bidops.crawl.import
bidops.review.read
bidops.review.approve
bidops.business.read
bidops.ops.read
bidops.ops.manage
bidops.dashboard.read
bidops.opportunity.read
bidops.opportunity.manage
bidops.opportunity.watch
bidops.opportunity.assess
bidops.supplier.read
bidops.supplier.manage
bidops.supplier.evidence.read
bidops.supplier.evidence.manage
bidops.matching.read
bidops.matching.run
bidops.matching.decide
```

### 2.5 当前前端路由

```text
/bidops
/bidops/crawl/sources
/bidops/crawl/channels
/bidops/crawl/raw-notices
/bidops/crawl/raw-notices/:id
/bidops/intelligence/run-logs
/bidops/intelligence/run-logs/:id
/bidops/review/tasks
/bidops/review/tasks/:id
/bidops/processing/failed
/bidops/notices
/bidops/packages
/bidops/packages/:id
/bidops/dashboard
/bidops/opportunities
/bidops/opportunities/:id
/bidops/opportunities/watchlist
/bidops/suppliers
/bidops/suppliers/:id
/bidops/matching/packages
/bidops/matching/runs
/bidops/matching/runs/:id
/bidops/matching/decisions
/bidops/operations
/bidops/operations/jobs
/bidops/operations/channels
/bidops/operations/worker-heartbeats
/ops/jobs
/ops/jobs/:id
/ops/workers
```

## 3. 对照 `src/Atlas.Modules.BidOps` 的模块状态标注

### 3.1 总览

| 状态 | 模块 |
|---|---|
| 已实现 | 情报采集中心、解析审核中心、后台任务与日志监控中心 |
| 部分实现 | 指挥中心、商机包件中心、厂家能力库、匹配与立项决策台、投标作业中心、结果复盘中心、合规风控与审计中心 |
| 未实现 | 采购方与代理机构画像、响应矩阵与文件中心、规则与配置中心 |

### 3.2 已实现

| 模块 | 源码证据 | 当前能力 | 仍需补强 |
|---|---|---|---|
| 情报采集中心 | `Entities/Crawling/*`、`Controllers/CrawlSourcesController.cs`、`Controllers/CrawlChannelsController.cs`、`Controllers/RawNoticesController.cs`、`Controllers/CrawlRunLogsController.cs`、`Services/BidOpsCrawlService.cs`、`Services/BidOpsRawIngestionService.cs`、`Services/BidOpsAttachmentProcessingService.cs`、`Crawling/StateGridEcpCrawler.cs`、`Documents/BidOpsTextExtractor.cs`、`BackgroundJobs/ManualUrlImportJobHandler.cs`、`BackgroundJobs/StateGridEcpCrawlJobHandler.cs`、`BackgroundJobs/AttachmentProcessJobHandler.cs`、`BackgroundJobs/BidOpsScheduledScanTask.cs`、`BackgroundJobs/BidOpsRecoveryTask.cs` | 已有采集来源、采集栏目、原始公告、RawNotice pipeline、采集运行日志列表/详情、附件元数据、附件文本、附件受控预览/下载、手动 URL 导入、来源启停、栏目立即扫描、State Grid ECP 公开采集、附件下载/文本提取、定时扫描和补偿任务。 | 仍缺 Raw 版本对比、采集质量问题、source health audit。 |
| 解析审核中心 | `Entities/Staging/*`、`Controllers/ReviewTasksController.cs`、`Controllers/ProcessingFailuresController.cs`、`Services/BidOpsAiParsingService.cs`、`Services/BidOpsReviewService.cs`、`Ai/BidOpsStructuredExtractionService.cs`、`Documents/BidOpsRawNoticeTextFormatter.cs`、`BackgroundJobs/StructuredParseJobHandler.cs`、`BackgroundJobs/MockAiParseJobHandler.cs` | 已有 staging 结构化解析、待审核池、审核详情、解析失败队列、重解析入口、中文化公告全文、附件提取文本展示、审核通过、忽略、审核通过后写入正式公告/包件/要求项。 | 仍缺可编辑 staging、疑似重复、合并、版本差异视图、审核 diff、`processing.*` 和 `review.merge` 权限。 |
| 后台任务与日志监控中心 | `Controllers/BidOpsOperationsController.cs`、`Controllers/BackgroundJobsOperationsController.cs`、`Controllers/BackgroundWorkersOperationsController.cs`、`Queries/BidOpsOperationsQueryService.cs`、`Models/BidOpsOperationsDtos.cs`、`BackgroundJobs/*`、`BidOpsConstants.cs` 中 `BidOpsBackgroundJobQueues` 和 `BidOpsBackgroundJobTypes`、Global `BackgroundWorkerHeartbeat` | 已有通用后台任务列表、摘要、详情、重试、取消；已有 Worker 心跳列表；已有 BidOps 运维看板、任务列表、配置检查、栏目健康、RawNotice pipeline、`bidops` 队列任务观察、Payload 脱敏视图；已追加 `bidops.ops.read/manage` 到授权目录。 | 仍缺集中日志查询、失败通知、长期趋势；运维接口授权仍保留旧 `crawl` 权限兼容。 |

### 3.3 部分实现

| 模块 | 源码证据 | 已有部分 | 未完成部分 |
|---|---|---|---|
| 指挥中心 | `Controllers/BidOpsDashboardController.cs`、`Controllers/BidOpsOperationsController.cs`、`Queries/BidOpsOperationsQueryService.cs`、`Models/BidOpsOperationsDtos.cs` | 有业务 dashboard summary，可统计今日公告/审核/入库/包件、待审核、有效商机、截止风险、商机漏斗、价值分布、高价值商机；也有运维类 dashboard 数据、配置告警和栏目健康摘要。 | 仍缺独立风险信号模型、胜率指标、投标作业风险、经营趋势图。 |
| 商机包件中心 | `Entities/Tendering/*`、`Entities/Opportunities/*`、`Controllers/NoticesController.cs`、`Controllers/PackagesController.cs`、`Controllers/OpportunitiesController.cs`、`Queries/BidOpsQueryService.cs`、`Services/BidOpsReviewService.cs`、`Services/BidOpsOpportunityService.cs`、`Services/BidOpsOpportunityMaintenanceService.cs`、`BackgroundJobs/BidOpsOpportunityMaintenanceTask.cs`、`BackgroundJobs/OpportunityMaintenanceJobHandlers.cs` | 有正式公告、包件、要求项、包件详情、包件时间线；审核通过能把 staging 导入正式 tendering 表；已有商机列表/详情/关注/评估/状态流转和包件详情创建商机入口；已有价值评估、截止提醒、关注提醒、陈旧状态扫描后台任务。 | 仍缺截止日历、外部/站内通知实体、独立评估记录、立项入口。 |
| 厂家能力库 | `Entities/Suppliers/*`、`Entities/Outcomes/OutcomeSupplierRecord.cs`、`Controllers/SuppliersController.cs`、`Services/BidOpsSupplierService.cs`、`Services/BidOpsOutcomeSupplierExtractionService.cs`、`Services/BidOpsSupplierMaintenanceService.cs`、`BackgroundJobs/BidOpsSupplierMaintenanceTask.cs`、`BackgroundJobs/SupplierEvidenceExpiryScanJobHandler.cs`、`BackgroundJobs/OutcomeSupplierExtractJobHandler.cs` | 有厂家列表、详情、新增、编辑、联系人、能力标签、资质材料元数据、材料有效期状态和基于厂家/匹配/决策/作业/公开结果线索的分析汇总；已有 `bidops.supplier.evidence-expiry-scan`、`bidops.outcome.supplier-extract` 和周期入队任务；前端 `/bidops/suppliers`、`/bidops/suppliers/:id`、`/bidops/suppliers/analysis` 已是实时页面，包件详情可看历史厂家线索。 | 仍缺独立材料临期列表 API/页面、独立子资源 GET 接口、产品线模型、完整 `SupplierPerformance` 胜率/地区/品类复盘、通知实体和提醒送达。 |
| 匹配与立项决策台 | `Entities/Matching/*`、`Controllers/MatchingController.cs`、`Controllers/PackagesController.cs` 中匹配和决策接口、`Services/BidOpsMatchingService.cs`、`BackgroundJobs/SupplierMatchRunJobHandler.cs` | 有包件发起厂家匹配、异步 Worker 运行、匹配记录列表/详情、匹配结果、缺失材料检查、Go/No-Go 决策记录；前端 `/bidops/matching/packages`、`/bidops/matching/runs`、`/bidops/matching/runs/:id`、`/bidops/matching/decisions` 已是真实页面。 | 仍缺 `MatchingRuleSet`、规则版本、画像参与评分、独立评分刷新任务和更细的材料核验工作台。 |
| 投标作业中心 | `Entities/Pursuits/*`、`Controllers/PursuitsController.cs`、`Services/BidOpsPursuitService.cs`、`BidOpsConstants.cs` 中 `bidops.pursuit.*`、前端 `pages/pursuits/*` | 有作业列表/详情、新建/编辑、阶段流转、任务新增/编辑、跟进记录新增、一个包件最多一个 active pursuit 的实体约束；前端 `/bidops/pursuits`、`/bidops/pursuits/:id`、`/bidops/pursuits/my-tasks` 已是真实页面。 | 仍缺作业日历、逾期提醒任务、PursuitRisk 独立模型、作业风险看板、响应矩阵和提交前检查。 |
| 结果复盘中心 | `Entities/Outcomes/OutcomeSupplierRecord.cs`、`Services/BidOpsOutcomeSupplierExtractionService.cs`、`BackgroundJobs/OutcomeSupplierExtractJobHandler.cs`、`Controllers/SuppliersController.cs` 中 outcome-records/outcome-summary/backfill 接口、`Controllers/PackagesController.cs` 中 historical-suppliers 接口 | 有公开中标/成交/候选公示厂家线索抽取、回填、结果线索列表/汇总，以及包件维度历史厂家线索；线索保留来源公告、证据文本和包件快照，不自动创建厂家主档。 | 仍缺 `BidOutcome`、`OutcomeReview`、结果录入、人工复盘、胜率分析、正式 `SupplierPerformance`、`outcome.*` 和 `analytics.read` 权限，以及独立结果复盘页面。 |
| 合规风控与审计中心 | `Services/BidOpsRawIngestionService.cs` 中登录源跳过/暂停逻辑、`Services/BidOpsReviewService.cs` 中人工审核后入库、`BackgroundJobs/BidOpsJobIdentity.cs`、`BidOpsConstants.cs` 中权限风险级别、共享 BackgroundJob payload 脱敏由 BidOps operations 使用 | 已有部分合规边界：不运行登录必需来源；AI/规则结果先进 staging；正式入库需要人工审核；后台任务身份上下文可追踪；运维 payload 会脱敏。 | 缺 `ComplianceCheck`、`SensitiveWordHit`、`ComplianceRule`、`BidOpsAuditLog` 实体；缺合规检查 API/UI、敏感词、审计查询、冲突扫描、合规规则配置、`compliance.*` 和 `audit.read` 权限。 |

### 3.4 未实现

| 模块 | 源码对照 | 缺口 |
|---|---|---|
| 采购方与代理机构画像 | 当前没有 `PublicOrgs` 目录、`BuyerProfile`/`AgencyProfile` 实体、画像 Controller、画像 QueryService 或画像刷新任务。 | 缺 buyer/agency profile、别名、公开历史统计、画像 API、画像页面、画像刷新任务、`public-org.*` 权限。 |
| 响应矩阵与文件中心 | 当前没有 `Responses` 目录、`ResponseMatrixItem`/`BidDocument` 实体或响应矩阵 Controller；现有 `RawAttachment` 只属于采集侧。 | 缺响应矩阵、证据绑定、投标文件、文件版本、模板库、提交前检查、`response.*` 和 `file.*` 权限。 |
| 规则与配置中心 | 当前没有 `Settings` 目录、字典/规则/Prompt/通知规则实体、配置 Controller 或规则审计任务；仅有服务读取 `BidOps:*` 配置键。 | 缺字典、匹配规则、合规规则、AI Prompt 管理、通知规则、功能开关、规则审计、`config.*` 和 `rules.*` 权限。 |

## 4. 关键缺口清单

### 4.1 P0 后续硬化缺口

| 编号 | 缺口 | 所属模块 | 状态 | 说明 |
|---|---|---|---|---|
| GAP-P0-01 | RawNotice pipeline view | 情报采集 / 运维监控 | 已完成 | 已实现按 RawNoticeId 聚合的采集、附件、解析、审核、正式入库流水线视图。 |
| GAP-P0-02 | 采集运行日志详情页 | 情报采集 | 已完成 | 已实现 `CrawlRunLog` 列表/详情 API 和前端页面。 |
| GAP-P0-03 | 解析失败队列 | 解析审核 | 已完成 | 已实现 `GET /api/bidops/processing/failures` 和解析失败前端工作台；当前按真实 RawNotice `Failed` 状态查询。 |
| GAP-P0-04 | 重解析入口 | 解析审核 | 已完成 | 已实现 `POST /api/bidops/raw-notices/{id}/reparse` 和前端入口；WebApi 只入队，Worker 强制新建结构化解析任务。 |
| GAP-P0-05 | 专用运维权限 | 运维监控 | 已完成 | 已追加 `bidops.ops.read/manage` 到授权目录和前端常量；接口仍保留旧 `crawl` 权限兼容。 |
| GAP-P0-06 | Worker 心跳 | 运维监控 | 已完成 | 已实现 Global `BackgroundWorkerHeartbeat`、`GET /api/ops/workers`、`/ops/workers` 和 `/bidops/operations/worker-heartbeats`；可确认是否存在消费 `bidops` 队列的在线 Worker。 |
| GAP-P0-07 | 附件受控下载/预览 | 情报采集 / 文件中心 | 已完成 | 已实现授权文件流接口 `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file`；默认 inline 预览，`?download=true` 下载，前端通过 Axios blob 携带 token，不暴露 storage key。 |

### 4.2 Phase B 缺口

| 编号 | 缺口 | 状态 | 说明 |
|---|---|---|---|
| GAP-B-01 | `Opportunity` 实体 | 已完成 | 已把正式包件提升为经营对象，承载状态、关注、评估和后续立项前置数据。 |
| GAP-B-02 | 商机 API | 已完成 | 已实现 `GET/POST/PUT /api/bidops/opportunities`、详情、关注、评估、状态流转。 |
| GAP-B-03 | 指挥中心业务指标 | 已完成 | 已实现真实 dashboard summary 和 `/bidops/dashboard` 页面，包含今日新增、高价值商机、待办、截止风险、商机漏斗。 |
| GAP-B-04 | 截止日历 | 未实现 | 以包件/商机关键日期提供日历视图和提醒基础；前端当前保持 `ComingSoon`，不调用假 API。 |
| GAP-B-05 | 商机后台任务 | 已完成 | 已实现 OpportunityMaintenance 周期任务和四类 job：价值评估、截止提醒扫描、关注提醒扫描、陈旧状态扫描。 |

### 4.3 Phase C-G 缺口

| 阶段 | 主要缺口 |
|---|---|
| Phase C | MVP 已完成：`Supplier`、联系人、能力标签、资质材料、材料有效期扫描、厂家分析汇总；已追加公开结果公示厂家线索 `OutcomeSupplierRecord`、回填任务和包件历史厂家线索；剩余为材料临期列表、产品线、完整 `SupplierPerformance`、通知送达。 |
| Phase D | MVP 已完成：`SupplierMatchRun`、`SupplierMatchResult`、`MissingEvidenceCheck`、`GoNoGoDecision`、异步匹配任务和匹配/决策页面；剩余为规则版本、画像参与评分、评分刷新和更细材料核验。 |
| Phase E | 投标作业 MVP 已完成：`Pursuit`、作业任务、跟进记录和核心页面；剩余为作业日历、逾期提醒、响应矩阵、文件版本和提交前检查。 |
| Phase F | 合规检查、敏感词、结果录入、结果复盘、胜率分析。 |
| Phase G | 字典、匹配规则、合规规则、AI Prompt、通知规则、功能开关。 |

## 5. API 缺口

以下 API 分为已实现和仍未实现两类。未实现前前端不得调用；后端也不得用 `200 OK` 空响应伪装成已实现。

### 5.1 指挥中心

当前已实现：

```http
GET /api/bidops/dashboard/summary
```

仍未实现：

```http
GET /api/bidops/dashboard/todos
GET /api/bidops/dashboard/deadlines
GET /api/bidops/dashboard/risks
GET /api/bidops/dashboard/pipeline
```

### 5.2 解析治理

```http
GET  /api/bidops/processing/duplicates
POST /api/bidops/review-tasks/{id}/merge
GET  /api/bidops/review-tasks/{id}/diff
```

### 5.3 商机经营

当前已实现：

```http
GET    /api/bidops/opportunities
POST   /api/bidops/opportunities
GET    /api/bidops/opportunities/{id}
PUT    /api/bidops/opportunities/{id}
POST   /api/bidops/opportunities/{id}/watch
POST   /api/bidops/opportunities/{id}/assess
POST   /api/bidops/opportunities/{id}/stage
```

仍未实现：

```http
GET    /api/bidops/opportunities/calendar
GET    /api/bidops/opportunities/assessments
```

### 5.4 厂家能力库

当前已实现：

```http
GET    /api/bidops/suppliers
POST   /api/bidops/suppliers
GET    /api/bidops/suppliers/{id}
PUT    /api/bidops/suppliers/{id}
POST   /api/bidops/suppliers/{id}/contacts
POST   /api/bidops/suppliers/{id}/capabilities
POST   /api/bidops/suppliers/{id}/evidence-documents
GET    /api/bidops/suppliers/analysis/summary
GET    /api/bidops/suppliers/outcome-records
GET    /api/bidops/suppliers/outcome-summary
POST   /api/bidops/suppliers/outcome-records/backfill
GET    /api/bidops/packages/{id}/historical-suppliers
```

仍未实现：

```http
GET    /api/bidops/suppliers/{id}/contacts
GET    /api/bidops/suppliers/{id}/capabilities
GET    /api/bidops/suppliers/{id}/evidence-documents
GET    /api/bidops/suppliers/evidence-expiry
GET    /api/bidops/analytics/supplier-performance
```

说明：当前厂家详情接口已返回联系人、能力标签和资质材料集合；厂家分析接口读取真实厂家库、匹配、决策、作业和公开结果厂家线索数据。`OutcomeSupplierRecord` 来自公开中标/成交/候选公示和附件文本的严格字段抽取，保留来源公告、证据文本和包件快照，不自动创建厂家主档、不伪造联系方式、不自动联系厂家。独立子资源 GET 接口、临期列表和完整结果复盘维度的 `supplier-performance` 尚未开放。

### 5.5 匹配与立项

当前已实现：

```http
POST   /api/bidops/packages/{id}/match-suppliers
GET    /api/bidops/matching/runs
GET    /api/bidops/matching/runs/{id}
GET    /api/bidops/matching/runs/{id}/results
POST   /api/bidops/packages/{id}/decisions
GET    /api/bidops/packages/{id}/decisions
```

仍未实现：

```http
GET    /api/bidops/matching/rule-sets
POST   /api/bidops/matching/rule-sets
POST   /api/bidops/matching/runs/{id}/refresh-score
```

### 5.6 画像、作业、响应、结果、合规、配置

投标作业当前已实现：

```http
GET    /api/bidops/pursuits
POST   /api/bidops/pursuits
GET    /api/bidops/pursuits/{id}
PUT    /api/bidops/pursuits/{id}
POST   /api/bidops/pursuits/{id}/status
GET    /api/bidops/pursuits/{id}/tasks
POST   /api/bidops/pursuits/{id}/tasks
PUT    /api/bidops/pursuits/{id}/tasks/{taskId}
GET    /api/bidops/pursuits/{id}/follow-records
POST   /api/bidops/pursuits/{id}/follow-records
```

以下仍未实现：

```http
GET    /api/bidops/public-orgs/buyers
GET    /api/bidops/public-orgs/agencies
POST   /api/bidops/public-orgs/aliases

GET    /api/bidops/pursuits/calendar

GET    /api/bidops/pursuits/{id}/response-matrix
POST   /api/bidops/pursuits/{id}/submission-checks
GET    /api/bidops/files
POST   /api/bidops/files
GET    /api/bidops/document-templates
POST   /api/bidops/document-templates

GET    /api/bidops/outcomes
POST   /api/bidops/outcomes
GET    /api/bidops/outcomes/{id}/review
POST   /api/bidops/outcomes/{id}/review
GET    /api/bidops/analytics/win-rate

GET    /api/bidops/compliance/checks
POST   /api/bidops/compliance/checks/run
GET    /api/bidops/compliance/sensitive-words
POST   /api/bidops/compliance/sensitive-words
GET    /api/bidops/audit-logs

GET    /api/bidops/settings/dictionaries
GET    /api/bidops/settings/matching-rules
GET    /api/bidops/settings/compliance-rules
GET    /api/bidops/settings/ai-prompts
GET    /api/bidops/settings/notification-rules
```

## 6. 权限缺口

当前已经包含旧权限组、P0 运维权限、Phase B dashboard/商机权限、Phase C 厂家权限、Phase D 匹配权限和 Phase E 投标作业权限。以下权限尚未进入后端授权目录和前端常量：

```text
bidops.intelligence.read
bidops.intelligence.manage
bidops.intelligence.import
bidops.intelligence.refetch
bidops.processing.read
bidops.processing.reparse
bidops.processing.manage
bidops.review.merge
bidops.public-org.read
bidops.public-org.manage-alias
bidops.response.read
bidops.response.manage
bidops.response.check
bidops.file.read
bidops.file.manage
bidops.outcome.read
bidops.outcome.manage
bidops.analytics.read
bidops.compliance.read
bidops.compliance.manage
bidops.audit.read
bidops.config.read
bidops.config.manage
bidops.rules.read
bidops.rules.manage
```

执行注意：

- 旧权限不得删除。
- 新权限应先加入授权目录、套餐能力和前端常量，再逐步替换页面和 API 策略。
- 未实现功能即使有权限也不能显示为可操作。

## 7. 数据模型缺口

已补齐：

```text
Opportunity
OpportunityStageHistory
OpportunityWatch
Supplier
SupplierContact
SupplierCapability
SupplierEvidenceDocument
SupplierMatchRun
SupplierMatchResult
MissingEvidenceCheck
GoNoGoDecision
Pursuit
PursuitTask
PursuitFollowRecord
```

下一批建议优先补：

```text
ResponseMatrixItem
ResponseEvidenceBinding
SubmissionCheckRun
BidOutcome
ComplianceCheck
SensitiveWordHit
BidOpsNotification
```

增强模型可后置：

```text
BuyerProfile
AgencyProfile
PublicOrganizationAlias
BuyerProcurementStat
AgencyNoticePattern
SupplierProductLine
OutcomeSupplierRecord
SupplierPerformance
PursuitRisk
BidDocument
BidDocumentVersion
DocumentTemplate
OutcomeReview
CategoryWinRateStat
RegionWinRateStat
MatchingRuleSet
AiPromptTemplate
```

数据模型实现要求：

- 继续使用 Atlas Tenant DB 和 `bidops_` 表前缀。
- 租户业务实体继续实现 Atlas 租户隔离规则。
- 唯一索引和去重键必须 tenant-scoped。
- 不新增 `BidOpsDbContext`。
- 大文件、附件二进制和长文本继续走 `IBidOpsFileStore`。

## 8. 前端缺口

当前前端已经补齐 13 个模块的菜单和路由占位。已实现模块继续进入真实页面；未实现模块进入 `ComingSoonPage`，不调用后端 API。

已完成：

- BidOps 一级菜单分组：指挥中心、情报采集、解析审核、商机包件、采购画像、厂家能力、匹配决策、投标作业、响应文件、结果复盘、合规审计、运维监控、规则配置。
- 13 个模块首页入口卡片。
- 未实现模块的 `ComingSoon` 路由占位。
- 已实现旧路由的兼容入口，例如 `crawl` 到 `intelligence`、`review` 到 `processing` 的别名路由。
- 采集运行日志列表和详情页已经从 `ComingSoon` 替换为真实页面。
- 解析失败页面已经从 `ComingSoon` 替换为真实失败队列；无失败数据时显示真实空状态。
- 商机列表、商机详情、关注列表、评估和状态流转已经从占位替换为真实页面。
- 指挥中心 `/bidops/dashboard` 已经从 `ComingSoon` 替换为真实业务 dashboard。
- 厂家列表 `/bidops/suppliers`、详情 `/bidops/suppliers/:id` 和分析 `/bidops/suppliers/analysis` 已经从占位替换为真实页面。
- 匹配记录 `/bidops/matching/runs`、匹配详情 `/bidops/matching/runs/:id`、包件匹配入口和立项决策页面已经从占位替换为真实页面。

仍需补齐：

- 截止日历、商机提醒确认、胜率趋势、投标作业风险等真实页面。
- 厂家材料临期列表、能力地图、完整 SupplierPerformance/胜率复盘等真实页面。
- 匹配规则版本、评分刷新和更细材料核验页面。

占位页规则：

- 不调用不存在 API。
- 不展示 mock 成功数据。
- 页面可以列出“所需后端接口”。
- 缺口需要继续同步到本文。

## 9. 后台任务缺口

当前任务覆盖采集、附件处理、解析、定时扫描、补偿、商机维护和厂家材料有效期扫描。以下类别尚未实现：

```text
bidops.governance.deduplicate
bidops.governance.version-detect
bidops.governance.data-quality-audit
bidops.public-org.profile-refresh
bidops.supplier.profile-quality-audit
bidops.matching.score-refresh
bidops.pursuit.deadline-reminder-scan
bidops.response.generate-matrix
bidops.response.submission-check
bidops.outcome.public-award-result-scan
bidops.analytics.win-rate-refresh
bidops.compliance.pursuit-conflict-scan
bidops.compliance.sensitive-word-audit
bidops.notification.failed-job-reminder
bidops.maintenance.cleanup-old-snapshots
```

实现顺序应跟随业务阶段，不要为了“任务全景”一次性创建空 handler。

## 10. 推荐执行顺序

1. 完成 Phase A 剩余文档和菜单规划：保持现有 API 不变，设计 13 模块菜单分组和禁用占位规则。
2. 继续硬化运维 P0 已完成：采集运行日志详情页、解析失败队列、重解析入口、Worker heartbeat 和附件受控下载/预览均已落地。
3. 继续 Phase B 增强项：截止日历、提醒确认、胜率趋势和投标作业风险。
4. Phase C 厂家基础档案、材料有效期、能力标签、厂家分析汇总和公开结果厂家线索 MVP 已完成；剩余完整 `SupplierPerformance`、胜率复盘和产品线增强后置。
5. Phase D 匹配运行、缺失材料检测、Go/No-Go 决策 MVP 已完成；剩余规则版本和评分刷新后置。
6. 实现 Phase E：投标作业、响应矩阵、文件版本和提交检查。
7. 实现 Phase F：合规检查、结果录入、复盘和基础经营分析。
8. 实现 Phase G：字典、规则、AI Prompt、通知规则和功能开关。

## 11. 不应实现的能力

以下能力仍然禁止实现，不能作为“缺口”排期：

```text
自动报价
自动投标
自动提交投标文件
自动联系厂家
登录态抓取
验证码破解
绕过反爬机制
非公开信息采集
专家名单采集
招标方内部意见采集
返点、好处费、灰色资金流相关功能
多厂家同一包件协同投标
帮助生成围标/串标材料
```
