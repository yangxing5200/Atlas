# BidOps × Atlas 数据归属与集成注意事项

> 目标：把 BidOps 作为 Atlas 的业务模块接入，而不是另起一个独立系统。本文是 Codex 执行时必须遵守的数据库、模块边界、迁移和文件存储规则。

---

## 1. 核心结论

BidOps 的数据设计应遵循：

```text
代码边界：Atlas.Modules.BidOps
模型归属：BidOps 模块 owns Entities / EntityConfigurations / Services / Queries / Jobs
物理存储：Atlas Tenant DB
表名前缀：bidops_
租户隔离：TenantId / ITenantEntity
迁移执行：Atlas.Data.Tenant.Migrations 统一生成和执行
文件二进制：对象存储或本地文件存储，不进 MySQL
```

明确区分两件事：

```text
“BidOps 模块拥有自己的业务表和模型” 是正确的。
“BidOps MVP 阶段拥有独立物理数据库 / 独立 DbContext” 不推荐。
```

---

## 2. 不要在 MVP 阶段给 BidOps 单独建库

Atlas 的基础架构是：

```text
Global DB：租户、认证、系统控制、跨租户配置
Tenant DB：租户业务数据
```

BidOps 的以下数据都属于租户业务数据：

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
证明材料元数据
投标作业
响应矩阵
结果复盘
合规检查
```

因此 MVP 阶段不要创建：

```text
bidops_db
BidOpsDbContext
独立 BidOps migration pipeline
跨租户共享 RawNotice 库
```

正确做法是：

```text
tenant_db
├── atlas / system tables
├── bidops_crawl_source
├── bidops_crawl_channel
├── bidops_raw_notice
├── bidops_raw_attachment
├── bidops_notice_staging
├── bidops_package_staging
├── bidops_requirement_staging
├── bidops_review_task
├── bidops_notice
├── bidops_tender_package
├── bidops_supplier
├── bidops_supplier_capability
├── bidops_evidence_document
├── bidops_pursuit
├── bidops_response_matrix
└── bidops_bid_outcome
```

---

## 3. 模块拥有模型，宿主统一迁移

推荐模式：

```text
BidOps 模块：定义 Entities + EntityConfigurations
Atlas Tenant DB：承载表
Atlas.Data.Tenant.Migrations：生成/执行最终迁移
```

不要把 BidOps 实体随意散落到：

```text
Atlas.WebApi
Atlas.Services
Atlas.Data.Tenant
Atlas.Core
```

除非 Atlas 当前版本硬性要求实体必须放在某个共享项目中。即便必须兼容，也要在 `docs/DECISIONS.md` 中说明原因，并把模块边界和后续迁移计划写清楚。

---

## 4. 必须先检查 AtlasTenantDbContext 的模块配置扫描能力

Codex 在编码前必须检查：

```text
AtlasTenantDbContext.OnModelCreating 是否只扫描 Atlas.Data.Tenant 程序集？
AtlasModule 是否已经支持声明 EF EntityConfiguration 程序集？
MigrationJob 是否能加载模块程序集里的 IEntityTypeConfiguration<T>？
```

如果当前 Atlas 还没有“模块 EF 配置扫描机制”，不要把 BidOps 的配置硬塞进宿主项目。应优先实现一个通用扩展点，例如：

```csharp
public abstract class AtlasModule
{
    public virtual IReadOnlyCollection<Assembly> EntityConfigurationAssemblies
        => Array.Empty<Assembly>();
}
```

BidOps 模块声明：

```csharp
public sealed class BidOpsModule : AtlasModule
{
    public override IReadOnlyCollection<Assembly> EntityConfigurationAssemblies
        => new[] { typeof(BidOpsModule).Assembly };
}
```

Tenant DbContext 或模型构建扩展扫描：

```text
Atlas.Data.Tenant 自身配置
+
已启用模块声明的 EntityConfiguration 程序集
```

要求：

```text
不要为 BidOps 做一次性硬编码。
实现为未来模块也能复用的机制。
不要在 AtlasTenantDbContext 上新增大量 DbSet<TEntity>。
```

---

## 5. 业务代码不得绕过 Atlas 数据访问边界

BidOps Web/API/Application 代码不要直接：

```text
注入 AtlasTenantDbContext
注入 ITenantDbContextFactory
调用 DbContext.Set<T>()
调用 FromSql* / ExecuteSqlRaw / ExecuteSqlInterpolated
调用 IgnoreQueryFilters
信任请求体里的 TenantId / StoreId
```

优先使用：

```text
IRepository<TEntity>
QueryService
领域服务
后台任务上下文提供的 TenantId
Atlas 现有事务/审计/数据范围机制
```

唯一索引和幂等键必须按租户隔离，例如：

```text
TenantId + SourceCode + DetailUrlHash
TenantId + RawNoticeId + FileHash
TenantId + TenderPackageId + ActivePursuitStatus
TenantId + BusinessKey
```

---

## 6. Raw / Staging / Formal 三层都放 Tenant DB

MVP 阶段三层数据都按租户隔离：

```text
Raw：这个租户抓到了什么、什么时候抓到、原文和附件是什么。
Staging：AI/规则把它理解成什么，等待人工审核。
Formal：人工确认后的正式业务对象。
```

不要提前做全局共享公告库，因为这会引入：

```text
不同租户审核状态不同
不同租户关注规则不同
不同租户 AI 配置不同
不同租户供应商能力库不同
同一公告如何推送给多租户
全局去重与租户去重冲突
```

后续 SaaS 阶段再考虑：

```text
Global DB
├── bidops_global_raw_notice
├── bidops_global_raw_attachment
└── bidops_tenant_notice_inbox
```

---

## 7. Global DB 只放系统级元数据，MVP 可暂不使用

MVP 阶段尽量不要新增 Global DB 表。

可选的 Global 数据仅限：

```text
BidOps 模块启用状态
系统默认采集来源模板
全局公告来源字典
全局公告类型字典
AI Provider 系统级配置模板
```

租户的实际采集配置、审核结果、厂家库、作业数据，都放 Tenant DB。

---

## 8. 文件二进制不要进 MySQL

招标文件、PDF、Word、Excel、Zip、HTML 快照、附件正文文本，不要直接存 MySQL 大字段。

数据库只存元数据：

```text
FileName
ContentType
FileSize
FileHash
StorageProvider
StorageKey
TextContentStorageKey
DownloadStatus
ExtractStatus
```

BidOps 模块内定义文件存储抽象：

```csharp
public interface IBidOpsFileStore
{
    Task<StoredFileInfo> SaveAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
```

MVP 可先实现：

```text
LocalBidOpsFileStore
```

后续实现：

```text
MinIOBidOpsFileStore
S3CompatibleBidOpsFileStore
```

---

## 9. BidOps 推荐目录结构

```text
src/Atlas.Modules.BidOps
├── ModuleEntry.cs / BidOpsModule.cs
├── Authorization
│   └── BidOpsAuthorizationCatalog.cs
├── Entities
│   ├── Crawling
│   ├── Staging
│   ├── Tendering
│   ├── Suppliers
│   ├── Pursuits
│   └── Compliance
├── EntityConfigurations
│   ├── Crawling
│   ├── Staging
│   ├── Tendering
│   ├── Suppliers
│   ├── Pursuits
│   └── Compliance
├── Services
├── Queries
├── Controllers
├── BackgroundJobs
├── Crawling
│   ├── Abstractions
│   ├── Sources
│   └── Parsers
├── Documents
├── Ai
├── Compliance
└── Models
    ├── Requests
    ├── Responses
    └── Dtos
```

如 Atlas 模板已有固定目录，以 Atlas 模板为准，但必须保留 BidOps 模块边界。

---

## 10. Codex 必须执行的检查清单

在实现 BidOps 数据模型前，Codex 必须完成：

```text
[ ] 确认 Atlas 当前 TargetFramework，不升级框架版本。
[ ] 确认 Atlas 模块系统如何注册模块。
[ ] 确认 Atlas 模块模板的标准目录结构。
[ ] 确认 Atlas Tenant DB 的实体配置扫描方式。
[ ] 确认是否已有模块 EntityConfiguration 扫描扩展点。
[ ] 确认 Tenant 实体应实现的接口，例如 ITenantEntity / ISharedEntity / IStoreOnlyEntity。
[ ] 确认 Repository / QueryService 的推荐写法。
[ ] 确认 MigrationJob 如何生成和执行租户库迁移。
[ ] 在 docs/ATLAS_BIDOPS_FIT_REPORT.md 记录结果。
[ ] 在 docs/DECISIONS.md 记录所有兼容性取舍。
```

---

## 11. 给 Codex 的硬性指令片段

可直接复制到任务 Prompt：

```text
BidOps 数据模型应逻辑上归属于 Atlas.Modules.BidOps，但 MVP 阶段不要创建独立物理数据库、独立 BidOpsDbContext 或独立 migration pipeline。

所有 BidOps 业务表放入 Atlas Tenant DB，表名统一使用 bidops_ 前缀，所有租户业务实体必须实现 Atlas 当前要求的租户隔离接口，并通过 TenantId 隔离。

BidOps 模块应包含 Entities、EntityConfigurations、Services、Queries、Controllers、BackgroundJobs 等代码。

如果当前 AtlasTenantDbContext 尚未支持扫描模块程序集中的 EntityConfigurations，应先实现通用的模块化 EF 配置扫描机制，而不是把 BidOps 的所有实体和配置硬塞进 Atlas.Core 或 Atlas.Data.Tenant。

迁移由 Atlas.Data.Tenant.Migrations 统一生成和执行。不要在 MVP 阶段为 BidOps 创建单独数据库或单独 DbContext。

WebApi 不得直接注入 AtlasTenantDbContext，不得调用 DbContext.Set<T>()。业务访问必须走 IRepository、QueryService 或领域服务。

不要把附件二进制、HTML 快照或大段文本直接写入 MySQL。数据库只存元数据，文件内容通过 IBidOpsFileStore 保存到本地文件或对象存储。
```
