# BidOps MVP Runbook

Date: 2026-06-12

## Scope

BidOps is implemented as the first formal Atlas-based business product module at `src/Atlas.Modules.BidOps`.
It uses Atlas Tenant DB physical storage, Atlas repositories/query services,
Atlas authorization/menu registration, and Atlas BackgroundJobs.

The completed MVP loop is:

```text
manual public URL import or mock crawl
-> bidops_raw_notice
-> attachment metadata/download/text extraction in Worker
-> structured staging rows
-> bidops_review_task
-> human approve
-> bidops_notice / bidops_tender_package / bidops_requirement_item
```

Phase B through Phase E add the first经营层 capabilities on top of this loop:

```text
formal TenderPackage
-> Opportunity management / dashboard / maintenance scans
-> Supplier profile / contacts / capabilities / evidence metadata
-> supplier evidence expiry scan in Worker
-> package supplier matching / missing evidence checks / Go-No-Go decisions
-> Pursuit work tracking / tasks / follow records / stage transitions
```

## Host Wiring

- WebApi registers BidOps controllers, permissions, menus, services, and enqueue/query endpoints.
- Worker registers BidOps background job handlers.
- MigrationJob registers BidOps EF configurations for tenant schema upgrades.
- LocalSetup can load BidOps EF configurations only as a local development helper. It is not the formal startup or migration path.
- Tenant migration design-time generation can load module EF configurations through:

```powershell
$env:ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES='Atlas.Modules.BidOps'
dotnet ef migrations add v0.2.3-bidops-mvp --project src\Atlas.Data.Tenant.Migrations --startup-project src\Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext
```

## Formal Startup

Use Atlas production-style hosts for BidOps: Global migration, tenant schema MigrationJob, WebApi, and Worker. Do not start BidOps through `samples/Atlas.Sample.WebApi`, and do not use demo seed data as the formal product path.

1. Start infrastructure.

BidOps MVP can run locally without Docker. The required dependency is MySQL.
Redis and RabbitMQ can stay offline for the MVP path because WebApi uses memory
cache and the Worker can process `BackgroundJobs` by polling the Global DB.

No-Docker local minimum:

- Install and start MySQL 8 locally as a Windows service or another local process.
- Keep the default root password/connection string from `appsettings.json`, or override `ConnectionStrings__AtlasGlobal`.
- Create/use `atlas_global` for the Global DB.
- Create/use one tenant database, for example `atlas_bidops`.

Optional Docker equivalent:

```powershell
docker compose up -d mysql redis rabbitmq
```

2. Restore and build.

```powershell
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
```

3. Apply Global DB migrations.

```powershell
dotnet ef database update --project src\Atlas.Data.Global.Migrations --startup-project src\Atlas.Data.Global.Migrations --context AtlasGlobalDbContext
```

4. Create or update the tenant DB schema for the local BidOps tenant.

For a no-Docker local run, apply tenant migrations directly to the local tenant
database first so tenant provisioning can create the headquarters store.

```powershell
$env:ATLAS_TENANT_DB_CONNECTION='Server=localhost;Port=3306;Database=atlas_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;'
$env:ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES='Atlas.Modules.BidOps'
dotnet ef database update --project src\Atlas.Data.Tenant.Migrations --startup-project src\Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext
```

5. Start WebApi.

```powershell
dotnet run --project src\Atlas.WebApi\Atlas.WebApi.csproj
```

Swagger uses the WebApi launch profile:

```text
http://localhost:5260/swagger
https://localhost:7282/swagger
```

6. Create or provision the real BidOps tenant.

Atlas.MigrationJob scans active/trial tenants from Global DB, then applies tenant migrations to their configured tenant databases. Before running it, ensure the target tenant has:

- a `DatabaseMasterServer` record;
- a `DatabaseInstance` record;
- an active or trial `Tenant` record;
- a valid tenant connection string pointing to the BidOps tenant database.

The formal Atlas WebApi exposes the tenant provisioning endpoint:

```http
POST /api/tenant-provisioning
```

It requires the `tenant.provisioning` permission and creates the Global tenant plus the tenant headquarters store. The database server and database instance records must already exist. If the platform-admin UI/token bootstrap is not ready yet, create those records through a controlled bootstrap script. Do not use `seed-demo` as the formal BidOps tenant source.

Example provisioning body:

```json
{
  "name": "BidOps",
  "domain": "bidops",
  "brandName": "BidOps",
  "phoneNumber": "010-00000000",
  "contactName": "BidOps Admin",
  "contactPhoneNumber": "13800000000",
  "contactEmail": "admin@bidops.local",
  "province": "Beijing",
  "city": "Beijing",
  "category": "TenderOps",
  "tenantType": "Enterprise",
  "businessType": "Chain",
  "databaseInstanceId": 1,
  "officeCount": 1,
  "headquartersName": "BidOps Headquarters",
  "headquartersCode": "BIDOPS-HQ"
}
```

7. Plan and apply tenant migrations.

```powershell
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- plan
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- apply --batch-size 100
```

8. Start Worker so BidOps queued jobs are processed.

No-Docker local Worker:

```powershell
$env:DOTNET_ENVIRONMENT='BidOpsLocal'
dotnet run --project src\Atlas.Worker\Atlas.Worker.csproj
```

Default/full Worker, when RabbitMQ is available:

```powershell
dotnet run --project src\Atlas.Worker\Atlas.Worker.csproj
```

Important: do not run Worker with the current `Development` config unless you override background tasks, because `src/Atlas.Worker/appsettings.Development.json` disables `BackgroundTasks:OneTimeJobs`. For BidOps MVP, Worker must have one-time jobs enabled and include the `bidops` queue. `src/Atlas.Worker/appsettings.BidOpsLocal.json` and `src/Atlas.Worker/appsettings.json` both include `bidops`.

Minimal Worker overrides if needed:

```powershell
$env:BackgroundTasks__OneTimeJobs__Enabled='true'
$env:BackgroundTasks__OneTimeJobs__Queues__0='default'
$env:BackgroundTasks__OneTimeJobs__Queues__1='tenant'
$env:BackgroundTasks__OneTimeJobs__Queues__2='export'
$env:BackgroundTasks__OneTimeJobs__Queues__3='bidops'
dotnet run --project src\Atlas.Worker\Atlas.Worker.csproj
```

## Local Developer Shortcut

