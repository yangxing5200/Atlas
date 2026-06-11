# BidOps 招投标自动采集与人工审核系统 — Codex 执行说明书 V1.0

> 目标读者：Codex / AI Coding Agent / 人类开发者  
> 目标：在尽量不打扰产品负责人的前提下，完成一个可运行的 MVP。  
> 核心定位：公开标讯自动采集 + AI 预解析 + 人工审核 + 包件级商机处理 + 厂家能力匹配 + 投标作业舱。

---

## 0. 给 Codex 的总指令

你是本项目的主要实现者。请先完整阅读本文件、仓库根目录的 `AGENTS.md`，然后按阶段顺序执行。除非遇到安全、合规、凭据、破坏性操作或必须购买服务的问题，否则不要中途询问用户。

### 0.1 不确定时的默认处理

当你遇到需求不明确、当前仓库缺少上下文、依赖版本不可用、外网不可访问、目标网站结构变化等问题时：

1. 采用本文中的保守默认方案。
2. 在 `docs/DECISIONS.md` 记录你的假设、选择原因、后续可调整点。
3. 继续完成可实现部分。
4. 不要因为非阻塞问题停下来问用户。

### 0.2 允许中断并询问的情况

只有以下情况允许停止并询问：

- 需要真实账号、验证码、短信、人脸验证、企业证书或登录态。
- 需要绕过网站访问限制、破解验证码、绕过反爬、模拟非法登录或高频访问。
- 需要生产环境密钥、数据库密码、OpenAI Key、DeepSeek Key、MinIO 密钥等敏感信息。
- 需要删除用户已有数据、重写既有业务代码、执行不可逆迁移。
- 某项实现存在明显违法、违规或可能帮助串标、行贿、受贿、规避审计的风险。

### 0.3 交付原则

每个阶段完成后：

- 更新 `docs/IMPLEMENTATION_LOG.md`。
- 更新或新增测试。
- 运行能运行的构建与测试命令。
- 如果有跳过测试，记录原因。
- 保证系统可以本地启动或至少服务端可构建。



---

## 0A. Atlas 集成覆盖规则（当前项目优先遵守）

如果本说明书在 Atlas 仓库中执行，以下规则覆盖后文“新仓库”默认方案：

1. 不要创建新的 solution，也不要重构 Atlas 底座。
2. 不要升级 Atlas 的目标框架；保持 Atlas 当前 .NET 8 / 包版本策略。
3. BidOps 作为 Atlas 业务模块接入，推荐目录为 `src/Atlas.Modules.BidOps`。
4. BidOps 的业务表、实体、EF 映射、服务、查询、后台任务归 BidOps 模块所有。
5. MVP 阶段不要为 BidOps 创建独立物理数据库、独立 `BidOpsDbContext` 或独立 migration pipeline。
6. 所有 BidOps 业务表放入 Atlas Tenant DB，表名统一使用 `bidops_` 前缀，租户业务实体必须按 Atlas 当前规则实现租户隔离。
7. 迁移由 Atlas Tenant migration 机制统一生成和执行。
8. Codex 必须先检查 Atlas 是否支持扫描模块程序集中的 `IEntityTypeConfiguration<T>`。如不支持，先实现通用模块 EF 配置扫描机制，不要把 BidOps 配置硬塞进宿主项目。
9. BidOps Web/API 代码不得直接注入 `AtlasTenantDbContext`、`ITenantDbContextFactory` 或 EF `DbContext`，不得直接调用 `DbContext.Set<T>()`；使用 Repository、QueryService 或领域服务。
10. 文件二进制、HTML 快照、PDF/Word/Excel/Zip、大段文本不写入 MySQL，只写元数据和 StorageKey，通过 `IBidOpsFileStore` 保存到本地文件或对象存储。
11. 公开标讯采集、附件下载、文本抽取、AI 解析、去重、变更监控等长任务必须跑在 Atlas.Worker / 后台任务机制中，不跑在 WebAPI 请求线程中。
12. 详细数据库归属规则见 `docs/BIDOPS/BIDOPS_ATLAS_DATABASE_INTEGRATION_NOTES.md`。

---

## 1. 产品重新定义

本产品不是普通 CRM，不是单纯招标信息网站，也不是自动写标书工具。

本产品是：

> 标讯情报 + 厂家能力库 + 投标作业协同 + 响应合规核验系统。

核心业务闭环：

```text
公开标讯自动采集
↓
原文快照与附件保存
↓
AI 预解析
↓
去重与变更识别
↓
人工审核
↓
公告/批次/分标/包件入库
↓
厂家能力匹配
↓
投标作业舱
↓
响应矩阵核验
↓
结果复盘
```

系统的最小业务单元不是“项目”，而是：

> 商机包件 / Tender Package / Opportunity Package

原因：一个招标公告可能包含多个批次、分标、标段、包件。后续匹配厂家、资格核验、投标作业、结果复盘都应围绕包件展开。

---

## 2. MVP 范围

### 2.1 MVP 必做

1. 采集来源配置
2. 采集任务调度
3. 列表扫描、详情抓取、附件下载
4. 原始公告、原文快照、附件存储
5. 文本抽取
6. AI 或规则预解析
7. 去重与变更识别
8. 待审核池
9. 审核通过后进入正式业务库
10. 公告、批次、分标、包件建模
11. 厂家能力库
12. 包件与厂家匹配
13. 投标作业舱
14. 响应矩阵
15. 结果复盘
16. 基础合规风控

