# BidOps 产品模块扩展与愿景对齐执行说明书

> 文件名建议：`CODEX_BIDOPS_PRODUCT_MODULE_EXPANSION_SPEC.md`
> 适用仓库：`yangxing5200/Atlas`
> 适用模块：`src/Atlas.Modules.BidOps` 与 `frontend/atlas-admin`
> 目标读者：Codex / AI Coding Agent / 人类开发者
> 目标：把 BidOps 从“采集 + 审核 + 包件 + 运维”扩展为完整的招投标运营系统模块蓝图，并为后续后端、前端、权限、任务、日志、数据模型演进提供统一边界。

---

## 0. 给 Codex 的总指令

你是本项目的主要实现者。请先阅读以下文件，再执行本说明书：

1. 仓库根目录 `AGENTS.md`
2. 仓库根目录 `BIDOPS_CODEX_EXECUTION_SPEC.md`
3. 仓库根目录 `CODEX_BIDOPS_FRONTEND_EXECUTION_SPEC.md`，如果存在
4. 仓库根目录 `CODEX_BIDOPS_BACKGROUND_TASKS_OBSERVABILITY_SPEC.md`，如果存在
5. 当前 BidOps 后端模块：`src/Atlas.Modules.BidOps`
6. 当前前端项目：`frontend/atlas-admin`，如果已经存在

本说明书的目标不是让你一次性完成所有业务能力，而是：

- 重新梳理 BidOps 一级模块体系。
- 明确哪些是现有能力，哪些是愿景能力，哪些只能做占位。
- 补齐权限、菜单、路由、API 命名、领域模型、后台任务之间的统一边界。
- 生成或更新产品模块蓝图文档，避免后续功能继续零散堆积。
- 在不破坏现有构建、不删除现有 API、不改变当前已实现业务链路的前提下，逐步演进。

如果当前仓库代码与本文档描述不一致，以当前代码为准，并在 `docs/BIDOPS_MODULE_GAPS.md` 中记录差异和处理建议。

---

## 1. 当前问题判断

当前 BidOps 已经有比较清晰的第一阶段骨架，但“4 个模块”不足以承载目标愿景。

现有能力更接近：

```text
采集配置 / 原始公告
  -> AI/规则预解析
    -> 待审核池
      -> 正式公告 / 包件 / 要求项
        -> 后台任务与日志监控
```

这条链路能解决“把公开标讯抓下来、解析出来、审核入库”的问题，但还没有覆盖完整招投标运营：

```text
发现机会
  -> 判断值不值得做
    -> 找谁做
      -> 谁来推进
        -> 材料是否齐
          -> 响应是否合规
            -> 是否按期提交
              -> 结果如何
                -> 复盘后如何提升下一次胜率
```

因此，BidOps 不应该被定义成 4 个粗模块，而应升级为：

> 公开标讯情报、商机包件经营、厂家资源调度、投标作业交付、响应合规核验、结果复盘增长、运维可观测的一体化招投标运营系统。

---

## 2. 新的产品定位

BidOps 的新定位：

> BidOps = Tender Operations OS / 招投标运营操作系统。

它不是：

- 不是普通 CRM。
- 不是单纯标讯采集工具。
- 不是招标公告展示站。
- 不是自动写标书工具。
- 不是自动报价工具。
- 不是自动投标工具。
- 不是关系、返点、串标、非公开信息分析工具。

它是：

```text
公开情报发现
  + 包件级商机判断
  + 厂家能力调度
  + 投标项目作业协同
  + 要求响应矩阵
  + 材料与文件核验
  + 合规风险控制
  + 结果复盘与经营分析
  + 后台任务与日志可观测
```

最小业务单元仍然是：

```text
商机包件 / Tender Package / Opportunity Package
```

但围绕包件需要新增完整经营链路，而不是只展示包件列表。

---

## 3. 模块划分原则

模块划分不要按数据库表，也不要按控制器数量。应按业务角色和业务流程划分。

### 3.1 一条主链路

```text
情报进入
  -> 数据加工
    -> 人工治理
      -> 商机经营
        -> 资源匹配
          -> 立项决策
            -> 投标交付
              -> 响应核验
                -> 结果复盘
```

### 3.2 三类支撑能力

```text
合规审计
后台运维
规则配置
```

### 3.3 一级菜单不等于领域边界

前端一级菜单可以做 10～13 个；后端领域上下文可以更多；数据库实体可以更多。不要为了菜单少而牺牲业务表达。

---

## 4. 推荐一级模块体系

建议 BidOps 最终采用以下 13 个一级模块。

```text
01. 指挥中心
02. 情报采集中心
03. 解析审核中心
04. 商机包件中心
05. 采购方与代理机构画像
06. 厂家能力库
07. 匹配与立项决策台
08. 投标作业中心
09. 响应矩阵与文件中心
10. 结果复盘中心
11. 合规风控与审计中心
12. 后台任务与日志监控中心
13. 规则与配置中心
```

其中：

- 01～10 是核心业务模块。
- 11 是业务安全边界。
- 12 是运行可观测能力。
- 13 是系统可配置能力。

---

## 5. 模块一：指挥中心

