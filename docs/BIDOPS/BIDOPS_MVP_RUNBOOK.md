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
snapshots. They do not automatically create supplier master data or contact
actions.

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
$env:DOTNET_ENVIRONMENT='BidOpsLocal'
$env:ConnectionStrings__AtlasGlobal='Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;'
dotnet run --no-build --project src\Atlas.Worker\Atlas.Worker.csproj
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
- Attachment extraction MVP supports TXT/HTML/DOCX and basic non-OCR PDF text. Scanned PDF OCR is intentionally left as a later pluggable extractor.

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