### 2.2 MVP 暂不做

- 自动生成整本标书
- 自动报价
- 自动投标
- 自动联系厂家
- 登录态抓取
- 验证码破解
- 绕过反爬机制
- 高并发采集
- 非公开信息采集
- 专家、领导、内部关系采集
- 返点、好处费、灰色资金流相关功能
- 多厂家同一包件协同投标
- 招标方/评标专家非公开信息分析

---

## 3. 合规与安全边界

爬虫必须是温和、可审计、可暂停的公开信息采集工具。

### 3.1 允许采集

- 公开网页上的招标公告、采购公告、变更公告、澄清公告、中标候选人公示、中标结果公告。
- 公开网页上的附件，例如 PDF、Word、Excel、ZIP。
- 不需要登录、不需要验证码、不需要绕过访问控制即可访问的信息。

### 3.2 禁止采集或实现

- 需要登录、验证码、短信、人脸、企业证书、客户端证书才能访问的数据。
- 非公开评标信息、专家信息、招标人内部意见。
- 个人隐私信息，除公告中公开的业务联系人外不主动采集。
- 通过绕过反爬、破解验证码、伪造登录态、攻击接口等方式获取内容。
- 高频压测式访问目标网站。

### 3.3 技术保护措施

系统必须实现：

- 每个来源单独限速。
- 全局抓取开关。
- 单来源抓取开关。
- 失败退避。
- 最大重试次数。
- 抓取日志。
- 异常熔断。
- User-Agent 标识。
- 仅保存必要字段。
- 对业务联系人等信息支持脱敏展示。

### 3.4 投标合规风控

系统必须避免成为串标工具。

必须提示或阻断：

- 同一包件选择多个厂家进入正式投标作业。
- 同一人员为同一包件下多个竞争厂家编制投标文件。
- 同一包件下不同厂家投标文件高度相似。
- 同一包件下不同厂家使用同一保证金账户、同一联系人、同一项目成员。
- 跟进记录中出现“返点、好处费、领导、搞定、关系、保证中标”等敏感词。

MVP 阶段先实现基础版：

- 同一包件只允许一个厂家进入 `Pursuit`。
- 跟进记录敏感词提示。
- 操作日志留存。
- 高风险功能不实现。

---

## 4. 默认技术栈

如果是新仓库，默认使用：

### 4.1 后端

- Atlas 仓库中：保持 Atlas 当前 TargetFramework，例如 .NET 8，不要在本任务中升级。
- 新仓库中：可按环境选择当前可用的 LTS 版本，并在 `docs/DECISIONS.md` 记录。
- ASP.NET Core Web API。
- EF Core。
- MySQL 8。
- Redis 可选，MVP 可先不用。
- Quartz.NET 或自研数据库轮询任务。MVP 推荐先用数据库任务表 + Worker 轮询，降低依赖复杂度。
- MinIO 作为对象存储。
- Serilog 记录日志。

### 4.2 前端

- Vue 3
- Vite
- TypeScript
- Element Plus
- Pinia
- Vue Router
- Axios

### 4.3 文档与文件处理

- HTML 解析：AngleSharp 或 HtmlAgilityPack。
- PDF 文本抽取：UglyToad.PdfPig 优先。
- Word/Excel：LibreOffice 命令行转换或 OpenXML SDK。
- ZIP：系统库解压，递归处理内部文件。
- OCR：非默认路径，仅当 PDF 无文本层时保留接口或可选实现。

### 4.4 AI

- 定义 `IAiExtractionService` 抽象。
- Provider 可支持 OpenAI / DeepSeek / Mock。
- MVP 必须提供 Mock Provider，保证没有 API Key 也能跑通。
- AI 输出必须进入暂存层，不能直接覆盖正式业务数据。

### 4.5 本地开发

提供：

- `docker-compose.yml`
- MySQL
- MinIO
- 可选 Redis
- 后端启动命令
- 前端启动命令
- 初始化数据脚本

---

## 5. 推荐仓库结构

如果仓库为空，创建：

```text
bidops/
├── AGENTS.md
├── README.md
├── docker-compose.yml
├── docs/
│   ├── DECISIONS.md
│   ├── IMPLEMENTATION_LOG.md
│   ├── API.md
│   ├── DATABASE.md
│   ├── COMPLIANCE.md
│   └── SAMPLE_PROMPTS.md
├── src/
│   ├── BidOps.Api/
│   ├── BidOps.Worker/
│   ├── BidOps.Domain/
│   ├── BidOps.Application/
│   ├── BidOps.Infrastructure/
│   └── BidOps.Web/
├── tests/
│   ├── BidOps.Domain.Tests/
│   ├── BidOps.Application.Tests/
│   └── BidOps.Infrastructure.Tests/
└── samples/
    ├── notices/
    ├── html/
    └── attachments/
```

如果仓库已有结构，则尽量适配现有结构，不要强行重构。

---

## 6. 分层架构

```text
Web UI
  ↓
API Layer
  ↓
Application Services
  ↓
Domain Model
  ↓
Infrastructure
  ├── MySQL
  ├── MinIO
  ├── AI Provider
  ├── Crawler Adapters
  └── Text Extractors

Worker Service
  ├── Crawl Job Poller
  ├── List Scanner
  ├── Detail Fetcher
  ├── Attachment Downloader
  ├── Text Extractor
  ├── AI Parser
  ├── Dedup Detector
  └── Review Task Generator
```

