# BidOps 人工审核减负升级任务清单

> 目标：把审核工作从“逐字段人工核对”升级为“系统自动质检 + 异常优先复核 + 低风险批量确认”，在不绕过人工审核、不降低追溯性的前提下减少人工工作量。

## 状态约定

- `Pending`：未开始。
- `In Progress`：正在实现。
- `Done`：代码、测试、文档更新已完成。
- `Blocked`：存在明确阻塞，需要记录原因。

## 总体原则

- Raw / Staging / Formal 三层继续分离。AI、规则、自动质检只能写 Staging、建议表或质量字段，不能绕过人工审核直接导入 Formal。
- WebApi 只做查询、人工确认、批量操作入口和任务入队；批量质检、历史回填、纠错样本分析走 Worker/后台任务。
- 自动通过只能是“通过建议”或“批量确认候选”，最终仍需要有权限的人工用户触发确认。
- 所有质量评分、异常原因、自动判断依据必须可追溯，能回到原始公告、附件、表格行、DeepSeek 响应或规则解析结果。
- 金额统一按人民币元存储；金额单位不明确、百分比/折扣/费率混淆必须进入异常复核。
- 任何功能不得帮助串标、规避审核、绕过公开来源限制、访问非公开数据或隐藏人工责任。

## 核心流程

```text
Raw 公告与附件
↓
规则解析 + DeepSeek 解析
↓
Staging 暂存事实
↓
自动质量评分与异常检测
↓
审核队列分层：低风险 / 普通复核 / 重点复核
↓
低风险批量确认；异常字段逐项复核
↓
人工修改沉淀为纠错样本
↓
规则、提示词、别名字典持续改进
```

## 任务列表

| 编号 | 状态 | 任务 | 可测试产出 |
|---|---|---|---|
| R0 | Done | 建立审核减负升级计划 | 本文档存在，后续任务可逐项更新状态 |
| R1 | Done | 定义质量评分与异常模型 | DTO/实体/枚举/迁移设计完成；模型配置测试通过 |
| R2 | Done | 实现采购公告质量评分服务 | 采购公告 Staging 能生成质量分、风险等级、异常项；单元测试覆盖金额、包号、资质异常 |
| R3 | Done | 实现中标/候选公告质量评分服务 | 中标/候选明细能生成质量分、风险等级、异常项；单元测试覆盖金额单位、厂家、包件匹配 |
| R4 | Done | 审核任务队列分层与筛选 | Review list 支持低风险/普通/重点筛选；API 查询测试通过 |
| R5 | Done | 审核详情页异常优先展示 | 页面默认展示异常字段、差异依据和原文证据；前端 typecheck 通过 |
| R6 | Done | 低风险批量确认 | 批量通过只允许低风险待审核任务；权限、状态、事务测试通过 |
| R7 | Done | 批量 DeepSeek 提示词重解析 | 可对选中待审核任务批量提交同一提示词；任务入队和幂等测试通过 |
| R8 | Done | 人工纠错样本沉淀 | 字段修改、批量确认、重解析提示形成纠错样本；不存密钥/敏感请求；测试通过 |
| R9 | Done | 纠错样本分析与规则改进看板 | 可查看高频错误表头、字段、公告类型；查询接口和页面 typecheck 通过 |
| R10 | Done | 历史任务质量回填 | Worker 支持批量大小、来源暂停、dry-run；回填测试通过 |
| R11 | Done | 审核效率指标与运行手册 | 看板显示待审量、低风险占比、批量通过数、人均处理量；Runbook 更新 |

## R1 详细验收：质量评分与异常模型

产出：

- 新增质量风险等级枚举：`Low`、`Medium`、`High`。
- 新增异常类型枚举或常量：
  - `MissingProjectCode`
  - `MissingLotOrPackage`
  - `AmbiguousAmountUnit`
  - `RateOrDiscountInAmountColumn`
  - `AiRuleMismatch`
  - `MissingQualificationRequirement`
  - `MissingPerformanceRequirement`
  - `MissingPersonnelRequirement`
  - `LifecycleMatchMissing`
  - `LifecycleMatchConflict`
  - `DuplicatePackageIdentity`
  - `OriginalEvidenceMissing`
- 推荐新增 `bidops_review_quality_issue` 表：
  - `Id`
  - `TenantId`
  - `ReviewTaskId`
  - `RawNoticeId`
  - `NoticeStagingId`
  - `PackageStagingId`
  - `OutcomeSupplierRecordId`
  - `ProcurementDetailStagingId`
  - `IssueType`
  - `Severity`
  - `FieldName`
  - `Message`
  - `EvidenceJson`
  - `IsResolved`
  - `ResolvedBy`
  - `ResolvedAt`
  - `CreatedAt`
