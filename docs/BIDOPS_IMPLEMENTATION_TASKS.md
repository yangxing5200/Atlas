# BidOps Implementation Tasks

> 更新日期：2026-06-12
> 来源文档：`docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md`、`docs/BIDOPS_MODULE_GAPS.md`
> 执行原则：增量实现，保留现有 Controller、权限、接口和前端可用路由；未实现后端 API 不返回假成功数据。

## 1. 状态口径

| 状态 | 含义 |
|---|---|
| Done | 已在当前代码中完成并有验证记录。 |
| In Progress | 正在当前批次实现。 |
| Next | 下一批优先实现。 |
| Planned | 已进入路线图，但尚未开始。 |
| Blocked | 受外部依赖、凭据、环境或架构前置条件阻塞。 |

## 2. 当前批次

| 编号 | 任务 | 模块 | 状态 | 验收点 |
|---|---|---|---|---|
| A-01 | 13 模块产品蓝图 | 全局 | Done | `docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md` 完成模块目标、路由、API、权限、后台任务和核心对象。 |
| A-02 | 当前源码缺口对照 | 全局 | Done | `docs/BIDOPS_MODULE_GAPS.md` 标出已实现、部分实现、未实现。 |
| A-03 | 非破坏性演进规则 | 全局 | Done | 文档和决策记录明确保留现有控制器、权限、接口和路由。 |
| A-04 | 前端 13 模块菜单和路由占位 | 全局 | Done | 未实现模块进入 `ComingSoon`，不调用不存在 API。 |
| A-05 | 未实现 API 语义守卫 | 全局 | Done | 后端缺口不得返回 `200 OK` 假成功；规划 API 可保持 404 或显式 501。 |
| P0-01 | 运维权限增量兼容 | 后台任务与日志监控中心 | Done | 已追加 `bidops.ops.read/manage` 到授权目录和前端常量，旧 `crawl` 权限继续兼容。 |
| P0-02 | RawNotice pipeline 只读 API | 情报采集 / 运维监控 | Done | 已新增真实查询 `GET /api/bidops/raw-notices/{id}/pipeline` 和运维别名；不造数据，Raw 不存在返回 404。 |
| P0-03 | RawNotice pipeline 前端入口 | 情报采集 / 运维监控 | Done | 原始公告详情已展示采集、附件、解析、审核、入库流水线。 |

## 3. P0 稳定性任务

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| P0-04 | 采集运行日志详情页 | 情报采集中心 | Done | 已完成 `CrawlRunLog` 查询 API、列表/详情页、失败原因和耗时展示。 |
| P0-05 | 解析失败业务队列 | 解析审核中心 | Done | 已完成解析失败列表，关联 RawNotice 详情；重试入口由 P0-06 重解析实现。 |
| P0-06 | 重解析入口 | 解析审核中心 | Done | 已完成 `POST /api/bidops/raw-notices/{id}/reparse`，真实入队，不直接同步解析。 |
| P0-07 | Worker heartbeat | 运维监控中心 | Done | 已完成 Global `BackgroundWorkerHeartbeat`、`GET /api/ops/workers`、`/ops/workers` 和 `/bidops/operations/worker-heartbeats`。 |
| P0-08 | 附件受控下载/预览 | 情报采集 / 文件中心 | Done | 已完成 `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file`，支持授权预览/下载，不暴露 storage key。 |

## 4. Phase B 商机经营

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| B-01 | Opportunity 实体与迁移 | 商机包件中心 | Done | 已新增 `Opportunity`、`OpportunityStageHistory`、`OpportunityWatch` 和 tenant migration；包件最多一个 active opportunity。 |
| B-02 | 商机 API | 商机包件中心 | Done | 已实现 `GET/POST/PUT /api/bidops/opportunities`、详情、关注、评估、状态流转；未命中资源返回 404，不造成功数据。 |
| B-03 | 商机前端 | 商机包件中心 | Done | 已完成商机列表、详情、关注列表、评估、状态流转和包件详情创建商机入口；截止日历仍为 `ComingSoon`。 |
| B-04 | 业务指挥中心 | 指挥中心 | Done | 已实现 `GET /api/bidops/dashboard/summary` 和 `/bidops/dashboard` 真实页面，展示今日新增、待办、截止风险、商机漏斗、高价值商机。 |
| B-05 | 商机后台任务 | 商机包件中心 | Done | 已实现 `bidops.opportunity.value-assessment`、`deadline-reminder`、`watch-reminder`、`stale-state-scan`，由 OpportunityMaintenance 周期任务入队。 |