### 6.1 原始采集层与正式业务层分离

必须分三层：

1. `Raw`：系统抓到了什么。
2. `Staging`：系统/AI 理解成什么。
3. `Business`：人工审核确认后的正式业务数据。

禁止爬虫或 AI 直接写正式业务表。

---

## 7. 核心数据模型

### 7.1 采集配置表

#### CrawlSource

```text
Id
Code
Name
SourceType
BaseUrl
Enabled
Priority
RateLimitPerMinute
CrawlIntervalMinutes
NeedJsRender
NeedLogin
RespectRobots
UserAgent
Remark
CreatedAt
UpdatedAt
```

#### CrawlChannel

```text
Id
SourceId
Code
Name
NoticeType
ListUrl
Region
Industry
Enabled
LastScanTime
LastSuccessTime
LastError
CreatedAt
UpdatedAt
```

#### CrawlJob

```text
Id
SourceId
ChannelId
JobType
Status
Priority
PayloadJson
ScheduledAt
StartedAt
FinishedAt
RetryCount
MaxRetryCount
ErrorMessage
CreatedAt
UpdatedAt
```

JobType：

```text
ListScan
DetailFetch
AttachmentDownload
TextExtract
AiParse
DedupCheck
ChangeDetect
ReviewGenerate
```

Status：

```text
Pending
Running
Succeeded
Failed
Canceled
Skipped
```

---

### 7.2 原始采集层

#### RawNotice

```text
Id
SourceId
ChannelId
SourceNoticeId
Title
DetailUrl
NoticeType
PublishTime
FetchTime
HtmlContent
TextContent
ContentHash
SnapshotPath
Status
CreatedAt
UpdatedAt
```

#### RawAttachment

```text
Id
RawNoticeId
FileName
FileUrl
FileType
FileSize
FileHash
StoragePath
DownloadStatus
TextExtractStatus
TextContentPath
CreatedAt
UpdatedAt
```

#### RawNoticeVersion

```text
Id
RawNoticeId
VersionNo
Title
TextContent
ContentHash
SnapshotPath
ChangeSummary
CreatedAt
```

---

### 7.3 暂存解析层

#### NoticeStaging

```text
Id
RawNoticeId
NoticeType
ProjectName
ProjectCode
BuyerName
AgencyName
Region
BudgetAmount
PublishTime
SignupDeadline
BidDeadline
OpenBidTime
AiConfidence
ReviewStatus
ReviewerId
ReviewedAt
CreatedAt
UpdatedAt
```

#### PackageStaging

```text
Id
NoticeStagingId
LotNo
LotName
PackageNo
PackageName
Category
Quantity
Unit
BudgetAmount
MaxPrice
DeliveryPlace
DeliveryPeriod
AiConfidence
ReviewStatus
CreatedAt
UpdatedAt
```

#### RequirementStaging

```text
Id
PackageStagingId
RequirementType
OriginalText
SourceFileId
SourcePage
IsMandatory
IsRejectRisk
RequiredEvidenceType
RiskLevel
AiExplanation
AiConfidence
ReviewStatus
CreatedAt
UpdatedAt
```

#### ReviewTask

```text
Id
BizType
BizId
TaskTitle
Priority
Status
AssignedTo
Decision
Remark
CreatedAt
ReviewedAt
```

Status：

```text
Pending
InReview
Approved
Ignored
Merged
ReparseRequired
```

---

### 7.4 正式业务层

#### Notice

```text
Id
RawNoticeId
Title
NoticeType
ProjectName
ProjectCode
BuyerName
AgencyName
Region
BudgetAmount
PublishTime
SignupDeadline
BidDeadline
OpenBidTime
Status
CreatedAt
UpdatedAt
```

#### TenderBatch

```text
Id
NoticeId
BatchNo
BatchName
Remark
```

#### TenderLot

```text
Id
NoticeId
BatchId
LotNo
LotName
Category
Remark
```

#### TenderPackage

```text
Id
NoticeId
BatchId
LotId
PackageNo
PackageName
Category
Quantity
Unit
BudgetAmount
MaxPrice
DeliveryPlace
DeliveryPeriod
Status
CreatedAt
UpdatedAt
```

#### RequirementItem

```text
Id
PackageId
RequirementType
OriginalText
SourceFileId
SourcePage
IsMandatory
IsRejectRisk
RequiredEvidenceType
RiskLevel
AiExplanation
ManualRemark
CreatedAt
UpdatedAt
```

#### Supplier

```text
Id
Name
UnifiedSocialCreditCode
RegisteredAddress
BusinessScope
MainProducts
CoverageRegions
Website
Remark
Status
CreatedAt
UpdatedAt
```

#### SupplierContact

```text
Id
SupplierId
Name
Position
Mobile
Wechat
Email
Remark
CreatedAt
UpdatedAt
```

#### SupplierCapability

```text
Id
SupplierId
CapabilityType
CapabilityName
Category
Region
Level
ValidFrom
ValidTo
Remark
CreatedAt
UpdatedAt
```

#### EvidenceDocument

```text
Id
SupplierId
DocumentType
DocumentName
FileId
ValidFrom
ValidTo
Status
ExtractedFieldsJson
Remark
CreatedAt
UpdatedAt
```