### 5.1 目标

让业务负责人一眼知道今天 BidOps 的整体状态：

```text
今天发现了多少机会
哪些机会值得跟进
哪些包件快截止
哪些厂家材料缺失
哪些投标项目有风险
哪些后台任务失败
哪些结果需要复盘
```

### 5.2 前端路由

```text
/bidops
/bidops/dashboard
```

### 5.3 页面内容

```text
今日新增标讯
待审核公告
高价值商机
高匹配包件
即将截止事项
投标作业风险
材料过期提醒
后台任务异常
本月中标/失标统计
本月胜率变化
```

### 5.4 后端 API

```http
GET /api/bidops/dashboard/summary
GET /api/bidops/dashboard/todos
GET /api/bidops/dashboard/deadlines
GET /api/bidops/dashboard/risks
GET /api/bidops/dashboard/pipeline
```

### 5.5 领域对象

```text
BidOpsDashboardSummary
BidOpsTodoItem
BidOpsDeadlineItem
BidOpsRiskItem
BidOpsPipelineStat
```

### 5.6 权限

```text
bidops.dashboard.read
```

---

## 6. 模块二：情报采集中心

### 6.1 目标

负责公开标讯来源、栏目、扫描、详情抓取、附件发现、原始公告入库。

### 6.2 已有能力

当前应保留并增强：

```text
CrawlSource
CrawlChannel
RawNotice
RawAttachment
CrawlRunLog
ManualUrlImport
```

### 6.3 前端路由

```text
/bidops/intelligence/sources
/bidops/intelligence/channels
/bidops/intelligence/raw-notices
/bidops/intelligence/raw-notices/:id
/bidops/intelligence/import-url
/bidops/intelligence/crawl-runs
```

可以兼容旧路由：

```text
/bidops/crawl/sources
/bidops/crawl/channels
/bidops/crawl/raw-notices
```

旧路由可以保留 redirect，不要直接删除。

### 6.4 后端 API

继续使用现有 API：

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
POST   /api/bidops/raw-notices/import-url
```

建议新增：

```http
GET    /api/bidops/raw-notices/{id}/attachments
GET    /api/bidops/raw-notices/{id}/crawl-logs
POST   /api/bidops/raw-notices/{id}/refetch
```

### 6.5 后台任务

```text
bidops.crawl.source-scan
bidops.crawl.channel-scan
bidops.crawl.detail-fetch
bidops.crawl.manual-url-import
bidops.document.attachment-discover
bidops.document.attachment-download
```

### 6.6 权限

```text
bidops.intelligence.read
bidops.intelligence.manage
bidops.intelligence.import
bidops.intelligence.refetch
```

兼容旧权限：

```text
bidops.crawl.read
bidops.crawl.manage
bidops.crawl.import
```

---

## 7. 模块三：解析审核中心

### 7.1 目标

负责把 RawNotice 加工成可审核的结构化数据，并确保 AI/规则解析不能直接进入正式业务库。

### 7.2 包含能力

```text
文本抽取
附件文本提取
AI/规则结构化解析
NoticeStaging
PackageStaging
RequirementStaging
置信度记录
解析错误处理
去重识别
变更识别
人工审核
合并
重析
```

### 7.3 前端路由

```text
/bidops/processing/review-tasks
/bidops/processing/review-tasks/:id
/bidops/processing/staging
/bidops/processing/duplicates
/bidops/processing/versions
/bidops/processing/parse-failures
```

兼容旧路由：

```text
/bidops/review/tasks
/bidops/review/tasks/:id
```

### 7.4 后端 API

保留现有：

```http
GET  /api/bidops/review-tasks
GET  /api/bidops/review-tasks/{id}
POST /api/bidops/review-tasks/{id}/approve
POST /api/bidops/review-tasks/{id}/ignore
```

建议新增：

```http
POST /api/bidops/review-tasks/{id}/merge
POST /api/bidops/review-tasks/{id}/reparse
GET  /api/bidops/staging/notices/{id}
GET  /api/bidops/staging/notices/{id}/packages
PUT  /api/bidops/staging/notices/{id}
PUT  /api/bidops/staging/packages/{id}
PUT  /api/bidops/staging/requirements/{id}
GET  /api/bidops/duplicates
POST /api/bidops/duplicates/{id}/confirm
POST /api/bidops/duplicates/{id}/dismiss
```

### 7.5 后台任务

```text
bidops.document.text-extract
bidops.ai.structured-parse
bidops.ai.mock-parse
bidops.governance.deduplicate
bidops.governance.version-detect
bidops.governance.generate-review-task
```

### 7.6 权限

```text
bidops.processing.read
bidops.processing.reparse
bidops.processing.manage
bidops.review.read
bidops.review.approve
bidops.review.merge
```

---

## 8. 模块四：商机包件中心

### 8.1 目标

把正式公告、包件、要求项从“数据展示”升级为“商机经营”。

### 8.2 核心对象

```text
Notice
TenderPackage
RequirementItem
Opportunity
OpportunityStageHistory
OpportunityWatch
OpportunityReminder
OpportunityTag
OpportunityValueAssessment
```

### 8.3 商机状态建议

```text
New               新发现
Screening         初筛中
Watch             关注中
Qualified         已确认有价值
Matching          匹配厂家中
DecisionPending   待立项决策
Pursuing          已进入投标作业
Submitted         已提交
Won               已中标
Lost              已失标
Abandoned         放弃
Archived          归档
```

### 8.4 前端路由

```text
/bidops/opportunities
/bidops/opportunities/:id
/bidops/opportunities/watchlist
/bidops/opportunities/deadlines
/bidops/notices
/bidops/notices/:id
/bidops/packages
/bidops/packages/:id
```

### 8.5 后端 API

保留现有：

```http
GET /api/bidops/notices
GET /api/bidops/packages
GET /api/bidops/packages/{id}/requirements
```

建议新增：

```http
GET    /api/bidops/notices/{id}
GET    /api/bidops/notices/{id}/packages
GET    /api/bidops/packages/{id}
GET    /api/bidops/packages/{id}/timeline