- 在 `ReviewTaskDto` / `ReviewTaskDetailDto` 暴露：
  - `QualityScore`
  - `RiskLevel`
  - `QualityIssueCount`
  - `HighRiskIssueCount`
  - `ReviewRecommendation`
  - `QualityIssues`

测试：

- EF 配置测试覆盖表名、租户索引、ReviewTask/RawNotice 查询索引。
- DTO 映射测试覆盖质量分、风险等级、异常项数量。

## R2 详细验收：采购公告质量评分服务

产出：

- 新增 `IBidOpsReviewQualityService` 或同等领域服务。
- 采购公告评分规则：
  - 采购编号缺失：中风险。
  - 分标编号/包号/包名称缺失：中风险，多个包号重复且分标不同缺失时高风险。
  - 金额列表头含 `%`、税率、折扣率、费率但进入金额字段：高风险。
  - 金额列表头或单元格含 `万元/万` 但金额未乘 10000：高风险。
  - 金额无单位但字段为金额：按元处理，不自动判高风险；若数值异常小且附件表头附近出现 `万元`，标记中/高风险。
  - 资质/业绩/人员要求全部缺失：中风险。
  - 采购明细原始行 JSON 缺失：中风险。
  - DeepSeek 与规则解析在包号、金额、资质要求上冲突：按字段重要性标记中/高风险。
- 输出 `QualityScore` 初始建议：
  - 100 起分。
  - 高风险每项扣 30。
  - 中风险每项扣 15。
  - 低风险每项扣 5。
  - 小于 0 归 0。
- 风险等级建议：
  - `Low`：分数 >= 85 且无高风险。
  - `Medium`：分数 60-84 或有中风险。
  - `High`：分数 < 60 或存在高风险。

测试：

- `万元` 表头未乘金额生成高风险。
- `最高限价（%）` 不进入金额字段，否则生成高风险。
- 包号缺失生成中风险。
- 资质/业绩/人员均缺失生成中风险。
- 完整采购公告样例生成低风险。

## R3 详细验收：中标/候选公告质量评分服务

产出：

- 中标/候选公告评分规则：
  - 厂家名称缺失：高风险。
  - 包号和分标编号均缺失：高风险。
  - 中标金额无单位按元；明确 `万元/万` 必须乘 10000。
  - 价格字段实际是折扣率、费率、得分：高风险。
  - 候选人排名缺失但公告是候选人公示：中风险。
  - 中标/候选结果无法匹配采购公告包件：中风险；多个候选包件冲突：高风险。
  - DeepSeek 返回顺序与公告表格顺序不一致时不自动排序，标记低/中风险供复核。
- `OutcomeSupplierRecord` 或质量 issue 能关联证据文本和原表格行。

测试：

- 明确 `万元` 中标金额归一为元后低风险。
- `95%`、`折扣率0.95` 不写入 `AwardAmount`，生成高风险或金额缺失异常。
- 同一包号跨多个分标不误匹配。
- 厂家名缺失生成高风险。

## R4 详细验收：审核队列分层与筛选

产出：

- Review task 搜索支持：
  - `riskLevel`
  - `minQualityScore`
  - `maxQualityScore`
  - `hasHighRiskIssue`
  - `reviewRecommendation`
  - `issueType`
- 列表显示：
  - 质量分
  - 风险等级
  - 高风险数/异常总数
  - 推荐操作：`BatchConfirmCandidate`、`NeedsReview`、`NeedsReparse`
- 默认排序：
  - 高风险优先。
  - 同风险下更新时间/创建时间倒序。

测试：

- API 查询能按风险等级筛选。
- 低风险筛选不包含高风险 issue。
- 前端 typecheck 通过。

## R5 详细验收：审核详情页异常优先展示

产出：

- 审核详情页新增“异常复核”区域。
- 默认展示异常字段，而不是让审核员从全部字段里找问题。
- 每个异常项显示：
  - 字段名
  - 风险等级
  - 异常说明
  - 系统建议
  - 规则值 / DeepSeek 值 / 人工当前值
  - 原文证据或附件表格行入口
- 支持一键定位：
  - 原始公告正文。
  - 附件文本。
  - 原始行 JSON。
  - DeepSeek 返回。
- 异常已处理后可标记解决，但标记解决不等于审核通过。

测试：

- 有异常时默认显示异常列表。
- 无异常低风险任务显示“可批量确认候选”。
- `npm run typecheck` 通过。
- 窄屏无明显文本重叠。

## R6 详细验收：低风险批量确认

产出：

- 新增批量审核 API，例如：
  - `POST /api/bidops/review-tasks/bulk-approve`