#### OpportunityMatch

```text
Id
PackageId
SupplierId
Score
MatchedItemsJson
MissingItemsJson
RiskItemsJson
Recommendation
Status
CreatedAt
UpdatedAt
```

#### Pursuit

```text
Id
PackageId
SupplierId
Status
OwnerUserId
StartedAt
SubmittedAt
OutcomeId
ComplianceStatus
CreatedAt
UpdatedAt
```

#### ResponseMatrixItem

```text
Id
PursuitId
RequirementItemId
EvidenceDocumentId
OwnerUserId
Status
RiskLevel
ManualResponse
AiSuggestion
CreatedAt
UpdatedAt
```

#### PursuitTask

```text
Id
PursuitId
Title
TaskType
OwnerUserId
DueTime
Status
Remark
CreatedAt
UpdatedAt
```

#### FollowRecord

```text
Id
PursuitId
PackageId
SupplierId
ContactId
FollowType
Content
SensitiveFlag
SensitiveWords
NextAction
FollowTime
CreatedBy
CreatedAt
```

#### BidOutcome

```text
Id
PackageId
PursuitId
OutcomeType
WinningSupplierName
WinningAmount
Reason
ReviewSummary
CreatedAt
UpdatedAt
```

#### ComplianceCheck

```text
Id
BizType
BizId
CheckType
RiskLevel
Message
Status
HandledBy
HandledAt
CreatedAt
```

---

## 8. 核心状态机

### 8.1 商机包件状态

```text
New
Parsed
PackageSplit
PendingEvaluation
MatchedSupplier
SupplierConfirming
PursuitStarted
Submitted
Opening
Won
Lost
Rejected
Abandoned
Reviewed
Archived
```

### 8.2 厂家匹配状态

```text
SystemRecommended
ManuallyConfirmed
Contacted
Interested
MaterialMissing
NotQualified
ConfirmedToParticipate
GaveUp
```

### 8.3 响应项状态

```text
New
AiDetected
ManuallyConfirmed
EvidenceBound
Completed
MaterialMissing
NeedSupplement
Deviation
HighRisk
NotSatisfied
NotApplicable
```

---

## 9. 爬虫适配器设计

### 9.1 接口

```csharp
public interface ITenderCrawler
{
    string SourceCode { get; }

    Task<IReadOnlyList<NoticeListItem>> ScanListAsync(
        CrawlChannel channel,
        CrawlCursor cursor,
        CancellationToken cancellationToken);

    Task<NoticeDetail> FetchDetailAsync(
        NoticeListItem item,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttachmentItem>> ExtractAttachmentsAsync(
        NoticeDetail detail,
        CancellationToken cancellationToken);
}
```

### 9.2 MVP 必须实现的适配器

#### ManualUrlCrawler

功能：用户输入 URL，系统抓详情、附件、解析。用于兜底。

#### ConfigurableHtmlCrawler

功能：通过数据库配置 CSS Selector 实现简单公开 HTML 页面采集。

字段：

```text
ListItemSelector
TitleSelector
UrlSelector
PublishTimeSelector
NextPageSelector
DetailContentSelector
AttachmentSelector
```

#### SampleFixtureCrawler

功能：读取 `samples/html` 下的样例 HTML，用于无外网环境下测试全流程。

### 9.3 暂缓实现

- 不要在 MVP 中硬编码复杂站点的反爬逻辑。
- 如果要实现国家电网/政府采购/公共资源平台适配器，必须只使用公开页面，不登录，不绕验证码，不高频访问。
- 如果站点结构无法稳定解析，则创建 Adapter Stub 和配置说明，不要阻塞其他功能。

---

## 10. 采集流水线

### 10.1 ListScan

输入：`CrawlChannel`  
输出：`NoticeListItem` 列表  
行为：发现新详情页 URL，创建 `DetailFetch` job。

### 10.2 DetailFetch

输入：详情页 URL  
输出：`RawNotice`  
行为：保存 HTML、纯文本、快照、内容 Hash，创建附件下载 job。

### 10.3 AttachmentDownload

输入：附件 URL  
输出：`RawAttachment`  
行为：下载到 MinIO，计算 Hash，创建文本抽取 job。

### 10.4 TextExtract

输入：RawNotice / RawAttachment  
输出：文本内容  
行为：PDF/Word/Excel/ZIP 文本抽取，无法抽取时标记失败但不阻塞公告入库。

### 10.5 AiParse

输入：公告文本 + 附件文本  
输出：NoticeStaging / PackageStaging / RequirementStaging  
行为：结构化提取并保留置信度。

### 10.6 DedupCheck

去重依据：

```text
DetailUrl
ProjectCode
Title + BuyerName + PublishTime
ContentHash
AttachmentHash
```

结果：

```text
Duplicate
PossibleDuplicate
NewVersion
NewNotice
```

### 10.7 ChangeDetect

识别：

```text
更正公告
澄清公告
延期公告
补遗公告
答疑公告
中标候选人公示
中标结果公告
```

### 10.8 ReviewGenerate

生成审核任务。

---

## 11. AI 解析设计

### 11.1 分阶段解析

不要一次性让模型输出所有内容。按阶段：

1. 公告分类
2. 基础字段提取
3. 包件拆分
4. 要求项抽取
5. 响应矩阵初稿
6. 风险识别