## 5. Phase C 厂家能力库

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| C-01 | 厂家基础模型 | 厂家能力库 | Done | 已新增 `Supplier`、`SupplierContact`、`SupplierCapability` 和 tenant migration；表名使用 `bidops_` 前缀。 |
| C-02 | 资质材料模型 | 厂家能力库 | Done | 已新增 `SupplierEvidenceDocument`，只保存文件元数据和有效期；二进制文件继续走文件存储，不进 MySQL。 |
| C-03 | 厂家 API 和页面 | 厂家能力库 | Done | 已实现厂家列表、详情、新增/编辑、联系人、能力标签、资质材料元数据；前端使用中文标题和类型。 |
| C-04 | 材料有效期任务 | 厂家能力库 | Done | 已实现 `bidops.supplier.evidence-expiry-scan` 和 SupplierMaintenance 周期入队；当前更新过期/临期状态，通知后置。 |
| C-05 | 厂家分析汇总 | 厂家能力库 | Done | 已实现 `GET /api/bidops/suppliers/analysis/summary` 和 `/bidops/suppliers/analysis` 页面，基于厂家、能力、材料、匹配、立项、作业和公开结果厂家线索真实数据汇总。 |
| C-06 | 公开结果厂家线索 | 厂家能力库 / 结果复盘中心 | Done | 已新增 `OutcomeSupplierRecord`、`bidops.outcome.supplier-extract`、结果线索列表/汇总/回填 API，以及包件历史厂家线索 API；只抽取公开中标/成交/候选公示中的明确厂家明细，不自动创建厂家主档或联系厂家。 |

## 6. Phase D 匹配与立项

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| D-01 | 匹配运行模型 | 匹配与立项决策台 | Done | 已新增 `SupplierMatchRun`、`SupplierMatchResult`、`MissingEvidenceCheck` 和 tenant migration。 |
| D-02 | 匹配 API 和后台任务 | 匹配与立项决策台 | Done | 已实现 `POST /api/bidops/packages/{id}/match-suppliers`、运行查询、结果查询和 `bidops.matching.supplier-match-run` Worker 任务。 |
| D-03 | Go/No-Go 决策 | 匹配与立项决策台 | Done | 已新增 `GoNoGoDecision`，支持包件维度人工登记决策、理由和风险摘要。 |
| D-04 | 匹配前端 | 匹配与立项决策台 | Done | 已完成包件匹配入口、匹配记录列表、匹配详情、候选厂家、缺失材料和立项决策展示。 |

## 7. Phase E 投标作业与响应

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| E-01 | 投标作业模型 | 投标作业中心 | Done | 已新增 `Pursuit`、`PursuitTask`、`PursuitFollowRecord` 和 tenant migration；一个包件最多一个 active pursuit。 |
| E-02 | 作业 API 和核心页面 | 投标作业中心 | Done | 已实现作业列表、详情、任务、跟进记录、状态流转和包件详情创建作业入口；作业日历仍为 `ComingSoon`。 |
| E-03 | 响应矩阵模型 | 响应矩阵与文件中心 | Planned | `ResponseMatrixItem`、`ResponseEvidenceBinding`。 |
| E-04 | 文件与模板模型 | 响应矩阵与文件中心 | Planned | `BidDocument`、`BidDocumentVersion`、`DocumentTemplate`。 |
| E-05 | 提交检查任务 | 响应矩阵与文件中心 | Planned | 仅做漏项和合规检查，不自动提交投标文件。 |

## 8. Phase F 结果复盘与合规

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| F-01 | 结果模型与 API | 结果复盘中心 | Planned | `BidOutcome`、结果录入、候选/中标/失标/废标状态。 |
| F-02 | 复盘模型与页面 | 结果复盘中心 | Planned | `OutcomeReview`、原因分析、行动项。 |
| F-03 | 经营分析 | 结果复盘中心 | Planned | 胜率、地区、品类、正式厂家表现 `SupplierPerformance` 统计；已落地的 `OutcomeSupplierRecord` 只作为公开结果厂家线索来源。 |
| F-04 | 合规模型 | 合规风控与审计中心 | Planned | `ComplianceCheck`、`SensitiveWordHit`、`ComplianceRule`、`BidOpsAuditLog`。 |
| F-05 | 合规检查任务 | 合规风控与审计中心 | Planned | 冲突扫描、敏感词审计、审计留存。 |

## 9. Phase G 规则与配置

| 编号 | 任务 | 模块 | 状态 | 主要交付 |
|---|---|---|---|---|
| G-01 | 字典配置 | 规则与配置中心 | Planned | `BidOpsDictionaryItem` 和前端维护页。 |
| G-02 | 匹配规则 | 规则与配置中心 | Planned | `MatchingRuleSet`，规则版本和启停。 |
| G-03 | 合规规则 | 规则与配置中心 | Planned | 合规规则版本、启停、审计。 |
| G-04 | AI Prompt 管理 | 规则与配置中心 | Planned | `AiPromptTemplate`、验证、版本管理。 |
| G-05 | 通知规则和功能开关 | 规则与配置中心 | Planned | `NotificationRule`、`BidOpsFeatureSwitch`。 |

## 10. 每批执行检查

每一批完成前必须检查：

- 是否保留现有 Controller、API 路由、权限码、实体和前端可用路由。
- 是否所有新增 API 都有真实实体、服务或查询支撑。
- 是否未实现能力只进入文档或 `ComingSoon`，不调用不存在 API。
- 是否长任务只入队，实际执行在 Worker 或 Atlas background task。
- 是否继续使用 repository、QueryService、UnitOfWork 和数据范围约定。
- 是否更新 `docs/IMPLEMENTATION_LOG.md`，有新增取舍时更新 `docs/DECISIONS.md`。
- 是否运行可用的后端测试、前端 typecheck/build 或记录失败原因。
