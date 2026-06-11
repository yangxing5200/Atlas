# Implementation Log

## 2026-06-11 BidOps MVP Loop

Integrated BidOps as an Atlas business module and completed the MVP loop:
public notice import/mock crawl -> Raw -> Staging -> human review -> formal Notice/Package/Requirement.

Completed:

- Phase 0 read-only Atlas evaluation and fit report.
- Phase 0.5 decisions for Tenant DB ownership, module EF scanning, unified tenant migration, and local file storage.
- Added generic module EF configuration scanning support before adding BidOps entities.
- Added `src/Atlas.Modules.BidOps` with permissions, menu entries, controllers, query service, domain services, background job handlers, local file storage, mock crawler/import, and mock AI/rule parsing.
- Added tenant-owned BidOps Raw, Staging, Review, and Formal entities using `bidops_` table names and tenant-scoped indexes.
- Generated tenant migration `20260611060338_v0.2.3-bidops-mvp` in `src/Atlas.Data.Tenant.Migrations`.
- Registered the module in WebApi, Worker, MigrationJob, and the tenant migration design-time project.
- Promoted tenant provisioning API into Atlas built-in WebApi controllers and removed the duplicate sample-only controller.
- Added `bidops` to the default Worker one-time job queues.
- Added `src/Atlas.Worker/appsettings.BidOpsLocal.json` so BidOps MVP can run locally without Docker by disabling RabbitMQ and using DB-polled BackgroundJobs.
- Registered BidOps EF configurations in LocalSetup only as a local development schema helper; the formal BidOps startup path remains Global migration plus MigrationJob against real tenants.
- Added BidOps module/service tests and documented the MVP runbook.

Verification:

- `dotnet restore Atlas.sln` succeeded.
- `dotnet build Atlas.sln --no-restore` succeeded after the startup documentation, tenant provisioning API, and Worker queue changes. The final build reported existing warnings in test projects, with 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build` succeeded: 51 passed.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `dotnet test Atlas.sln --no-build` did not fully pass because existing integration tests require additional local environment setup: cache integration tests fail on missing `ICurrentIdentity` registration, and global database tests fail on local MySQL/schema/index prerequisites.
- `docker compose config` could not run because Docker is not installed or not on PATH in this environment.

## 2026-06-11 State Grid ECP Public Adapter

Implemented the first real public crawl adapter for 国家电网新一代电子商务平台.

Completed:

- Added `IStateGridEcpCrawler` implementation that runs only from Atlas.Worker background jobs.
- Added public WCM API parsing for State Grid ECP `noteList` responses.
- Added detail endpoint mapping for bid/change/delay/waste/win notice doctypes, with metadata fallback when a detail API call fails.
- Kept HTML link parsing as a fallback for absolute public SGCC pages.
- Routed `SourceType = StateGridEcp` channel scans from the existing `scan-now` API into `bidops.crawl.state-grid-ecp-scan` jobs.
- Kept notice body/synthetic HTML snapshots in `IBidOpsFileStore`; MySQL receives metadata, hashes, preview text, and statuses.
- Added parser and module registration tests.
- Documented source/channel setup and verified menu IDs in `docs/BIDOPS/BIDOPS_MVP_RUNBOOK.md`.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore` succeeded: 53 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build` succeeded: 53 passed.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `dotnet build Atlas.sln --no-restore` was blocked by the currently running local `Atlas.WebApi (26080)` process locking `src\Atlas.WebApi\bin\Debug\net8.0\Atlas.Modules.BidOps.dll`; no BidOps compile errors were reported before the copy lock failure.

## 2026-06-11 Local State Grid Runtime Bootstrap

Configured and started a local State Grid crawl run.

Completed:

- Added `seed-bidops-state-grid` to `tools/Atlas.LocalSetup` to create isolated local runtime databases, seed a formal BidOps tenant, configure State Grid ECP source/channels, and enqueue crawl jobs.
- Added `bidops-status` to `tools/Atlas.LocalSetup` to inspect BackgroundJobs and BidOps Raw/Staging/Review counts without requiring a MySQL CLI.
- Added runtime references from Worker/WebApi to `Atlas.Data.Global.Migrations` so Global DbContext entity configurations are available at runtime.
- Reduced Raw notice preview storage to 200 characters. Full notice text remains in `IBidOpsFileStore`; MySQL keeps only a short preview.
- Created local runtime databases `atlas_global_bidops` and `atlas_bidops_runtime`.
- Seeded State Grid ECP source and four public channels:
  - `sgcc-menu:2018032700291334`
  - `sgcc-menu:2018032900295987`
  - `sgcc-menu:2018060501171107`
  - `sgcc-menu:2018060501171111`
- Started Atlas.Worker against `atlas_global_bidops` with `DOTNET_ENVIRONMENT=BidOpsLocal` and `BidOps:StateGridEcp:MaxNoticesPerScan=2`.
- Started Atlas.WebApi on `http://localhost:5260`; Swagger returned HTTP 200.