### 11.2 输出约束

- AI 必须输出 JSON。
- JSON 必须通过 Schema 校验。
- 每个字段带 `confidence`。
- 重要字段带 `sourceText` 或 `sourcePage`。
- AI 结果只进 Staging，不直接进正式业务库。

### 11.3 NoticeExtract JSON 示例

```json
{
  "noticeType": "TenderAnnouncement",
  "projectName": "",
  "projectCode": "",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.0,
  "sourceEvidence": []
}
```

### 11.4 PackageExtract JSON 示例

```json
{
  "packages": [
    {
      "lotNo": "",
      "lotName": "",
      "packageNo": "",
      "packageName": "",
      "category": "",
      "quantity": null,
      "unit": "",
      "budgetAmount": null,
      "maxPrice": null,
      "deliveryPlace": "",
      "deliveryPeriod": "",
      "confidence": 0.0,
      "sourceEvidence": []
    }
  ]
}
```

### 11.5 RequirementExtract JSON 示例

```json
{
  "requirements": [
    {
      "requirementType": "Qualification",
      "originalText": "",
      "sourcePage": null,
      "isMandatory": false,
      "isRejectRisk": false,
      "requiredEvidenceType": "",
      "riskLevel": "Medium",
      "aiExplanation": "",
      "confidence": 0.0
    }
  ]
}
```

---

## 12. API 设计

### 12.1 采集来源

```text
GET    /api/crawl-sources
POST   /api/crawl-sources
GET    /api/crawl-sources/{id}
PUT    /api/crawl-sources/{id}
POST   /api/crawl-sources/{id}/enable
POST   /api/crawl-sources/{id}/disable
```

### 12.2 采集栏目

```text
GET    /api/crawl-channels
POST   /api/crawl-channels
PUT    /api/crawl-channels/{id}
POST   /api/crawl-channels/{id}/scan-now
```

### 12.3 采集任务

```text
GET    /api/crawl-jobs
GET    /api/crawl-jobs/{id}
POST   /api/crawl-jobs/{id}/retry
POST   /api/crawl-jobs/{id}/cancel
```

### 12.4 原始公告

```text
GET    /api/raw-notices
GET    /api/raw-notices/{id}
GET    /api/raw-notices/{id}/attachments
GET    /api/raw-notices/{id}/versions
POST   /api/raw-notices/import-url
```

### 12.5 审核池

```text
GET    /api/review-tasks
GET    /api/review-tasks/{id}
POST   /api/review-tasks/{id}/approve
POST   /api/review-tasks/{id}/ignore
POST   /api/review-tasks/{id}/merge
POST   /api/review-tasks/{id}/reparse
```

### 12.6 正式公告与包件

```text
GET    /api/notices
GET    /api/notices/{id}
GET    /api/notices/{id}/packages
GET    /api/packages
GET    /api/packages/{id}
GET    /api/packages/{id}/requirements
```

### 12.7 厂家能力库

```text
GET    /api/suppliers
POST   /api/suppliers
GET    /api/suppliers/{id}
PUT    /api/suppliers/{id}
GET    /api/suppliers/{id}/contacts
POST   /api/suppliers/{id}/contacts
GET    /api/suppliers/{id}/capabilities
POST   /api/suppliers/{id}/capabilities
GET    /api/suppliers/{id}/evidence-documents
POST   /api/suppliers/{id}/evidence-documents
```

### 12.8 匹配与投标作业

```text
POST   /api/packages/{id}/match-suppliers
GET    /api/packages/{id}/matches
POST   /api/packages/{id}/pursuits
GET    /api/pursuits
GET    /api/pursuits/{id}
GET    /api/pursuits/{id}/response-matrix
POST   /api/pursuits/{id}/tasks
POST   /api/pursuits/{id}/follow-records
POST   /api/pursuits/{id}/submit-check
POST   /api/pursuits/{id}/outcome
```

---

## 13. 前端页面

### 13.1 今日作业台

展示：

```text
今日新增标讯
待审核公告
高匹配商机
即将截止事项
关注项目变更
解析失败任务
中标结果待回填
高风险响应项
```

### 13.2 标讯采集

页面：

```text
采集来源管理
采集栏目管理
采集任务列表
原始公告列表
原始公告详情
手动 URL 导入
```

### 13.3 待审核池

页面：

```text
审核任务列表
审核详情
左侧原文/附件预览
右侧 AI 解析字段
审核通过/忽略/合并/重析
```

### 13.4 商机包件中心

页面：

```text
公告列表
公告详情
包件列表
包件详情
要求项列表
时间节点
```

### 13.5 厂家能力库

页面：

```text
厂家列表
厂家详情
联系人
能力标签
资质文件
业绩文件
材料有效期
```

### 13.6 匹配决策台

页面：

```text
包件选择
推荐厂家
匹配分数
满足项
缺失项
风险项
人工确认
进入投标作业
```

### 13.7 投标作业舱

页面：

```text
作业概览
时间轴
任务清单
响应矩阵
文件清单
跟进记录
风险清单
提交检查
结果录入
```

---

## 14. 匹配算法 MVP

先用规则评分，不要一开始做复杂模型。

### 14.1 评分项

```text
品类匹配：40 分
资质匹配：20 分
业绩匹配：15 分
地区匹配：10 分
材料完整度：10 分
历史参与：5 分
```