For a developer-only machine, `tools/Atlas.LocalSetup` can create local schemas from the current model. This is only a convenience path and removes indexes for older local MySQL compatibility; it is not the formal BidOps migration path.

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- init-global
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- create-tenant-db
```

Avoid `seed-demo` for BidOps acceptance or release verification unless the goal is explicitly to test Atlas sample/demo data.

## Main API Surface

All endpoints use Atlas permission policies.

- `GET /api/bidops/crawl-sources`
- `POST /api/bidops/crawl-sources`
- `PUT /api/bidops/crawl-sources/{id}`
- `POST /api/bidops/crawl-sources/{id}/enable`
- `POST /api/bidops/crawl-sources/{id}/disable`
- `GET /api/bidops/crawl-channels`
- `POST /api/bidops/crawl-channels`
- `PUT /api/bidops/crawl-channels/{id}`
- `POST /api/bidops/crawl-channels/{id}/scan-now`
- `GET /api/bidops/dashboard/summary`
- `GET /api/bidops/raw-notices`
- `GET /api/bidops/raw-notices/{id}`
- `GET /api/bidops/raw-notices/{id}/pipeline`
- `GET /api/bidops/raw-notices/{id}/attachments`
- `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/text`
- `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file`
- `POST /api/bidops/raw-notices/{id}/reparse`
- `POST /api/bidops/raw-notices/import-url`
- `POST /api/bidops/raw-notices/backfill-attachments`
- `GET /api/bidops/crawl-run-logs`
- `GET /api/bidops/crawl-run-logs/{id}`
- `GET /api/bidops/review-tasks`
- `GET /api/bidops/review-tasks/{id}`
- `POST /api/bidops/review-tasks/{id}/approve`
- `POST /api/bidops/review-tasks/{id}/ignore`
- `GET /api/bidops/processing/failures`
- `GET /api/bidops/notices`
- `GET /api/bidops/notices/{id}`
- `GET /api/bidops/packages`
- `GET /api/bidops/packages/{id}`
- `GET /api/bidops/packages/{id}/timeline`
- `GET /api/bidops/packages/{id}/requirements`
- `GET /api/bidops/opportunities`
- `POST /api/bidops/opportunities`
- `GET /api/bidops/opportunities/{id}`
- `PUT /api/bidops/opportunities/{id}`
- `POST /api/bidops/opportunities/{id}/watch`
- `POST /api/bidops/opportunities/{id}/assess`
- `POST /api/bidops/opportunities/{id}/stage`
- `GET /api/bidops/suppliers`
- `POST /api/bidops/suppliers`
- `GET /api/bidops/suppliers/{id}`
- `PUT /api/bidops/suppliers/{id}`
- `POST /api/bidops/suppliers/{id}/contacts`
- `POST /api/bidops/suppliers/{id}/capabilities`
- `POST /api/bidops/suppliers/{id}/evidence-documents`
- `POST /api/bidops/packages/{id}/match-suppliers`
- `GET /api/bidops/matching/runs`
- `GET /api/bidops/matching/runs/{id}`
- `GET /api/bidops/matching/runs/{id}/results`
- `GET /api/bidops/packages/{id}/decisions`
- `POST /api/bidops/packages/{id}/decisions`
- `GET /api/bidops/pursuits`
- `POST /api/bidops/pursuits`
- `GET /api/bidops/pursuits/{id}`
- `PUT /api/bidops/pursuits/{id}`
- `POST /api/bidops/pursuits/{id}/status`
- `GET /api/bidops/pursuits/{id}/tasks`
- `POST /api/bidops/pursuits/{id}/tasks`
- `PUT /api/bidops/pursuits/{id}/tasks/{taskId}`
- `GET /api/bidops/pursuits/{id}/follow-records`
- `POST /api/bidops/pursuits/{id}/follow-records`
- `GET /api/ops/background-jobs`
- `GET /api/ops/background-jobs/summary`
- `GET /api/ops/background-jobs/{id}`
- `POST /api/ops/background-jobs/{id}/retry`
- `POST /api/ops/background-jobs/{id}/cancel`
- `GET /api/ops/workers`
- `GET /api/bidops/operations/dashboard`
- `GET /api/bidops/operations/jobs`
- `GET /api/bidops/operations/config-check`
- `GET /api/bidops/operations/channels/health`
- `GET /api/bidops/operations/worker-heartbeats`
- `POST /api/bidops/operations/jobs/{id}/retry`
- `POST /api/bidops/operations/jobs/{id}/cancel`

Example manual import body:

```json
{
  "sourceId": 123,
  "channelId": 456,
  "detailUrl": "https://example.gov/public/tender/notice-1",
  "title": "Public tender notice",
  "noticeType": "TenderAnnouncement",
  "textContent": "Optional public notice text for MVP ingestion."
}
```

The request enqueues a background job and returns `202 Accepted`; parsing and
review task creation happen in Worker.

Frontend manual import is available at:

```text
http://localhost:5173/bidops/intelligence/manual-import
```

The page accepts a public procurement announcement URL, submits the same
enqueue-only API, polls the background job result, and shows the generated
RawNotice pipeline when Worker returns `rawNoticeId=...`. Source/channel/title
and fallback text fields are kept under advanced options; normal State Grid ECP
manual imports only need the announcement URL.

Historical State Grid ECP `doci-bid` Raw notices that were crawled before ZIP
attachment discovery was fixed can be backfilled through a Worker job:

```http
POST /api/bidops/raw-notices/backfill-attachments
Content-Type: application/json

{
  "maxItems": 100,
  "includeAlreadyProcessed": true,
  "forceReparse": true
}
```

The job re-checks eligible public SGCC招标/采购公告 detail URLs, records missing
`downLoadBid?noticeId=...` ZIP attachment metadata, enqueues attachment download
and text extraction, then forces a structured staging reparse. It skips Raw
notices that are approved, ignored, or already imported into formal Notice rows
so historical backfill does not silently mutate approved business data.

The frontend entry is on the Raw notice list:

```text
http://localhost:5173/bidops/crawl/raw-notices
```

Use the `补齐历史附件` action after WebApi and Worker have both loaded the latest
`Atlas.Modules.BidOps.dll`.

## Reverse Lifecycle Closure Debug

The first package-lifecycle debug entry starts from a public State Grid ECP
`doci-win` result notice and tries to link backward:

```text
Award result notice -> candidate announcement -> tender/procurement announcement
```

The target sample URL is:

```text
https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522123684_2018060501171111
```

`doci-win` is treated as an award/win notice. The existing State Grid Worker
adapter parses the URL as `doctype=doci-win`, `noticeId=2606128522123684`,
`menuId=2018060501171111`, calls `index/getNoticeWin`, then reads win
attachments through `index/getWinFile` and `downLoadWinFile`.

Debug API:

```http
POST /api/bidops/lifecycle/debug/reverse-close-url
Content-Type: application/json