Runtime result:

- State Grid crawl jobs succeeded for all four seeded channels.
- The MVP pipeline reached Raw -> Staging -> Review locally.
- Latest status observed: `RawNotices=1`, `NoticeStaging=1`, `ReviewTasks=1`.

Environment notes:

- Docker CLI exists at `C:\Program Files\Docker\Docker\resources\bin\docker.exe`, but Docker daemon was not running/accessible from this shell.
- Local ports `3306` and `6379` were already occupied by local services, so Docker Compose was not used.
- Formal tenant migration against local MySQL failed on an existing Atlas index-length limitation (`max key length is 767 bytes`); the isolated LocalSetup path was used for local runtime verification.

## 2026-06-11 BidOps Production-MVP Hardening

Completed:

- Replaced the default mock AI extractor with `BidOpsStructuredExtractionService`.
- Added deterministic structured extraction for State Grid WCM Raw text, including project code, buyer, agency, region, notice type, publish/open/signup deadlines, category, package fallback, and deadline/qualification/rejection-risk requirements.
- Added optional OpenAI-compatible structured extraction behind `BidOps:Ai:*`; it is disabled by default and falls back to deterministic extraction on failure.
- Added public attachment discovery from State Grid WCM JSON and HTML detail pages.
- Added `RawAttachmentCandidate` ingestion and idempotent `bidops_raw_attachment` metadata upsert.
- Added `BidOpsAttachmentProcessingService` plus `AttachmentProcessJobHandler` for Worker-only public attachment download and text extraction.
- Added `BidOpsTextExtractor` for TXT/HTML/DOCX/basic non-OCR PDF extraction without introducing new package dependencies.
- Changed the job chain to `crawl/import -> attachment process -> structured parse -> review task`; the legacy mock parse handler remains registered for old queued jobs only.
- Added content-hash change detection in Raw ingestion. Changed Raw notices are marked `ParseQueued` so Staging can be refreshed.
- Added reparse support for existing Staging rows by replacing package/requirement staging data and reopening the review task.
- Added `BidOpsScheduledScanTask` for explicit-tenant recurring scans and `BidOpsRecoveryTask` for parse/attachment compensation.
- Enabled scheduled scan/recovery for local `BidOpsLocal` tenant `300001`; default Worker config keeps scheduled scanning disabled.
- Added service tests for State Grid attachment discovery, real deterministic extraction, text extraction, and module registration.
- Added `Atlas.LocalSetup bidops-status` Formal table output so local verification can inspect approved Notice/Package/Requirement data without a MySQL CLI.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore` succeeded: 56 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build` succeeded: 56 passed.
- `dotnet build Atlas.sln --no-restore` initially failed only because running local `Atlas.Worker`/`Atlas.WebApi` processes locked `Atlas.Modules.BidOps.dll`.
- After stopping the local development Worker/WebApi processes, `dotnet build Atlas.sln --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test Atlas.sln --no-build --verbosity minimal` was executed. Non-integration test projects passed, but existing `Atlas.Integration.Tests` failed outside the BidOps path: cache integration setup cannot resolve `ICurrentIdentity`, `GlobalDataSeederTest` still targets missing `atlas_global`, and global database tests hit the known local MySQL key-length limit.
- Local runtime approval verification succeeded: `FormalNotices=1`, `FormalPackages=1`, `FormalRequirements=3`; latest formal notice is project code `23FG10`, buyer `国网吉林省电力有限公司超高压公司`, region `吉林`, signup deadline `2026-06-18 08:00:00Z`, bid/open time `2026-07-02 10:00:00Z`.

## 2026-06-11 Local Restart and State Grid Crawl Verification

Completed:

- Restarted local MySQL `MySQL56`, Atlas.WebApi, and Atlas.Worker after machine reboot.
- Increased local-only State Grid scan size to 20 notices per channel.
- Fixed BidOps URL hashing to include SPA URL fragments. State Grid ECP detail links use `#/doc/...`; ignoring fragments collapsed multiple public notices into one Raw record.
- Added tests for SPA fragment URL hashing and distinct State Grid WCM detail URLs.
- Paused `BidOpsLocal` scheduled scans after the manual verification run to prevent duplicate local crawl backlog while attachment/parse jobs catch up.
- Added `Atlas.LocalSetup cancel-bidops-crawl-jobs` for local cleanup of duplicated pending/running State Grid crawl jobs.
- Re-ran State Grid public scans for four configured channels.

Runtime result:

- `RawNotices=81`, `NoticeStaging=81`, `ReviewTasks=81`, `FormalNotices=1`, `FormalPackages=1`, `FormalRequirements=3`.
- The 81 Raw/Staging/Review rows include the existing previously approved Raw plus up to 20 public notices from each configured State Grid channel.
- Current State Grid public responses did not expose downloadable attachment URLs for these notices, so `RawAttachments=0`.

Verification:

- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 58 passed.
- Atlas.WebApi was reachable at `http://localhost:5260/swagger/index.html` with HTTP 200.