GET    /api/bidops/opportunities
POST   /api/bidops/opportunities/from-package/{packageId}
GET    /api/bidops/opportunities/{id}
PUT    /api/bidops/opportunities/{id}
POST   /api/bidops/opportunities/{id}/stage
POST   /api/bidops/opportunities/{id}/watch
POST   /api/bidops/opportunities/{id}/unwatch
POST   /api/bidops/opportunities/{id}/assess-value
```

### 8.6 权限

```text
bidops.business.read
bidops.opportunity.read
bidops.opportunity.manage
bidops.opportunity.watch
bidops.opportunity.assess
```

---

## 9. 模块五：采购方与代理机构画像

### 9.1 目标

基于公开公告和公开结果，沉淀采购方、招标代理机构、地区、行业、品类的公开历史画像。

该模块只能处理公开信息，不得采集或录入非公开关系、内部消息、专家名单、返点、好处费等内容。

### 9.2 价值

```text
了解采购方常见采购品类
了解采购方历史预算区间
了解代理机构公告格式与文件习惯
了解地区/行业项目节奏
辅助判断商机价值
辅助选择厂家能力要求
```

### 9.3 核心对象

```text
BuyerProfile
AgencyProfile
PublicOrganizationAlias
BuyerProcurementStat
AgencyNoticePattern
PublicAwardHistory
CategoryRegionStat
```

### 9.4 前端路由

```text
/bidops/public-orgs/buyers
/bidops/public-orgs/buyers/:id
/bidops/public-orgs/agencies
/bidops/public-orgs/agencies/:id
/bidops/public-orgs/statistics
```

### 9.5 后端 API

```http
GET /api/bidops/public-orgs/buyers
GET /api/bidops/public-orgs/buyers/{id}
GET /api/bidops/public-orgs/buyers/{id}/history
GET /api/bidops/public-orgs/buyers/{id}/stats

GET /api/bidops/public-orgs/agencies
GET /api/bidops/public-orgs/agencies/{id}
GET /api/bidops/public-orgs/agencies/{id}/history
GET /api/bidops/public-orgs/agencies/{id}/patterns
```

### 9.6 后台任务

```text
bidops.intelligence.public-org-profile-refresh
bidops.analytics.public-org-stat-refresh
```

### 9.7 权限

```text
bidops.public-org.read
bidops.public-org.manage-alias
```

---

## 10. 模块六：厂家能力库

### 10.1 目标

管理厂家、联系人、产品能力、资质文件、业绩材料、授权材料、材料有效期，为后续匹配和投标作业提供基础。

### 10.2 核心对象

```text
Supplier
SupplierContact
SupplierCapability
SupplierProductLine
SupplierQualification
SupplierEvidenceDocument
SupplierPerformance
SupplierMaterialExpiry
SupplierTag
```

### 10.3 前端路由

```text
/bidops/suppliers
/bidops/suppliers/:id
/bidops/suppliers/:id/capabilities
/bidops/suppliers/:id/evidence-documents
/bidops/suppliers/:id/performance
/bidops/suppliers/material-expiry
```

### 10.4 后端 API

```http
GET    /api/bidops/suppliers
POST   /api/bidops/suppliers
GET    /api/bidops/suppliers/{id}
PUT    /api/bidops/suppliers/{id}

GET    /api/bidops/suppliers/{id}/contacts
POST   /api/bidops/suppliers/{id}/contacts
PUT    /api/bidops/suppliers/{id}/contacts/{contactId}

GET    /api/bidops/suppliers/{id}/capabilities
POST   /api/bidops/suppliers/{id}/capabilities
PUT    /api/bidops/suppliers/{id}/capabilities/{capabilityId}

GET    /api/bidops/suppliers/{id}/evidence-documents
POST   /api/bidops/suppliers/{id}/evidence-documents
PUT    /api/bidops/suppliers/{id}/evidence-documents/{documentId}