{
  "url": "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522123684_2018060501171111",
  "resetDerivedData": false,
  "persistEvidence": true
}
```

If the RawNotice already exists and Worker has processed its attachments, the
response includes `awardEvidence`, candidate/tender notice matches, extracted
candidate/tender evidence, and `closures`. State Grid WCM HTML tables such as
`分标编号 / 分标名称 / 包号 / 中标状态 / 项目单位 / 中标人` are parsed into
award evidence fields including `lotNo`, `lotName`, `packageNo`, `projectUnit`,
and `awardedSupplierName`. If the RawNotice is not available yet, the API
enqueues the existing manual import job and returns a warning with the job id.
WebApi does not crawl or download attachments synchronously.

For an already known RawNotice id:

```http
POST /api/bidops/lifecycle/debug/reverse-close-raw-notice/{rawNoticeId}
```

The debug closure is DTO-only in this phase. It does not write supplier
capability facts and does not create formal lifecycle tables. Missing links are
reported explicitly, for example `candidate notice not found`, `tender notice
not found`, `award amount missing`, `package number ambiguous`, or
`parser template not matched`.

Development-only derived-data cleanup is available through LocalSetup. It is
dry-run by default and protects RawNotice, RawAttachment, CrawlSource,
CrawlChannel, CrawlRunLog, attachment files, and supplier master data:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- reset-bidops-derived-data --dry-run `
  --tenant-id 300001 `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

Only execute the delete in a development database:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- reset-bidops-derived-data --confirm `
  --tenant-id 300001 `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

## Attachment Text Extraction

Attachment download and text extraction run only in Atlas.Worker. Binary files
and extracted long text stay in `IBidOpsFileStore`; MySQL stores only metadata,
hashes, storage keys, and status fields.

Supported MVP extraction inputs:

- TXT, CSV, HTML
- PDF with a text layer
- DOCX
- XLSX/XLSM/XLTX/XLTM worksheet text
- ZIP archives containing supported files, processed recursively with depth,
  entry count, and entry size limits
- Legacy DOC/XLS through a conservative readable-text fallback

Extracted attachment text is appended to the Raw notice text before deterministic
or optional AI structured parsing. The parser still writes only Staging rows;
human review is required before formal Notice/Package/Requirement records are
created.

## Optional DeepSeek Extraction

DeepSeek is wired through the existing optional `BidOps:Ai:*` settings and is
disabled by default outside the local BidOps profile. Worker is the important
process to configure because it owns crawling, attachment text extraction,
structured parsing, and outcome supplier lead extraction. Background job payloads
do not carry API keys; the Worker resolves the key from its effective
configuration or `DEEPSEEK_API_KEY` when the job executes.

```powershell
$env:BidOps__Ai__Enabled='true'
$env:BidOps__Ai__Provider='DeepSeek'
$env:BidOps__Ai__BaseUrl='https://api.deepseek.com'
$env:BidOps__Ai__Model='deepseek-v4-flash'
$env:BidOps__Ai__UseForNoticeStaging='true'
$env:BidOps__Ai__UseForOutcomeSuppliers='true'
$env:DEEPSEEK_API_KEY='...'
dotnet run --project src\Atlas.Worker\Atlas.Worker.csproj
```

If `BidOps:Ai:Endpoint` is empty, DeepSeek requests are sent to
`https://api.deepseek.com/chat/completions`. `BidOps:Ai:ApiKey` may be used
instead of `DEEPSEEK_API_KEY` for local-only configuration files that are not
committed. In `BidOpsLocal`, do not set `BidOps:Ai:ApiKey` to an empty string;
that shadows lower-priority configuration and makes the Worker behave as if no
token exists.

DeepSeek outcome extraction is a correction/enrichment layer, not a business
import shortcut. Deterministic result parsers run first; DeepSeek receives the
public Raw notice text, extracted attachment text, and deterministic reference
rows, then returns strict JSON supplier lead candidates. The Worker still writes
only `bidops_outcome_supplier_record` lead rows and conservative buyer/supplier
master-data links. `采购代理服务费` is stored separately in
`ProcurementAgencyServiceFeeAmount` and is not counted as `AwardAmount`.