- 请求字段：
  - `reviewTaskIds`
  - `remark`
  - `expectedRiskLevel = Low`
  - `maxHighRiskIssueCount = 0`
- 服务端强制校验：
  - 用户有审核权限。
  - 任务仍是待审核/审核中。
  - 没有高风险 issue。
  - 当前风险等级仍是 `Low`。
  - 已入库任务不能重复审批。
- 每个任务审批仍走现有单任务入 Formal 逻辑，保证幂等与审计一致。
- 返回逐项结果：成功、跳过、失败原因。

测试：

- 低风险任务批量通过成功。
- 包含高风险任务时该项失败且不影响其他可通过任务。
- 无权限不能批量通过。
- 重复提交幂等。

## R7 详细验收：批量 DeepSeek 提示词重解析

产出：

- Review list 支持选中多个待审核任务，输入一次 DeepSeek 修正提示词。
- 后端为每个 RawNotice 入队结构化重解析或中标/候选重解析：
  - 采购公告走 raw notice structured reparse。
  - 中标/候选公告走 outcome supplier reparse。
- 只允许未入库、未审核通过任务。
- 批量结果显示任务 ID 和失败原因。

测试：

- 采购公告批量提示词进入 `StructuredParseJobPayload.ReviewerPrompt`。
- 中标公告批量提示词进入 `OutcomeSupplierExtractJobPayload.ReviewerPrompt`。
- 已通过任务不会入队。

## R8 详细验收：人工纠错样本沉淀

产出：

- 新增 `bidops_review_correction_sample` 表：
  - `Id`
  - `TenantId`
  - `ReviewTaskId`
  - `RawNoticeId`
  - `NoticeType`
  - `SourceKind`：`ManualEdit`、`BulkApprove`、`ReparsePrompt`、`IssueResolved`
  - `FieldName`
  - `OriginalValue`
  - `CorrectedValue`
  - `OriginalHeader`
  - `OriginalRowJson`
  - `ReviewerPrompt`
  - `Reason`
  - `CreatedBy`
  - `CreatedAt`
- 人工编辑字段时记录样本。
- DeepSeek 提示词重解析时记录提示词样本。
- 批量通过时记录“低风险确认”样本，用于统计自动质检准确率。
- 不记录 API key、Authorization header、非公开凭据。

测试：

- 编辑金额字段记录纠错样本。
- 编辑资质要求记录纠错样本。
- 提交重解析提示词记录样本。
- 敏感字段不会写入样本。

## R9 详细验收：纠错样本分析与规则改进看板

产出：

- 新增查询 API：
  - 高频错误字段。
  - 高频原始表头。
  - 高频金额单位错误。
  - 高频缺失资质/业绩/人员要求。
  - DeepSeek 提示词常用模式。
- 前端页面展示：
  - Top 表头别名。
  - Top 错误字段。
  - 最近纠错样本。
  - 可复制为规则测试样例的原始行 JSON。
- 暂不自动改规则，只辅助开发者/管理员沉淀规则。

测试：

- 查询接口按字段、公告类型、日期过滤。
- 前端 typecheck 通过。

## R10 详细验收：历史任务质量回填

产出：

- 新增 Worker job：
  - `BidOpsReviewQualityBackfillJob`
- Payload：
  - `maxItems`
  - `noticeType`
  - `riskLevel`
  - `dryRun`
  - `sourceId`
  - `pauseSourceAware`
- 支持来源级暂停和 kill switch。
- dry-run 只返回预计更新数量和样例，不写库。
- 回填时不修改 Formal 数据，只写质量字段和 issue 表。

测试：

- dry-run 不写库。
- maxItems 生效。
- source pause 后不处理对应 RawNotice。
- 已有 issue 可幂等更新，不重复膨胀。

## R11 详细验收：审核效率指标与运行手册

产出：

- Operations 或审核中心新增指标：
  - 今日新增待审。
  - 待审总数。
  - 低/中/高风险数量。
  - 低风险占比。
  - 批量通过数量。
  - 人工平均处理时长。
  - 重解析后质量提升情况。
- Runbook 增加：
  - 如何判断低风险批量确认。
  - 如何处理高风险金额单位异常。
  - 如何使用批量 DeepSeek 提示词。
  - 如何回填历史质量评分。
  - 如何查看纠错样本并转化为解析规则。

测试：

- 指标查询能返回空数据默认值。
- 有样本数据时统计准确。
- Runbook 更新。

## 推荐实施顺序