GET    /api/bidops/suppliers/material-expiry
```

### 10.5 后台任务

```text
bidops.supplier.evidence-expiry-scan
bidops.supplier.profile-quality-audit
```

### 10.6 权限

```text
bidops.supplier.read
bidops.supplier.manage
bidops.supplier.evidence.read
bidops.supplier.evidence.manage
```

---

## 11. 模块七：匹配与立项决策台

### 11.1 目标

围绕包件，推荐合适厂家，并支持人工做 Go / No-Go 决策。

### 11.2 核心对象

```text
SupplierMatchRun
SupplierMatchResult
SupplierMatchReason
SupplierMissingEvidence
SupplierRiskItem
GoNoGoDecision
DecisionRecord
```

### 11.3 匹配输出

```text
匹配分
推荐等级
满足项
缺失项
风险项
材料缺口
建议动作
可解释原因
```

### 11.4 前端路由

```text
/bidops/matching
/bidops/matching/package/:packageId
/bidops/matching/runs/:runId
/bidops/decisions
/bidops/decisions/:id
```

### 11.5 后端 API

```http
POST /api/bidops/packages/{id}/match-suppliers
GET  /api/bidops/packages/{id}/matches
GET  /api/bidops/match-runs/{id}
POST /api/bidops/match-results/{id}/confirm
POST /api/bidops/match-results/{id}/reject

POST /api/bidops/packages/{id}/go-no-go
GET  /api/bidops/packages/{id}/decisions
GET  /api/bidops/decisions
```

### 11.6 后台任务

```text
bidops.matching.supplier-match-run
bidops.matching.missing-evidence-check
bidops.matching.score-refresh
```

### 11.7 权限

```text
bidops.matching.read
bidops.matching.run
bidops.matching.decide
```

---

## 12. 模块八：投标作业中心

### 12.1 目标

当包件和厂家确认后，创建投标作业项目，管理推进过程、时间线、任务、成员、跟进记录和状态。

### 12.2 核心对象

```text
Pursuit
PursuitMember
PursuitTask
PursuitTimelineEvent
PursuitFollowRecord
PursuitRisk
PursuitStatusHistory
```

### 12.3 作业状态建议

```text
Draft             草稿
Active            推进中
WaitingSupplier   等厂家材料
WaitingInternal   等内部处理
Submitted         已提交
Won               已中标
Lost              已失标
Abandoned         已放弃
Archived          已归档
```

### 12.4 前端路由

```text
/bidops/pursuits
/bidops/pursuits/:id
/bidops/pursuits/:id/overview
/bidops/pursuits/:id/timeline
/bidops/pursuits/:id/tasks
/bidops/pursuits/:id/follow-records
/bidops/pursuits/:id/risks
```

### 12.5 后端 API

```http
GET    /api/bidops/pursuits
POST   /api/bidops/pursuits
GET    /api/bidops/pursuits/{id}
PUT    /api/bidops/pursuits/{id}
POST   /api/bidops/pursuits/{id}/status

GET    /api/bidops/pursuits/{id}/tasks
POST   /api/bidops/pursuits/{id}/tasks
PUT    /api/bidops/pursuits/{id}/tasks/{taskId}
POST   /api/bidops/pursuits/{id}/tasks/{taskId}/complete

GET    /api/bidops/pursuits/{id}/follow-records
POST   /api/bidops/pursuits/{id}/follow-records

GET    /api/bidops/pursuits/{id}/timeline
GET    /api/bidops/pursuits/{id}/risks
```

### 12.6 后台任务

```text
bidops.pursuit.deadline-reminder-scan
bidops.pursuit.overdue-task-scan
bidops.pursuit.status-sync
```

### 12.7 权限

```text
bidops.pursuit.read
bidops.pursuit.manage
bidops.pursuit.task.manage
bidops.pursuit.follow-record.manage
```

---

## 13. 模块九：响应矩阵与文件中心

### 13.1 目标

围绕 RequirementItem 建立逐条响应矩阵，绑定厂家材料、响应文件、证明材料，做提交前检查。

### 13.2 核心对象

```text
ResponseMatrixItem
ResponseEvidenceBinding
BidDocument
BidDocumentVersion
SubmissionCheckRun
SubmissionCheckItem
DocumentTemplate
FileChecklist
```

### 13.3 前端路由

```text
/bidops/pursuits/:id/response-matrix
/bidops/pursuits/:id/files
/bidops/pursuits/:id/submission-check
/bidops/document-templates
```

### 13.4 后端 API

```http
GET  /api/bidops/pursuits/{id}/response-matrix
PUT  /api/bidops/pursuits/{id}/response-matrix/{itemId}
POST /api/bidops/pursuits/{id}/response-matrix/{itemId}/bind-evidence
POST /api/bidops/pursuits/{id}/response-matrix/generate