When a reviewer submits a correction prompt from the review-detail page, the
DeepSeek result is treated as the replacement set for that Raw notice's
中标/候选明细. Existing outcome rows are deleted before the Worker persists the
DeepSeek rows, and deterministic historical rows are not merged back into that
prompted run. Reviewers can also manually add, edit, or delete outcome rows
before approval; approval then syncs buyer/supplier master data from the edited
rows.

## State Grid ECP Public Adapter

The State Grid adapter is implemented for the public 国家电网新一代电子商务平台
portal only:

- Public portal: `https://ecp.sgcc.com.cn/ecp2.0/portal/`
- Public WCM API host/path used by the portal: `https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/`
- No login, CAPTCHA, anti-bot bypass, private data collection, or high-frequency crawling is implemented.
- WebApi only enqueues the scan. Atlas.Worker executes the crawl, attachment processing, and structured parse jobs.
- Large notice text and synthetic HTML snapshots go through `IBidOpsFileStore`, not MySQL.

Create the source with `SourceType = StateGridEcp`:

```json
{
  "code": "state-grid-ecp",
  "name": "国家电网新一代电子商务平台",
  "sourceType": "StateGridEcp",
  "baseUrl": "https://ecp.sgcc.com.cn/ecp2.0/portal/",
  "enabled": true,
  "rateLimitPerMinute": 6,
  "crawlIntervalMinutes": 60,
  "maxRetryCount": 3,
  "needJsRender": false,
  "needLogin": false,
  "respectRobots": true,
  "robotsPolicyNote": "Public State Grid ECP portal data only. No login, captcha, bypass, or private data access."
}
```

Then create channels. For State Grid ECP, prefer `ListUrl = sgcc-menu:<menuId>`.
The adapter uses the menu ID to call the portal's public WCM list API and falls
back to HTML parsing only when no menu ID is configured.

Known public menu IDs verified on 2026-06-11:

| Channel | ListUrl | Suggested NoticeType |
| --- | --- | --- |
| 招标公告及投标邀请书 | `sgcc-menu:2018032700291334` | `TenderAnnouncement` |
| 采购公告 | `sgcc-menu:2018032900295987` | `ProcurementAnnouncement` |
| 推荐中标候选人公示 | `sgcc-menu:2018060501171107` | `CandidateAnnouncement` |
| 中标（成交）结果公告 | `sgcc-menu:2018060501171111` | `AwardAnnouncement` |

Example channel body:

```json
{
  "sourceId": 123,
  "code": "sgcc-tender-announcements",
  "name": "国家电网招标公告及投标邀请书",
  "noticeType": "TenderAnnouncement",
  "listUrl": "sgcc-menu:2018032700291334",
  "region": "CN",
  "industry": "Power",
  "enabled": true
}
```

Start a scan:

```http
POST /api/bidops/crawl-channels/{channelId}/scan-now
```

Expected job chain:

```text
StateGridEcpCrawlJobHandler
-> public ECP WCM noteList/detail API
-> bidops_raw_notice
-> bidops_raw_attachment metadata, when public attachments are discovered
-> AttachmentProcessJobHandler
-> public attachment download + text extraction through IBidOpsFileStore
-> StructuredParseJobHandler
-> bidops_notice_staging / bidops_package_staging / bidops_requirement_staging
-> bidops_review_task
```

Manual import of a public State Grid ECP detail URL uses the same Worker-side
adapter when the URL matches `#/doc/{doctype}/{noticeId}_{menuId}`. For example:

```http
POST /api/bidops/raw-notices/import-url
Content-Type: application/json

{
  "sourceId": 330001,
  "channelId": 330102,
  "detailUrl": "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128544990232_2018032900295987",
  "noticeType": "TenderAnnouncement"
}
```

For `doci-bid` detail responses where `fileFlag = 1`, the Worker records the
public announcement package as a ZIP attachment through
`/ecp2.0/ecpwcmcore/index/downLoadBid?noticeId=...`. The ZIP then follows the
normal attachment download, text extraction, structured staging, and human
review flow.

When ZIP entries contain DOCX procurement公告附件, the extractor preserves Word
tables as Markdown tables in the attachment extracted text. Standard State Grid
ECP tables are recognized before the generic text heuristic:

- `项目概况与采购范围` -> package candidates.
- `响应供应商须满足如下专用资格要求` -> qualification, performance, personnel, and joint-venture requirement candidates matched back to packages.

Important: after changing adapter code, restart WebApi and Worker so the running
processes load the new `Atlas.Modules.BidOps.dll`.

### Local State Grid Bootstrap