1. R1 + R2：先让系统能判断“哪些任务更危险”。
2. R4 + R5：让审核员先看异常，马上减少找问题时间。
3. R6：低风险批量确认，直接降低人工点击量。
4. R3 + R7：补齐中标/候选质量评分和批量重解析。
5. R8 + R9：把人工纠错沉淀成规则改进材料。
6. R10 + R11：回填历史数据并形成运营指标。

## MVP 取舍

- MVP 不做全自动入库，只做批量确认候选。
- MVP 不训练模型，只沉淀纠错样本和规则样例。
- MVP 不把质量 issue 做成复杂工作流，只支持生成、展示、标记解决。
- MVP 不要求所有旧数据立即有质量分，通过回填任务逐批处理。

## 执行记录

### 2026-06-22 R1 + R2

完成：

- 新增 `ReviewQualityRiskLevel`、`ReviewRecommendation` 和质量 issue 类型常量。
- 新增 `ReviewTask` 质量摘要字段：`QualityScore`、`RiskLevel`、`QualityIssueCount`、`HighRiskIssueCount`、`ReviewRecommendation`。
- 新增 `ReviewQualityIssue` 实体与租户库迁移 `20260622090000_v0.2.15-bidops-review-quality`。
- 新增 `BidOpsReviewQualityEvaluator` 与 `BidOpsReviewQualityService`，结构化解析/重解析后自动刷新采购公告质量评分和异常项。
- 审核任务列表 DTO 返回质量摘要；审核详情 DTO 返回 `QualityIssues`。
- 前端 BidOps 类型定义已加入质量字段和 `ReviewQualityIssueDto`。

验证：

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过，6 个测试。
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `npm run typecheck` 通过。
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` 无匹配。
- `git diff --check` 通过，仅提示既有 `AtlasTenantDbContextModelSnapshot.cs` 换行规范化。

### 2026-06-22 R4 + R5

完成：

- 审核任务搜索新增 `riskLevel`、`minQualityScore`、`maxQualityScore`、`hasHighRiskIssue`、`reviewRecommendation`、`issueType` 查询条件。
- 审核任务列表默认按质量风险权重排序，让高风险、异常多、质量分低的任务靠前。
- 待审核池页面新增风险等级、推荐动作、异常类型、高风险筛选，并显示质量分、风险等级、异常总数/高风险数和推荐动作。
- 审核详情页在原文/解析分栏之前新增“异常复核”区域，展示质量分、风险等级、异常数量、推荐动作和未解决异常列表。

验证：

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|ReviewTaskSearchQuery|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过，7 个测试。
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `npm run typecheck` 通过。

### 2026-06-22 R3 + R6 + R7 + R8 + R9 + R10 + R11

完成：

- 中标/候选公告质量评分接入 `OutcomeSupplierRecord`：检查厂家名称、分标/包号、候选排名、金额单位、费率误入金额、采购包件匹配缺失或冲突。
- 结果厂家抽取服务保存或未抽取到结果时都会刷新审核任务质量摘要和质量 issue。
- 新增批量审核 API：
  - `POST /api/bidops/review-tasks/bulk-approve`
  - `POST /api/bidops/review-tasks/batch-reparse`
  - `POST /api/bidops/review-tasks/quality-backfill`
- 批量确认只允许低风险、无高风险 issue、待审核/审核中任务；逐项返回成功、失败、跳过原因。
- 批量提示词重解析会按公告类型分流：采购公告走结构化重解析，中标/候选公告走厂家结果重解析。
- 新增 `ReviewCorrectionSample` 实体、EF 配置和迁移 `20260622100000_v0.2.16-bidops-review-automation-completion`。
- 人工编辑结果明细、删除结果明细、批量确认、DeepSeek 提示词重解析都会沉淀纠错样本。
- 新增纠错样本分析 API 和审核效率指标 API：
  - `GET /api/bidops/review-tasks/corrections/analysis`
  - `GET /api/bidops/review-tasks/efficiency-metrics`
- 新增 `ReviewQualityBackfillJobHandler`，支持 `maxItems`、`noticeType`、`riskLevel`、`dryRun`、`sourceId`、`pauseSourceAware`。
- 待审核池新增多选、低风险批量确认、批量提示词重解析、质量回填、质量分析入口。
- 新增审核质量分析页，展示效率指标、高频错误字段、高频原始表头、金额/要求项线索和最近纠错样本。
- 新增运行手册 `docs/BIDOPS/BIDOPS_REVIEW_AUTOMATION_RUNBOOK.md`。

验证：

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|ReviewAutomation|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过，10 个测试。
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过，108 个测试。
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` 通过。
- `npm run typecheck` 通过。
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` 无匹配。
- `git diff --check` 通过，仅提示既有 `AtlasTenantDbContextModelSnapshot.cs` 换行规范化。