GET  /api/bidops/pursuits/{id}/files
POST /api/bidops/pursuits/{id}/files
GET  /api/bidops/pursuits/{id}/files/{fileId}/versions
POST /api/bidops/pursuits/{id}/submission-check
GET  /api/bidops/submission-check-runs/{id}
```

### 13.5 后台任务

```text
bidops.response.generate-matrix
bidops.response.submission-check
bidops.response.file-version-snapshot
```

### 13.6 权限

```text
bidops.response.read
bidops.response.manage
bidops.response.check
bidops.file.read
bidops.file.manage
```

---

## 14. 模块十：结果复盘中心

### 14.1 目标

记录中标、失标、废标、放弃结果，沉淀胜率、失败原因、厂家表现、品类表现、地区表现。

### 14.2 核心对象

```text
BidOutcome
BidOutcomeReason
OutcomeReview
SupplierPerformanceSnapshot
CategoryWinRateStat
RegionWinRateStat
PublicAwardResult
```

### 14.3 前端路由

```text
/bidops/outcomes
/bidops/outcomes/:id
/bidops/outcomes/review
/bidops/analytics/win-rate
/bidops/analytics/supplier-performance
/bidops/analytics/category-region
```

### 14.4 后端 API

```http
GET  /api/bidops/outcomes
POST /api/bidops/pursuits/{id}/outcome
GET  /api/bidops/outcomes/{id}
PUT  /api/bidops/outcomes/{id}
POST /api/bidops/outcomes/{id}/review

GET  /api/bidops/analytics/win-rate
GET  /api/bidops/analytics/supplier-performance
GET  /api/bidops/analytics/category-region
```

### 14.5 后台任务

```text
bidops.outcome.public-award-result-scan
bidops.analytics.win-rate-refresh
bidops.analytics.supplier-performance-refresh
```

### 14.6 权限

```text
bidops.outcome.read
bidops.outcome.manage
bidops.analytics.read
```

---

## 15. 模块十一：合规风控与审计中心

### 15.1 目标

保证系统不会演变成串标、围标、非公开信息处理或自动投标工具。

### 15.2 必须阻断或提示的行为

```text
同一包件多个厂家进入正式 Active Pursuit
同一人员为同一包件多个竞争厂家编制投标文件
不同厂家文件高度相似
不同厂家使用同一保证金账户、同一联系人、同一项目成员
跟进记录出现敏感词
录入非公开信息来源
配置登录态采集、验证码破解、绕过反爬
自动报价
自动投标
```

### 15.3 核心对象

```text
ComplianceRule
ComplianceCheck
ComplianceCheckResult
SensitiveWordHit
OperationAuditLog
ConflictCheckResult
```

### 15.4 前端路由

```text
/bidops/compliance
/bidops/compliance/checks
/bidops/compliance/rules
/bidops/compliance/sensitive-words
/bidops/audit-logs
```

### 15.5 后端 API

```http
GET  /api/bidops/compliance/checks
GET  /api/bidops/compliance/checks/{id}
POST /api/bidops/compliance/check-sensitive-text
POST /api/bidops/compliance/check-package-conflict
GET  /api/bidops/compliance/rules
PUT  /api/bidops/compliance/rules/{id}
GET  /api/bidops/audit-logs
```

### 15.6 后台任务

```text
bidops.compliance.pursuit-conflict-scan
bidops.compliance.sensitive-word-audit
bidops.compliance.audit-log-retention
```

### 15.7 权限

```text
bidops.compliance.read
bidops.compliance.manage
bidops.audit.read
```

---

## 16. 模块十二：后台任务与日志监控中心

### 16.1 目标

统一监控 Atlas 后台任务和 BidOps 专项流水线。

该模块承接 `CODEX_BIDOPS_BACKGROUND_TASKS_OBSERVABILITY_SPEC.md` 的设计，不在本说明书重复所有细节。

### 16.2 前端路由

```text
/ops
/ops/jobs
/ops/jobs/:id
/ops/recurring-tasks
/ops/workers
/ops/logs
/bidops/operations
/bidops/operations/jobs
/bidops/operations/channels
/bidops/operations/raw-notices/:id/pipeline
/bidops/operations/config
```

### 16.3 必须支持的 BidOps 流水线视图

```text
Channel Scan
  -> Detail Fetch / Manual Import
    -> RawNotice Created
      -> Attachment Download
        -> Text Extract
          -> Structured Parse
            -> Deduplicate / Version Detect
              -> ReviewTask Generated
                -> Approve
                  -> Notice / Package / Requirement Created
                    -> Opportunity Generated
                      -> Supplier Match
                        -> Pursuit Created
```

### 16.4 权限

```text
bidops.ops.read
bidops.ops.manage
ops.jobs.read
ops.jobs.manage
ops.logs.read
ops.workers.read
```

---

## 17. 模块十三：规则与配置中心

### 17.1 目标

让业务规则逐渐配置化，不要把所有枚举、评分、风险、字典、Prompt 都写死在代码中。

### 17.2 核心对象

```text
BidOpsDictionary
CategoryDictionary
RegionDictionary
NoticeTypeDictionary
RequirementTypeDictionary
EvidenceTypeDictionary
MatchingRuleSet
MatchingScoreRule
ComplianceRule
SensitiveWord
AiPromptTemplate
CrawlAdapterConfig
DeadlineRule
NotificationRule
```

### 17.3 前端路由

```text
/bidops/settings
/bidops/settings/dictionaries
/bidops/settings/categories
/bidops/settings/regions
/bidops/settings/requirement-types
/bidops/settings/evidence-types
/bidops/settings/matching-rules
/bidops/settings/compliance-rules
/bidops/settings/ai-prompts
/bidops/settings/notification-rules
```

### 17.4 后端 API

```http
GET  /api/bidops/settings/dictionaries
POST /api/bidops/settings/dictionaries
PUT  /api/bidops/settings/dictionaries/{id}