### 14.2 输出

```text
Score
MatchedItems
MissingItems
RiskItems
Recommendation
```

Recommendation：

```text
Recommended
NeedReview
NotRecommended
```

### 14.3 缺失材料识别

规则：RequirementItem.requiredEvidenceType 与 Supplier.EvidenceDocument.DocumentType 匹配；若不存在或过期，则进入 MissingItems。

---

## 15. 合规检查 MVP

### 15.1 同包件冲突

创建 `Pursuit` 时检查：

```text
同一个 PackageId 下是否已有 Active Pursuit
```

如果已有，阻止创建，并生成 ComplianceCheck。

### 15.2 敏感词检测

保存 FollowRecord 时检查：

```text
返点
返几个点
好处费
领导
搞定
保证中标
关系
评委
专家名单
内部消息
```

检测到后：

- `SensitiveFlag = true`
- 保存命中的词
- 前端提示“该内容可能涉及合规风险，请确认记录方式是否恰当”
- 不要自动删除用户内容，但要留审计记录。

---

## 16. 开发任务拆分

每个任务应控制在 0.5～2 人天。Codex 执行时按顺序推进。

### Phase 0：项目骨架

| 任务 | 交付物 | 验收 |
|---|---|---|
| P0-01 创建解决方案结构 | src/tests/docs/samples | `dotnet build` 可运行或记录原因 |
| P0-02 创建 docker-compose | MySQL/MinIO/可选Redis | `docker compose config` 通过 |
| P0-03 创建基础 README | 启动说明 | 新人可按说明启动 |
| P0-04 创建 DECISIONS/LOG 文档 | docs 文件 | 后续决策可记录 |
| P0-05 配置日志 | Serilog | API/Worker 均有结构化日志 |

### Phase 0.5：Atlas 数据归属与模块化 EF 检查

| 任务 | 交付物 | 验收 |
|---|---|---|
| P0.5-01 检查 Atlas 模块系统 | `docs/ATLAS_BIDOPS_FIT_REPORT.md` | 说明模块注册、权限、菜单、Worker 接入方式 |
| P0.5-02 检查 Tenant 实体规范 | `docs/ATLAS_BIDOPS_FIT_REPORT.md` | 明确应实现的租户接口、索引规则 |
| P0.5-03 检查 EF 配置扫描 | `docs/DECISIONS.md` | 确认是否需实现模块 EntityConfiguration 扫描 |
| P0.5-04 确认迁移路径 | `docs/DECISIONS.md` | 明确使用 Atlas.Data.Tenant.Migrations，不创建 BidOpsDbContext |
| P0.5-05 确认文件存储策略 | `docs/DECISIONS.md` | 明确 MySQL 只存元数据，文件走 IBidOpsFileStore |

### Phase 1：领域模型与数据库

| 任务 | 交付物 | 验收 |
|---|---|---|
| P1-01 创建 Domain 实体基类 | Entity/AuditableEntity | 单元测试通过 |
| P1-02 创建采集配置实体 | CrawlSource/CrawlChannel | 迁移生成 |
| P1-03 创建 CrawlJob 实体 | 任务状态机 | 状态转换测试 |
| P1-04 创建 RawNotice/Attachment | 原始层表 | 迁移生成 |
| P1-05 创建 Staging 实体 | Notice/Package/Requirement Staging | 迁移生成 |
| P1-06 创建 ReviewTask | 审核任务表 | CRUD 测试 |
| P1-07 创建 Business 实体 | Notice/Package/Requirement | 迁移生成 |
| P1-08 创建 Supplier 实体 | 厂家/联系人/能力/文件 | 迁移生成 |
| P1-09 创建 Pursuit 实体 | 作业/响应矩阵/任务/跟进 | 迁移生成 |
| P1-10 接入 Atlas Tenant EF 配置扫描 | BidOps EntityConfigurations 被 Tenant DbContext 加载 | 统一 Tenant migration 可生成；不创建 BidOpsDbContext |

### Phase 2：采集任务系统

| 任务 | 交付物 | 验收 |
|---|---|---|
| P2-01 创建 CrawlJob 服务 | 创建/领取/完成/失败 | 并发领取测试 |
| P2-02 Worker 轮询任务 | BidOps.Worker | 可消费 Pending Job |
| P2-03 实现限速服务 | Per-source throttle | 单元测试 |
| P2-04 实现失败重试 | Retry/backoff | 达到 MaxRetry 后 Failed |
| P2-05 实现全局开关 | 配置项 | 关闭后不执行抓取 |
| P2-06 实现采集日志 | Job log | API 可查看 |

### Phase 3：爬虫适配器

| 任务 | 交付物 | 验收 |
|---|---|---|
| P3-01 定义 ITenderCrawler | 接口和 DTO | 编译通过 |
| P3-02 实现 SampleFixtureCrawler | 样例 HTML 采集 | 测试全流程无需外网 |
| P3-03 实现 ManualUrlCrawler | URL 导入 | URL 可生成 DetailFetch |
| P3-04 实现 ConfigurableHtmlCrawler | CSS Selector 配置 | 样例页面可解析 |
| P3-05 实现 DetailFetch | 保存 RawNotice | HTML/Text/Hash 入库 |
| P3-06 实现 AttachmentExtractor | 提取附件链接 | 相对路径处理正确 |
| P3-07 实现 AttachmentDownload | 下载到 MinIO | Hash/StoragePath 入库 |