For a local machine where Docker Desktop is installed but the daemon is not
running, or where local MySQL has older index limits, use the isolated LocalSetup
bootstrap below. It creates local runtime databases, seeds a formal BidOps tenant,
configures State Grid ECP source/channels, and enqueues scan jobs.

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- seed-bidops-state-grid `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

If the local BidOps tenant DB was created before the Phase B opportunity
migration and has no EF migration history, prepare opportunity tables and
permissions idempotently for local smoke testing:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-opportunities `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

For a local BidOps tenant created before the Phase C supplier migration,
prepare supplier tables and permissions idempotently for local smoke testing:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-suppliers `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

For a local BidOps tenant created before the Phase D matching migration,
prepare matching tables and permissions idempotently for local smoke testing:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-matching `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

For a local BidOps tenant created before the Phase E pursuit migration,
prepare pursuit tables and permissions idempotently for local smoke testing:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-pursuits `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

For a local BidOps tenant created before the public outcome supplier lead
migration, prepare the outcome lead table idempotently:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-outcomes `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

Public outcome supplier leads come from explicit supplier/candidate/winner
fields in public中标/成交/候选公示 and extracted attachments. They are stored in
`bidops_outcome_supplier_record` as traceable leads with source and package
snapshots. They conservatively upsert buyer and supplier master records by
normalized public organization name, and link records through `BuyerId` and
`SupplierId` where possible. They do not automatically create contacts,
capabilities, qualification evidence, pursuit decisions, or external contact
actions.

Review detail shows the parsed buyer and public result supplier rows before
approval under `采购方与厂家`. Approval is the review-scoped association gate:
approving the review task creates or links the buyer and supplier master
records if needed, records the buyer procurement history in
`bidops_buyer_procurement_record`, and associates each outcome supplier row with
the existing or newly-created supplier. If public outcome extraction already
created or linked a master-data shell, approval refreshes it idempotently. The
supplier shell records which public announcement created it through
`CreatedFrom*` fields; existing suppliers keep the association through
`LastOutcome*` and `OutcomeSupplierRecord.SupplierId`.

If persisted outcome rows are missing on an older or failed parse review task,
the review detail page builds a read-only preview from the stored Raw notice
text/HTML plus already-extracted attachment text. The preview helps reviewers
inspect buyers and suppliers before approval; it does not write master data or
replace the Worker/approval extraction paths.

Worker structured parsing attempts outcome supplier extraction even when the
generic procurement staging parser cannot fully parse a result/candidate
announcement. Approval also runs a missing-outcome-row check before the review
transaction, so existing review tasks can still extract and link public award
rows from the original Raw notice before creating buyer/supplier associations.

Raw notice detail exposes two recovery actions for unapproved announcements:
`重新导入` re-fetches the public URL in Worker with `forceRefresh=true`, refreshes
stored Raw text/HTML even when the content hash is unchanged, forces attachment
text extraction, and queues a fresh structured parse. `重解析` keeps the stored
Raw content and only reruns attachment text extraction plus structured parsing.
Both paths rebuild that RawNotice's outcome supplier lead rows, including any
DeepSeek-assisted result rows when optional AI is enabled. Approved Raw notices
are protected from force refresh in MVP.

State Grid ECP crawling is exposed through `IBidOpsCrawlAdapter` and currently
implemented by `StateGridEcpCrawlAdapter`. The adapter participates in
source/detail URL validation and declares support for inline HTML announcement
tables and public attachment discovery, including PDF, Office, Excel, and
ZIP/RAR attachment metadata. Attachment binaries and extracted long text still
go through `IBidOpsFileStore`; parsing still runs in Worker jobs before staging
or outcome lead creation.

State Grid WCM imports preserve the original `CONT` HTML as the Raw HTML
snapshot when available. Word/WPS-style tables such as `p.MsoNormal` or
`MsoNormalTable` are parsed by table headers. If a result table omits
采购编号/招标编号 or 分标编号/分标名称, the parser uses regex fallback from the
announcement正文 and the text immediately before the table; `流标`/`废标` rows are
not converted into supplier outcome records.

To backfill local public outcome supplier leads after improving extraction rules
or attachment text extraction, use the authenticated API:

```powershell
POST /api/bidops/suppliers/outcome-records/backfill?maxItems=200
```