GET  /api/bidops/settings/matching-rules
POST /api/bidops/settings/matching-rules
PUT  /api/bidops/settings/matching-rules/{id}

GET  /api/bidops/settings/compliance-rules
PUT  /api/bidops/settings/compliance-rules/{id}

GET  /api/bidops/settings/ai-prompts
PUT  /api/bidops/settings/ai-prompts/{id}
```

### 17.5 权限

```text
bidops.config.read
bidops.config.manage
bidops.rules.read
bidops.rules.manage
```

---

## 18. 全局通知、待办与日历

通知、待办、日历不是单独一级模块，但应横跨多个模块。

### 18.1 场景

```text
待审核任务提醒
即将截止提醒
厂家材料过期提醒
投标任务逾期提醒
后台任务失败提醒
高风险响应项提醒
中标结果待回填提醒
```

### 18.2 前端入口

```text
顶部通知中心
/bidops/todos
/bidops/calendar
```

### 18.3 后端 API

```http
GET  /api/bidops/todos
POST /api/bidops/todos/{id}/complete
GET  /api/bidops/calendar
GET  /api/bidops/notifications
POST /api/bidops/notifications/{id}/read
```

### 18.4 后台任务

```text
bidops.notification.deadline-reminder
bidops.notification.evidence-expiry-reminder
bidops.notification.failed-job-reminder
bidops.notification.review-task-reminder
```

---

## 19. 新模块与现有能力映射

| 现有能力 | 新模块归属 | 处理方式 |
|---|---|---|
| CrawlSource / CrawlChannel | 情报采集中心 | 保留，路由可重命名，旧路由 redirect |
| RawNotice / RawAttachment | 情报采集中心 | 保留，补附件、日志、流水线视图 |
| NoticeStaging / PackageStaging / RequirementStaging | 解析审核中心 | 保留，补可编辑审核、置信度、重析 |
| ReviewTask | 解析审核中心 | 保留，补合并、重析、审核差异视图 |
| Notice / TenderPackage / RequirementItem | 商机包件中心 | 保留，新增 Opportunity 经营层 |
| BackgroundJob / Worker / Logs | 后台任务与日志监控中心 | 按观测文档补齐 |
| BidOpsCapabilities: Crawl / Review / Business | 旧权限组 | 保留兼容，新增更细权限 |
| Supplier / Matching / Pursuit | 愿景能力 | 目前若未实现，只做接口和页面占位，不得假装完成 |

---

## 20. 前端菜单建议

```text
BidOps
├── 指挥中心
├── 情报采集
│   ├── 采集来源
│   ├── 采集栏目
│   ├── 原始公告
│   ├── 手动导入
│   └── 采集运行日志
├── 解析审核
│   ├── 待审核池
│   ├── 解析失败
│   ├── 疑似重复
│   └── 变更版本
├── 商机包件
│   ├── 商机列表
│   ├── 关注商机
│   ├── 正式公告
│   ├── 包件中心
│   └── 截止日历
├── 采购画像
│   ├── 采购方
│   ├── 代理机构
│   └── 公开历史统计
├── 厂家能力
│   ├── 厂家列表
│   ├── 材料有效期
│   └── 能力标签
├── 匹配决策
│   ├── 包件匹配
│   ├── 匹配记录
│   └── 立项决策
├── 投标作业
│   ├── 作业列表
│   ├── 我的任务
│   └── 作业日历
├── 响应文件
│   ├── 响应矩阵
│   ├── 文件清单
│   ├── 提交检查
│   └── 模板库
├── 结果复盘
│   ├── 结果录入
│   ├── 复盘列表
│   └── 经营分析
├── 合规审计
│   ├── 合规检查
│   ├── 敏感词
│   └── 操作审计
├── 运维监控
│   ├── 任务看板
│   ├── 日志查询
│   ├── Worker 心跳
│   └── 配置检查
└── 规则配置
    ├── 字典配置
    ├── 匹配规则
    ├── 合规规则
    ├── AI Prompt
    └── 通知规则
```

---

## 21. 后端目录建议

不要把所有服务继续堆在 `Services/` 根目录。建议逐步演进为：

```text
src/Atlas.Modules.BidOps/
  Controllers/
    Intelligence/
    Processing/
    Opportunities/
    PublicOrgs/
    Suppliers/
    Matching/
    Pursuits/
    Responses/
    Outcomes/
    Compliance/
    Operations/
    Settings/

  Entities/
    Crawling/
    Staging/
    Tendering/
    Opportunities/
    PublicOrgs/
    Suppliers/
    Matching/
    Pursuits/
    Responses/
    Outcomes/
    Compliance/
    Settings/

  Services/
    Intelligence/
    Processing/
    Opportunities/
    PublicOrgs/
    Suppliers/
    Matching/
    Pursuits/
    Responses/
    Outcomes/
    Compliance/
    Operations/
    Settings/

  Models/
    Intelligence/
    Processing/
    Opportunities/
    PublicOrgs/
    Suppliers/
    Matching/
    Pursuits/
    Responses/
    Outcomes/
    Compliance/
    Operations/
    Settings/

  BackgroundJobs/
    Intelligence/
    Processing/
    Suppliers/
    Matching/
    Pursuits/
    Responses/
    Outcomes/
    Compliance/
    Notifications/