### Phase 4：文本抽取

| 任务 | 交付物 | 验收 |
|---|---|---|
| P4-01 定义 ITextExtractor | 接口 | 编译通过 |
| P4-02 PDF 文本抽取 | PdfPig 实现 | 样例 PDF 可抽文字 |
| P4-03 Word 文本抽取 | OpenXML/LibreOffice | 样例 DOCX 可抽文字 |
| P4-04 Excel 文本抽取 | 表格转文本 | 样例 XLSX 可抽文字 |
| P4-05 ZIP 递归处理 | 解压并处理内部文件 | 嵌套文件可处理 |
| P4-06 文本抽取失败处理 | 状态与错误日志 | 失败不阻塞整条公告 |

### Phase 5：AI 预解析

| 任务 | 交付物 | 验收 |
|---|---|---|
| P5-01 定义 AI Provider 抽象 | IAiExtractionService | 可切换 Provider |
| P5-02 Mock AI Provider | 固定 JSON 输出 | 无 Key 可跑通 |
| P5-03 公告分类解析 | NoticeType | JSON Schema 校验 |
| P5-04 基础字段解析 | NoticeStaging | 置信度保存 |
| P5-05 包件拆分解析 | PackageStaging | 多包件保存 |
| P5-06 要求项解析 | RequirementStaging | 风险字段保存 |
| P5-07 AI 结果校验 | Schema validation | 非法 JSON 进入失败状态 |
| P5-08 生成 ReviewTask | 审核任务 | 待审核池可见 |

### Phase 6：去重与变更识别

| 任务 | 交付物 | 验收 |
|---|---|---|
| P6-01 内容指纹服务 | normalized title/hash | 单元测试 |
| P6-02 完全重复识别 | Duplicate | 重复不生成新审核 |
| P6-03 疑似重复识别 | PossibleDuplicate | 进入人工合并 |
| P6-04 公告版本链 | RawNoticeVersion | 变更保留历史 |
| P6-05 变更类型识别 | 澄清/延期/中标等 | 样例文本可识别 |
| P6-06 关注项目提醒 | ReviewTask/Notification | 变更生成提醒 |

### Phase 7：审核池

| 任务 | 交付物 | 验收 |
|---|---|---|
| P7-01 审核任务列表 API | 分页/筛选 | 返回待审核数据 |
| P7-02 审核详情 API | 原文+AI结果 | 前端可渲染 |
| P7-03 审核通过 API | Staging -> Business | 正式表生成 |
| P7-04 忽略 API | 标记 Ignored | 不进入业务库 |
| P7-05 合并 API | PossibleDuplicate 合并 | 版本链正确 |
| P7-06 重析 API | 重新创建 AiParse Job | 状态正确 |
| P7-07 审核列表页面 | Vue 页面 | 可筛选/查看 |
| P7-08 审核详情页面 | 左原文右解析 | 可修改字段并通过 |

### Phase 8：正式业务库与包件中心

| 任务 | 交付物 | 验收 |
|---|---|---|
| P8-01 公告列表 API | /api/notices | 分页筛选 |
| P8-02 公告详情 API | 基础信息+包件 | 数据完整 |
| P8-03 包件列表 API | /api/packages | 按状态/品类筛选 |
| P8-04 包件详情 API | 要求项/时间节点 | 数据完整 |
| P8-05 包件中心页面 | 列表/详情 | 可查看要求项 |
| P8-06 时间节点展示 | 截止/开标等 | 即将截止突出 |

### Phase 9：厂家能力库

| 任务 | 交付物 | 验收 |
|---|---|---|
| P9-01 厂家 CRUD API | Supplier | 可增删改查 |
| P9-02 联系人 API | SupplierContact | 可维护联系人 |
| P9-03 能力标签 API | SupplierCapability | 可维护能力 |
| P9-04 证明材料 API | EvidenceDocument | 可上传/有效期 |
| P9-05 厂家列表页面 | Vue | 可搜索 |
| P9-06 厂家详情页面 | 能力/联系人/材料 | 可编辑 |
| P9-07 材料过期提醒 | ValidTo 检查 | 过期状态正确 |

### Phase 10：匹配决策台

| 任务 | 交付物 | 验收 |
|---|---|---|
| P10-01 匹配评分服务 | Rule-based scoring | 单元测试 |
| P10-02 缺失材料检测 | MissingItems | 缺失/过期可识别 |
| P10-03 匹配 API | match-suppliers | 返回推荐厂家 |
| P10-04 匹配结果页面 | 分数/原因/风险 | 可人工确认 |
| P10-05 进入作业前冲突检查 | ComplianceCheck | 同包件冲突阻断 |

### Phase 11：投标作业舱

| 任务 | 交付物 | 验收 |
|---|---|---|
| P11-01 创建 Pursuit API | 包件+厂家 | 同包件只允许一个 Active |
| P11-02 生成响应矩阵 | Requirement -> ResponseMatrix | 默认状态正确 |
| P11-03 作业任务 API | PursuitTask | 可维护任务 |
| P11-04 跟进记录 API | FollowRecord | 敏感词检测 |
| P11-05 提交前检查 API | Submit check | 缺材料/未完成项返回 |
| P11-06 作业舱页面 | 概览/矩阵/任务/跟进 | 可日常使用 |
| P11-07 结果录入 API | BidOutcome | 中标/失标/废标/放弃 |
| P11-08 复盘页面 | 原因/总结 | 可归档 |