If historical local smoke data contains parser placeholders such as
`UNSPECIFIED`, `????`, or question-mark-plus-timestamp supplier names, run the
idempotent data-quality repair command. It does not invent real identifiers; it
clears unknown package numbers and marks unreadable supplier names as
`待补录厂家`.

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- repair-bidops-data-quality `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

Start Worker against the isolated Global DB:

```powershell
.\scripts\restart-worker.ps1
```

The restart script writes Worker output to `var\logs\worker-local.log` and sets
`DOTNET_ENVIRONMENT=BidOpsLocal` / `ASPNETCORE_ENVIRONMENT=BidOpsLocal` before
launching the process. If starting Worker manually, set those environment
variables first; otherwise it may load the default RabbitMQ-enabled config.

Manual equivalent:

```powershell
$env:DOTNET_ENVIRONMENT='BidOpsLocal'
$env:ASPNETCORE_ENVIRONMENT='BidOpsLocal'
$env:ConnectionStrings__AtlasGlobal='Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;'
dotnet run --no-build --project src\Atlas.Worker\Atlas.Worker.csproj
```

For focused one-off parser smoke tests, recurring recovery and maintenance tasks can be disabled for the local Worker process while leaving one-time jobs enabled:

```powershell
$env:BackgroundTasks__Recurring__Enabled='false'
$env:BidOps__Recovery__Enabled='false'
$env:BidOps__OpportunityMaintenance__Enabled='false'
$env:BidOps__SupplierMaintenance__Enabled='false'
```

`BidOpsLocal` enables recurring scan/recovery, opportunity maintenance, and
supplier evidence maintenance for tenant `300001`:

```json
{
  "BidOps": {
    "ScheduledScan": {
      "Enabled": true,
      "TenantIds": [ 300001 ],
      "MaxChannelsPerCycle": 4
    },
    "Recovery": {
      "Enabled": true,
      "TenantIds": [ 300001 ]
    },
    "OpportunityMaintenance": {
      "Enabled": true,
      "TenantIds": [ 300001 ]
    },
    "SupplierMaintenance": {
      "Enabled": true,
      "TenantIds": [ 300001 ],
      "EvidenceWarningDays": 30
    },
    "StateGridEcp": {
      "MaxNoticesPerScan": 2
    }
  }
}
```

Check local status without a MySQL CLI:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- bidops-status `
  --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;" `
  --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"
```

## Storage Rules

- MySQL stores metadata, hashes, preview fields, status fields, and formal business rows.
- HTML snapshots, attachment binaries, and extracted long text go through `IBidOpsFileStore`.
- MVP implementation is `LocalBidOpsFileStore`.
- Default local storage root is `storage/bidops` under the host base directory unless `BidOps:FileStore:LocalRootPath` is configured.
- Attachment extraction MVP supports TXT/HTML/DOCX, ZIP archives, OpenXML Excel workbooks, conservative legacy DOC/XLS readable-text fallback, and basic non-OCR PDF text. Scanned PDF OCR and high-fidelity legacy Office conversion are intentionally left as later pluggable extractors.
- DOCX extraction preserves Word tables as Markdown in attachment extracted text. Standard State Grid ECP `采购公告附件` tables are parsed deterministically into staging packages and requirements, including merged qualification headers such as `响应供应商专用资格要求` over `资质要求` / `业绩要求` / `人员要求`.

## Compliance Guardrails

- `NeedLogin = true` crawl sources are rejected by the module service.
- Manual import accepts only `http` and `https` URLs.
- Generic unrestricted live HTTP fetching is intentionally not implemented in MVP.
- The State Grid ECP adapter only calls the public portal WCM APIs under source/channel rate limits.
- Structured extraction writes only Staging rows.
- Optional external AI uses `BidOps:Ai:*` only when explicitly configured with credentials; it is disabled by default.
- Formal Notice/Package/Requirement rows are created only by human approval.

## Verification

Commands run on 2026-06-11:

```powershell
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore
dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore
rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps
```

Results:

- Restore succeeded.
- Full solution build succeeded with 0 warnings and 0 errors after stopping local development Worker/WebApi processes that were locking `Atlas.Modules.BidOps.dll`.
- BidOps module build succeeded with 0 warnings and 0 errors.
- Services tests passed: 56 passed.
- Forbidden BidOps data-access pattern search returned no matches.

Known environment gaps:

- `dotnet test Atlas.sln --no-build` still fails in existing integration tests that require additional local test setup.
- `docker compose config` could not run because Docker is not installed or not on PATH.
