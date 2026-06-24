# BidOps 采购明细与中标闭环升级任务清单

> 目标：把现有“公告解析 + 中标线索”升级为可审核、可追溯、可分析的“采购明细事实 + 中标结果事实 + 包件生命周期闭环”。

## 状态约定

- `Pending`：未开始。
- `In Progress`：正在实现。
- `Done`：代码、测试、文档更新已完成。
- `Blocked`：存在明确阻塞，需要记录原因。

## 总体原则

- Raw / Staging / Formal 三层继续分离，AI 和规则解析只能写 Staging 或线索表。
- WebApi 只做查询、审核、入队；附件解析、AI 解析、闭环匹配和回填走 Worker/后台任务。
- 金额统一存人民币元；只有单元格、表头或紧邻上下文明确出现 `万元/万` 时才乘以 10000，没有明确单位时按元。
- 原始附件字段不强行全部变成固定列；标准字段用于分析，原始行 JSON 用于追溯和后续字段映射。
- 所有新表使用 `bidops_` 前缀，并按租户隔离。

## 任务列表

| 编号 | 状态 | 任务 | 可测试产出 |
|---|---|---|---|
| T0 | Done | 建立升级任务清单 | 本文档存在，后续任务可逐项更新状态 |
| T1 | Done | 增加采购明细与闭环关联核心数据模型 | 新实体、EF 配置、租户迁移；模型配置测试通过 |
| T2 | Done | 增加采购明细 DTO 与查询读模型 | Review/Package API 可返回采购明细；查询服务映射测试通过 |
| T3 | In Progress | 升级采购附件表格字段映射 | 三类采购一览表样例可解析为采购明细 Staging；金额单位测试通过 |
| T4 | In Progress | 审核页展示和编辑采购明细 | 前端类型检查通过；审核页能查看标准字段和原始行 JSON |
| T5 | Pending | 审核通过时导入正式采购明细 | Staging 明细随审核进入 Formal；重复审批幂等测试通过 |
| T6 | Pending | 中标/候选结果与采购明细自动闭环匹配 | 生成生命周期关联建议；强/弱匹配测试通过 |
| T7 | Pending | 闭环人工确认与解除关联 | 审核人员可确认/解除关联；权限和状态测试通过 |
| T8 | Pending | 公司中标与包件服务分析视图 | 能查询“哪个公司中了什么标、多少钱、服务内容、资质要求” |
| T9 | Pending | 历史数据回填与运行手册 | Worker 回填任务可控、可暂停；Runbook 更新 |

## T1 详细验收

产出：

- `ProcurementDetailStaging`：采购明细暂存行，关联 `NoticeStaging`、可选 `PackageStaging`、Raw notice、附件和原始表格位置。
- `ProcurementDetail`：正式采购明细行，关联 `Notice`、可选 `TenderPackage`，承载审核后的标准字段和原始行 JSON。
- `LifecyclePackageLink`：采购明细、包件、候选/中标结果之间的闭环关联或建议。
- EF 配置包含表名、长度、金额精度、租户索引和幂等索引。
- Tenant migration 创建三张表。

测试：

