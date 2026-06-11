# Codex 启动 Prompt：基于 Atlas 底座实现 BidOps

你现在在一个已有项目底座 Atlas 中工作。不要创建全新的空项目，也不要重构 Atlas 的基础架构。你的任务是：在理解 Atlas 现有架构、编码规范、模块组织、权限体系、菜单体系、数据库访问方式、前端框架、任务调度方式之后，将 BidOps 招投标作业中台作为一个增量业务模块接入 Atlas。

## 一、必须先做的事

1. 阅读仓库根目录 README、solution/project 文件、package 配置、docker 配置、数据库迁移、前后端目录结构。
2. 识别 Atlas 的技术栈、分层方式、模块规范、依赖注入方式、认证授权方式、菜单/权限注册方式、数据库访问方式、前端路由和页面组织方式。
3. 在 `docs/ATLAS_BIDOPS_FIT_REPORT.md` 中输出适配报告，包括：
   - Atlas 当前架构概览
   - 可直接复用的能力
   - 需要新增的能力
   - 不应放进 Atlas 主 Web 进程的能力
   - BidOps 推荐目录结构
   - 数据库表前缀/Schema 方案
   - 第一阶段实现计划
4. 在 `docs/DECISIONS.md` 中记录所有不确定项的保守默认决策。
5. 除非遇到凭据、安全、生产数据、破坏性操作、付费服务、法律合规风险，否则不要中途询问用户；采用保守默认方案并继续推进。

## 二、核心产品方向

BidOps 不是普通 CRM，也不是标书生成器。它是：

公开标讯自动采集 + 原文快照 + 附件下载 + AI 预解析 + 人工审核 + 包件级商机 + 厂家能力匹配 + 投标作业舱 + 响应矩阵核验 + 结果复盘。

核心闭环：

```text
公开站点自动采集
↓
Raw 原始层保存
↓
AI/规则预解析到 Staging 暂存层
↓
人工审核
↓
正式业务库入库
↓
公告拆成包件
↓
匹配厂家能力
↓
进入投标作业舱
↓
响应矩阵核验
↓
中标/失标/废标/放弃复盘
```

## 三、Atlas 复用原则

优先复用 Atlas 已有能力：

- 登录、用户、角色、组织、权限、菜单
- 审计日志、操作日志、异常处理
- 数据库访问、事务、仓储、迁移
- 前端布局、路由、表格、表单、弹窗、上传组件
- 缓存、配置、日志、文件存储抽象
- 后台任务/定时任务能力，如果 Atlas 已有

不要强行塞入 Atlas 主 Web 进程的能力：

- 爬虫调度
- 网页抓取
- 附件下载
- PDF/Word/Excel 文本抽取
- OCR
- AI 解析队列
- 大文件处理

这些能力应放在独立 Worker 或独立模块中，通过数据库/队列与主 Web 交互。

## 四、目录结构建议

优先使用 Atlas 模块模板和既有模块规范。推荐形态：

```text
src/Atlas.Modules.BidOps
├── ModuleEntry.cs / BidOpsModule.cs
├── Authorization
├── Entities
├── EntityConfigurations
├── Services
├── Queries
├── Controllers
├── BackgroundJobs
├── Crawling
├── Documents
├── Ai
├── Compliance
└── Models
```

不要创建全新的 solution，不要绕过 Atlas 的认证、授权、租户隔离、Repository、Worker、消息、日志和迁移体系。

Web/API 只负责人机交互、审核、查询和入队；爬虫、附件下载、文本抽取、AI 解析、去重、变更监控、中标结果回填等长任务放到 Atlas.Worker 或 Atlas 后台任务机制中执行。

## 五、数据库原则

### 5.1 核心判断

BidOps 的数据应“逻辑上归 BidOps 模块所有”，但 MVP 阶段不要“物理上独立建库”。

```text
代码边界：src/Atlas.Modules.BidOps
实体归属：BidOps 模块
EF 映射归属：BidOps 模块
物理存储：Atlas Tenant DB
迁移执行：Atlas.Data.Tenant.Migrations 统一生成和执行
表名前缀：bidops_
租户隔离：TenantId / Atlas 当前租户实体接口
```

不要创建：

```text
bidops_db
BidOpsDbContext
独立 BidOps migration pipeline
跨租户共享 RawNotice 库
```

### 5.2 Atlas Tenant DB 承载 BidOps 业务表