```

如果当前项目体量还小，可以先只创建文档，不要立刻移动文件，避免大面积破坏引用。

---

## 22. 前端目录建议

```text
frontend/atlas-admin/src/modules/bidops/
  dashboard/
  intelligence/
  processing/
  opportunities/
  public-orgs/
  suppliers/
  matching/
  pursuits/
  responses/
  outcomes/
  compliance/
  operations/
  settings/
  shared/
```

每个子模块内部统一：

```text
api.ts
routes.ts
types.ts
pages/
components/
composables/
```

如果之前已经使用：

```text
modules/bidops/pages/crawl
modules/bidops/pages/review
modules/bidops/pages/business
```

则先兼容，不要强制重构。新增模块按新目录组织，旧目录后续再迁移。

---

## 23. 权限体系扩展

### 23.1 保留旧权限

```text
bidops.crawl.read
bidops.crawl.manage
bidops.crawl.import
bidops.review.read
bidops.review.approve
bidops.business.read
```

### 23.2 新增权限

```text
bidops.dashboard.read

bidops.intelligence.read
bidops.intelligence.manage
bidops.intelligence.import
bidops.intelligence.refetch

bidops.processing.read
bidops.processing.reparse
bidops.processing.manage

bidops.review.read
bidops.review.approve
bidops.review.merge

bidops.opportunity.read
bidops.opportunity.manage
bidops.opportunity.watch
bidops.opportunity.assess

bidops.public-org.read
bidops.public-org.manage-alias

bidops.supplier.read
bidops.supplier.manage
bidops.supplier.evidence.read
bidops.supplier.evidence.manage

bidops.matching.read
bidops.matching.run
bidops.matching.decide

bidops.pursuit.read
bidops.pursuit.manage
bidops.pursuit.task.manage
bidops.pursuit.follow-record.manage

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

bidops.ops.read
bidops.ops.manage

bidops.config.read
bidops.config.manage
bidops.rules.read
bidops.rules.manage
```

### 23.3 执行要求

- 旧权限不得删除，防止已实现接口失效。
- 新权限先加入常量和菜单元数据。
- 未实现的接口不要因为权限存在就显示可用按钮。
- 前端菜单可按权限隐藏，但后端必须继续做权限校验。

---

## 24. 后台任务全景

BidOps 后台任务不应只覆盖抓取。完整任务全景如下。

```text
采集类
  bidops.crawl.source-scan
  bidops.crawl.channel-scan
  bidops.crawl.detail-fetch
  bidops.crawl.manual-url-import

文档类
  bidops.document.attachment-discover
  bidops.document.attachment-download
  bidops.document.text-extract
  bidops.document.file-snapshot

AI/解析类
  bidops.ai.structured-parse
  bidops.ai.mock-parse
  bidops.ai.reparse

治理类
  bidops.governance.deduplicate
  bidops.governance.version-detect
  bidops.governance.generate-review-task
  bidops.governance.data-quality-audit

商机类
  bidops.opportunity.value-assessment
  bidops.opportunity.deadline-reminder
  bidops.opportunity.watch-reminder

采购画像类
  bidops.public-org.profile-refresh
  bidops.public-org.stats-refresh

厂家类
  bidops.supplier.evidence-expiry-scan
  bidops.supplier.profile-quality-audit

匹配类
  bidops.matching.supplier-match-run
  bidops.matching.missing-evidence-check
  bidops.matching.score-refresh

投标作业类
  bidops.pursuit.deadline-reminder-scan
  bidops.pursuit.overdue-task-scan
  bidops.pursuit.status-sync

响应核验类
  bidops.response.generate-matrix
  bidops.response.submission-check
  bidops.response.file-version-snapshot

结果复盘类
  bidops.outcome.public-award-result-scan
  bidops.analytics.win-rate-refresh
  bidops.analytics.supplier-performance-refresh

合规类
  bidops.compliance.pursuit-conflict-scan
  bidops.compliance.sensitive-word-audit
  bidops.compliance.audit-log-retention

通知类
  bidops.notification.deadline-reminder
  bidops.notification.evidence-expiry-reminder
  bidops.notification.failed-job-reminder
  bidops.notification.review-task-reminder

维护类
  bidops.maintenance.cleanup-old-snapshots
  bidops.maintenance.rebuild-search-index
```

现阶段只实现已有任务和必须任务，不要一次性补全所有后台任务。

---

## 25. 数据模型优先级

### 25.1 已有模型继续稳定

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
```

### 25.2 下一批必须补的模型

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
GoNoGoDecision
Pursuit
PursuitTask
PursuitFollowRecord
ResponseMatrixItem
ResponseEvidenceBinding
SubmissionCheckRun
BidOutcome
ComplianceCheck
SensitiveWordHit
BidOpsNotification
```

### 25.3 再下一批增强模型

```text
BuyerProfile
AgencyProfile
PublicOrganizationAlias
BuyerProcurementStat
AgencyNoticePattern
SupplierProductLine
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