- `ProcurementDetailConfiguration_MapsCoreIndexesAndJsonColumns`
- `LifecyclePackageLinkConfiguration_UsesTenantScopedMatchIndex`
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore`

完成情况：

- Done. 已新增 `ProcurementDetailStaging`、`ProcurementDetail`、`LifecyclePackageLink`。
- Done. 已新增 tenant migration `20260618093000_v0.2.14-bidops-procurement-details`。
- Done. 已新增本地补表命令 `ensure-bidops-procurement-details`，用于修复早于该迁移的本地运行库。
- Done. 模型配置测试和 BidOps/tenant migration 构建通过。

## T2 详细验收

产出：

- 新增 `ProcurementDetailStagingDto`、`ProcurementDetailDto`、`LifecyclePackageLinkDto`。
- Review detail 能返回当前公告的采购明细暂存行。
- Package detail 能返回正式采购明细和关联结果概览。

测试：

- QueryService 映射测试覆盖采购明细标准字段、金额字段、原始行 JSON 是否返回。

完成情况：

- Done. 已新增采购明细暂存/正式 DTO 和闭环关联 DTO。
- Done. `ReviewTaskDetailDto` 返回 `ProcurementDetails`。
- Done. `TenderPackageDto` 在包件详情中返回正式 `ProcurementDetails`。
- Done. `BidOpsQueryService_MapsProcurementDetailDtosWithRawJsonAndAmounts` 通过。

## T3 详细验收

产出：

- 表头别名字典覆盖：采购编号、分标编号、分标名称、包号、包名称、项目名称、采购内容、工程概况、资质、业绩、人员、金额、工期、地点、权重、报价方式等。
- 支持用户提供的三类采购一览表样式。
- 原始行 JSON 保留所有原始列。

测试：

- 三个样例表各自至少解析出包号、包名称/项目名称、采购内容、金额、资质/业绩/人员要求。
- 无单位金额按元，明确 `万元` 才乘以 10000，百分比不进金额字段。

完成情况：

- In Progress. 已先补强现有包件级采购金额解析：DeepSeek 提示要求金额返回元，后端会对 `万元` 表头的未乘数值进行确定性纠偏。
- In Progress. 确定性表头别名已覆盖 `采购金额（元）`、`包估算金额（万元）`、`行报价最高限价（含税/万元）`、`最高应答限价含税（元或折扣比例)` 等变体，并跳过百分比/税率/权重等非金额列。

## T4 详细验收

产出：

- 审核详情页新增“采购明细”区域。
- 标准字段用表格展示，原始行 JSON 可展开查看。
- 支持审核人员编辑核心字段和金额单位纠错。
- 采购公告审核页支持填写 DeepSeek 调整提示词并触发结构化重解析。

测试：

- `npm run typecheck`
- 页面无空字段大面积挤占，移动/窄屏不出现明显文本重叠。

完成情况：

- In Progress. 采购公告审核页已新增 DeepSeek 解析调整输入框，提示词会进入 raw notice 结构化重解析 job。

## T5 详细验收

产出：

- 审核通过时将 `ProcurementDetailStaging` 导入 `ProcurementDetail`。
- 若采购明细可定位到 `PackageStaging`，同步关联正式 `TenderPackage`。
- 重复审批或重试不重复创建明细。

测试：

- 审核通过创建正式采购明细。
- 已存在同源明细时幂等更新或跳过。

## T6 详细验收

产出：

- 将现有反向闭环 Debug 能力升级为正式匹配服务。
- 按 `采购编号 + 分标编号 + 包号`、`采购编号 + 包号 + 名称`、文本相似度生成候选关联。
- 输出匹配分、匹配原因、缺失字段、是否需要人工复核。

测试：

- 强匹配自动高置信关联。
- 包号重复但分标不同不误关联。
- 缺少采购编号时进入人工复核。

## T7 详细验收

产出：

- Review UI 或闭环工作台能确认/解除 `LifecyclePackageLink`。
- 记录确认人、确认时间、备注。

测试：

- 无权限不能确认。
- 已确认关联可解除并保留审计。

## T8 详细验收

产出：

- 公司维度：中了哪些标、金额、采购人、服务内容、资质要求。
- 包件维度：采购公告、候选人公示、中标公告完整链路。
- 买方维度：历史采购服务类型、常见资质要求、中标供应商。

测试：

- API 查询能按供应商、采购人、金额区间、服务类型过滤。

## T9 详细验收

产出：

- Worker 回填任务支持来源级暂停、批量大小、重试限制。
- Runbook 记录本地和生产前置检查。

测试：

- dry-run 不写库。
- pause source 后不继续回填。