所有 BidOps 租户业务数据先放 Atlas Tenant DB，包括：

```text
采集来源配置
采集栏目配置
原始公告
公告附件元数据
AI 解析暂存
待审核任务
正式公告
商机包件
厂家能力库
投标作业
响应矩阵
结果复盘
合规检查
```

所有 BidOps 表必须加 `bidops_` 前缀，避免污染系统表。

### 5.3 Raw / Staging / Formal 三层必须分离

```text
Raw：抓到了什么，原始可追溯。
Staging：AI/规则理解成什么，等待人工审核。
Formal：人工确认后的正式业务数据。
```

AI 只能写 Staging，不能直接写 Formal。

### 5.4 模块 EF 配置扫描

Codex 必须检查 Atlas 当前 `AtlasTenantDbContext.OnModelCreating` 是否只扫描宿主程序集。如果当前还不能扫描模块里的 `IEntityTypeConfiguration<T>`，先实现通用的模块 EF 配置扫描机制。

要求：

```text
不要把 BidOps EF 配置硬塞进 Atlas.Data.Tenant。
不要为 BidOps 做一次性硬编码。
不要在 AtlasTenantDbContext 增加大量 DbSet<TEntity>。
应让未来业务模块也能复用相同机制。
```

### 5.5 数据访问边界

BidOps 业务/API 代码不得直接注入 `AtlasTenantDbContext`、`ITenantDbContextFactory` 或 EF `DbContext`，不得直接调用 `DbContext.Set<T>()`、`FromSql*`、`ExecuteSqlRaw`、`IgnoreQueryFilters`。

优先使用：

```text
IRepository<TEntity>
QueryService
领域服务
Atlas 现有事务/审计/数据范围机制
```

唯一索引和幂等键必须按租户隔离，例如：

```text
TenantId + SourceCode + DetailUrlHash
TenantId + RawNoticeId + FileHash
TenantId + TenderPackageId + ActivePursuitStatus
TenantId + BusinessKey
```

### 5.6 核心表

核心表至少包括：

```text
bidops_crawl_source
bidops_crawl_channel
bidops_raw_notice
bidops_raw_attachment
bidops_notice_staging
bidops_package_staging
bidops_requirement_staging
bidops_review_task
bidops_notice
bidops_tender_batch
bidops_tender_lot
bidops_tender_package
bidops_requirement_item
bidops_supplier
bidops_supplier_contact
bidops_supplier_capability
bidops_evidence_document
bidops_opportunity_match
bidops_pursuit
bidops_response_matrix
bidops_pursuit_task
bidops_follow_record
bidops_bid_outcome
bidops_compliance_check
```

### 5.7 文件存储

不要把招标文件、HTML 快照、PDF、Word、Excel、Zip、抽取后的大段文本直接存入 MySQL。数据库只存元数据和 StorageKey。

BidOps 模块应定义 `IBidOpsFileStore`，MVP 可先用 `LocalBidOpsFileStore`，后续再接 MinIO/S3-compatible storage。

详细规则见：`docs/BIDOPS/BIDOPS_ATLAS_DATABASE_INTEGRATION_NOTES.md`。

## 六、合规硬约束

系统只能采集公开页面和公开附件。

禁止实现或引导：

- 登录态绕过
- 验证码破解
- 反爬绕过
- 非公开评标信息采集
- 专家/领导关系信息采集
- 自动投标
- 自动报价操控
- 多厂家同一包件协同投标
- 隐藏资金流向、返点、回扣、好处费相关功能

爬虫必须内置：

- 单域名限速
- 失败退避
- 全局暂停开关
- 来源级暂停开关
- 抓取日志
- User-Agent 标识
- robots/站点规则备注字段
- 异常自动停抓

## 七、MVP 实现顺序

### Phase 0：Atlas 评估

只读分析 + 文档输出，不做大改。

产物：

```text
docs/ATLAS_BIDOPS_FIT_REPORT.md
docs/DECISIONS.md
```

### Phase 0.5：数据归属与模块化 EF 评估

在实现任何实体前，完成并记录：

```text
[ ] Atlas 当前 TargetFramework 和包版本策略
[ ] AtlasModule 是否支持声明 EF EntityConfiguration 程序集
[ ] AtlasTenantDbContext 当前 ApplyConfigurationsFromAssembly 行为
[ ] Tenant 实体必须实现的接口
[ ] Repository / QueryService 使用方式
[ ] MigrationJob 如何生成/执行 Tenant DB 迁移
[ ] 是否需要先实现通用模块 EF 配置扫描扩展点
```