---

## 26. 编码实施顺序

### Phase A：模块蓝图与菜单重构，不改核心业务

交付：

```text
docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md
docs/BIDOPS_MODULE_GAPS.md
frontend BidOps 菜单结构草案
BidOps 权限常量扩展草案
```

要求：

- 不删除现有控制器。
- 不删除现有权限。
- 不迁移已有实体。
- 不引入大规模数据库迁移。
- 未实现模块标记为 ComingSoon 或 Disabled。

### Phase B：指挥中心 + 商机经营层

交付：

```text
Opportunity 实体
Opportunity API
Opportunity 页面
Dashboard summary API
Dashboard 页面
Deadline / Watchlist
```

原因：

当前 Notice / Package 只是数据，Opportunity 才是业务经营对象。

### Phase C：厂家能力库

交付：

```text
Supplier
SupplierContact
SupplierCapability
SupplierEvidenceDocument
材料有效期提醒
厂家详情页
```

### Phase D：匹配与立项

交付：

```text
SupplierMatchRun
SupplierMatchResult
规则评分服务
缺失材料检测
Go / No-Go 决策
匹配决策台页面
```

### Phase E：投标作业与响应矩阵

交付：

```text
Pursuit
PursuitTask
FollowRecord
ResponseMatrixItem
SubmissionCheckRun
作业舱页面
响应矩阵页面
提交前检查
```

### Phase F：合规与结果复盘

交付：

```text
ComplianceCheck
SensitiveWordHit
BidOutcome
OutcomeReview
胜率分析
厂家表现分析
```

### Phase G：规则配置与持续运营

交付：

```text
字典配置
匹配规则配置
AI Prompt 配置
通知规则配置
后台任务配置检查
```

---

## 27. 前端占位规则

对于未实现后端 API 的模块，前端可以创建菜单和路由，但必须满足：

- 页面显示“规划中 / 后端接口未实现”。
- 不调用不存在的 API。
- 不假造成功数据。
- 不把 Mock 数据当真实数据展示。
- 在页面中提供所需 API 清单。
- 在 `docs/BIDOPS_MODULE_GAPS.md` 中记录缺口。

---

## 28. 后端占位规则

后端不要为了凑 API 随便返回空数据。可以选择：

1. 不创建未实现 Controller。
2. 创建 Controller 但返回 `501 NotImplemented`，并在 Swagger 描述中写明未实现。
3. 先只创建 DTO 和接口，不注册路由。

优先策略：

```text
先文档化缺口 -> 再实现真实模型 -> 再实现服务 -> 再开放 API -> 最后接前端
```

---

## 29. 合规边界再次强调

无论模块如何扩展，以下能力禁止实现：

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

采购方与代理机构画像只能基于公开公告、公开结果、公开附件、公开网页信息。

---

## 30. Codex 本次应执行的最小任务

如果本说明书作为单独任务交给 Codex，建议本次只执行以下最小安全范围：

```text
1. 创建 docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md
2. 创建 docs/BIDOPS_MODULE_GAPS.md
3. 在 docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md 中写入 13 个模块蓝图、路由、API、权限、任务映射
4. 检查当前 src/Atlas.Modules.BidOps 与蓝图差异
5. 在 docs/BIDOPS_MODULE_GAPS.md 中列出：已实现、部分实现、未实现
6. 如前端项目已存在，只新增菜单分组常量/路由占位，不调用不存在 API
7. 如后端常量适合扩展，可只追加权限常量，不改旧权限、不改旧接口
8. 运行 dotnet build；如果前端存在，运行 pnpm build 或 npm run build
9. 在最终总结中列出改动、未实现项、下一步建议
```

不要在本次任务中一次性创建所有实体、所有 Controller、所有迁移、所有页面。那会导致范围过大、回归风险高。

---

## 31. 验收标准

本次蓝图任务验收：

```text
docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md 存在
13 个模块定义清晰
每个模块有目标、前端路由、后端 API、核心对象、权限、后台任务
现有模块与新模块映射清晰
docs/BIDOPS_MODULE_GAPS.md 存在
Gaps 中明确哪些已实现、哪些部分实现、哪些未实现
现有 API 没有被删除或改名
现有权限没有被删除
项目 build 不被破坏
```

后续实现任务验收应按 Phase B～G 分批进行。

---

## 32. 最终结论

当前 BidOps 的 4 个粗模块更像 MVP 骨架，不足以承载“招投标运营操作系统”的愿景。

应升级为 13 个一级模块：

```text
指挥中心
情报采集中心
解析审核中心
商机包件中心
采购方与代理机构画像
厂家能力库
匹配与立项决策台
投标作业中心
响应矩阵与文件中心
结果复盘中心
合规风控与审计中心
后台任务与日志监控中心
规则与配置中心
```

其中真正决定产品价值的，不是采集本身，而是：

```text
包件级商机经营
厂家能力调度
可解释匹配决策
投标作业交付
响应矩阵核验
结果复盘增长
```

Codex 后续应围绕这些模块逐步实现，而不是继续围绕“公告列表 + 审核列表 + 包件列表”扩展。