### Phase 12：质量、测试、文档

| 任务 | 交付物 | 验收 |
|---|---|---|
| P12-01 单元测试补齐 | Domain/Application | 核心服务覆盖 |
| P12-02 API 集成测试 | 关键接口 | 测试通过或记录原因 |
| P12-03 端到端样例流程 | Fixture -> Review -> Package | 本地可演示 |
| P12-04 API 文档 | docs/API.md | 主要接口说明 |
| P12-05 数据库文档 | docs/DATABASE.md | 表和字段说明 |
| P12-06 合规文档 | docs/COMPLIANCE.md | 爬虫与投标风控边界 |
| P12-07 README 完善 | 启动/测试/演示 | 新人可复现 |

---

## 17. Codex 执行顺序建议

不要一次性尝试完成所有功能。请按以下顺序推进：

```text
Phase 0-2：让项目能跑、任务能调度
Phase 3-4：让公开信息能进入 Raw 层
Phase 5-7：让 Raw 数据能变成待审核任务并入库
Phase 8-10：让包件能匹配厂家
Phase 11：让包件进入投标作业舱
Phase 12：补测试、文档、演示流程
```

每完成一个 Phase，更新 `docs/IMPLEMENTATION_LOG.md`。

---

## 18. 验收场景

最终 MVP 至少能演示以下流程：

### 场景 A：样例 HTML 自动采集

```text
1. 系统读取 samples/html 中的公告列表。
2. 生成 RawNotice。
3. 抽取附件或模拟附件。
4. 文本抽取。
5. Mock AI 生成 NoticeStaging/PackageStaging/RequirementStaging。
6. 生成 ReviewTask。
7. 用户在前端审核通过。
8. 系统生成 Notice/TenderPackage/RequirementItem。
```

### 场景 B：手动 URL 导入

```text
1. 用户输入公开公告 URL。
2. 系统抓取详情。
3. 保存原文快照。
4. 下载公开附件。
5. 进入 AI 解析和审核流程。
```

如果外网不可用，则使用 Fixture 演示。

### 场景 C：厂家能力匹配

```text
1. 用户录入厂家 A。
2. 维护厂家产品能力和资质文件。
3. 打开某包件。
4. 点击“匹配厂家”。
5. 系统输出匹配分、满足项、缺失项、风险项。
```

### 场景 D：投标作业舱

```text
1. 用户选择一个包件和一个厂家进入投标作业。
2. 系统生成响应矩阵。
3. 用户绑定证明材料。
4. 系统提示未完成项和缺失材料。
5. 用户录入跟进记录。
6. 用户录入中标/失标/废标/放弃结果。
```

### 场景 E：合规阻断

```text
1. 同一个包件已有 Active Pursuit。
2. 用户尝试为另一个厂家创建 Pursuit。
3. 系统阻断并生成 ComplianceCheck。
```

---

## 19. 非功能要求

### 19.1 可观测性

- 所有 Job 有状态、耗时、错误、重试次数。
- 所有抓取有来源、URL、时间、Hash。
- 所有 AI 解析有输入摘要、输出 JSON、错误日志。
- 所有审核有审核人、审核时间、审核结论。

### 19.2 幂等性

- 同一 URL 重复抓取不应产生重复业务公告。
- 同一附件 Hash 不应重复存储。
- 失败重试不应重复生成多个审核任务。

### 19.3 可扩展性

- 新来源通过 Adapter 扩展。
- 新文件类型通过 TextExtractor 扩展。
- 新 AI Provider 通过接口扩展。
- 新匹配规则通过 ScoringRule 扩展。

### 19.4 安全性

- 不提交任何密钥。
- 配置通过环境变量读取。
- 上传文件限制大小和类型。
- 下载附件限制大小，避免大文件拖垮服务。
- 外部 URL 请求设置超时。
- 禁止访问内网地址，避免 SSRF。

---

## 20. 默认配置建议

```json
{
  "Crawler": {
    "GlobalEnabled": true,
    "DefaultRateLimitPerMinute": 10,
    "DefaultTimeoutSeconds": 20,
    "MaxAttachmentSizeMb": 100,
    "MaxRetryCount": 3,
    "RetryBackoffSeconds": 60,
    "UserAgent": "BidOpsCrawler/0.1 (+public tender notice monitor; contact: admin@example.com)"
  },
  "Ai": {
    "Provider": "Mock",
    "MaxInputChars": 60000,
    "SaveRawOutput": true
  },
  "Compliance": {
    "SingleActivePursuitPerPackage": true,
    "SensitiveWordCheckEnabled": true
  }
}
```

---

## 21. 给 Codex 的最终完成标准

当你认为任务完成时，必须在最终回复中说明：

1. 完成了哪些 Phase。
2. 哪些功能可以本地运行。
3. 执行过哪些测试命令。
4. 哪些测试失败或跳过，原因是什么。
5. 记录了哪些默认假设。
6. 下一步建议是什么。

不要只说“已完成”。必须给出可复核证据。

---

## 22. 一句话产品准则

> 把公开招标信息，自动转化为可审核、可拆包、可匹配、可执行、可复盘的投标作业流；同时坚持公开信息采集、人工审核确认、合规风控内置。