产物：

```text
docs/ATLAS_BIDOPS_FIT_REPORT.md
docs/DECISIONS.md
```

### Phase 1：BidOps 模块骨架

创建 BidOps 后端模块、前端菜单、基础路由、权限注册。

验收：登录 Atlas 后能看到 BidOps 菜单和空页面。

### Phase 2：Raw 采集层

实现采集来源、栏目、任务、原始公告、原始附件的数据模型和基础 API。

验收：能手动创建采集来源/栏目，能写入 RawNotice/RawAttachment 测试数据。

### Phase 3：Crawler Worker 骨架

实现 Worker、任务调度、任务状态机、限速、失败重试、日志。

验收：能运行一个 MockCrawler，将模拟公告写入 Raw 层。

### Phase 4：文本抽取与附件管理

实现附件下载接口、文件存储适配、PDF/Word/Excel 文本抽取占位或基础实现。

验收：上传或模拟附件后，能生成可查看的 TextContent。

### Phase 5：AI/规则预解析到 Staging

实现公告分类、基础字段提取、时间节点提取、包件初步拆分、要求项抽取的数据结构和服务接口。

验收：RawNotice 能生成 NoticeStaging、PackageStaging、RequirementStaging。

### Phase 6：人工审核池

实现待审核列表、审核详情、原文/解析结果对照、通过/忽略/退回重析/合并占位。

验收：审核通过后能进入正式业务表。

### Phase 7：正式业务库与商机包件

实现 Notice、TenderBatch、TenderLot、TenderPackage、RequirementItem 正式表、列表页、详情页。

验收：已审核公告能在商机包件中心查看。

### Phase 8：厂家能力库

实现 Supplier、Capability、EvidenceDocument、联系人/资质/业绩基础能力。

验收：能维护厂家能力，并上传/绑定证明材料。

### Phase 9：包件匹配

实现基于标签/资质/业绩/地区/材料完整度的初版匹配。

验收：包件详情页能显示推荐厂家、匹配原因、缺失项、风险项。

### Phase 10：投标作业舱

实现 Pursuit、PursuitTask、ResponseMatrix、FollowRecord。

验收：同一包件只能有一个 Active Pursuit；能创建任务、跟进、响应矩阵。

### Phase 11：合规检查

实现敏感词提醒、同包件多厂家冲突提醒、操作审计。

验收：出现高风险行为时生成 ComplianceCheck 记录和前端提示。

### Phase 12：结果复盘

实现 BidOutcome、复盘标签、失标原因、废标原因、经验总结。

验收：投标作业可以录入结果并归档。

## 八、Codex 工作规则

1. 不要重写 Atlas 底座。
2. 不要删除现有业务代码。
3. 不要改变现有登录、权限、菜单体系，除非通过扩展方式接入。
4. 不要一次提交巨型不可审查改动；按 Phase 分批提交。
5. 每个 Phase 完成后：
   - 更新文档
   - 运行能运行的 build/test/lint
   - 记录未完成项
   - 继续下一个 Phase
6. 如果 Atlas 缺少某个基础设施，先实现最小抽象，不要引入重型依赖。
7. 所有新表、新接口、新页面优先使用 BidOps 命名空间或目录。
8. 所有外部站点采集适配器先用 Mock/示例适配器打通流程，再接真实公开来源。

## 九、第一轮完成标准

当以下能力可用时，认为第一轮完成：

1. Atlas 中出现 BidOps 菜单。
2. 能维护采集来源和栏目。
3. Worker 能生成模拟 RawNotice。
4. RawNotice 能进入 AI/规则预解析流程。
5. 能生成 NoticeStaging、PackageStaging、RequirementStaging。
6. 审核池能查看解析结果。
7. 审核通过后生成正式 Notice 和 TenderPackage。
8. 能维护厂家能力。
9. 包件能推荐厂家并显示匹配原因。
10. 能创建一个投标作业舱。
11. 响应矩阵能展示要求项和材料缺失状态。
12. 同一包件重复创建 Active Pursuit 会被阻止。
13. README/docs 说明如何运行和验证。

开始执行。先完成 Phase 0，然后按 Phase 1 到 Phase 12 继续推进。除非遇到安全、凭据、生产数据、法律合规或破坏性操作问题，不要中途询问用户。
