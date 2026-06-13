# Decisions

## 2026-06-11 BidOps MVP Integration

- Keep Atlas on the repository's current .NET 8 target framework and central package policy. No package or framework upgrade is introduced.
- BidOps is a module at `src/Atlas.Modules.BidOps`; it is not a new solution and does not create a separate `BidOpsDbContext`.
- BidOps tables live in Atlas Tenant DB with the `bidops_` prefix. Tenant-scoped unique indexes include `TenantId`.
- Atlas did not previously scan module EF configuration assemblies. A reusable `EntityConfigurationAssemblies` module contract was added and wired into `AtlasTenantDbContext`, the tenant migration service, and the design-time migration factory.
- Tenant migrations stay in `src/Atlas.Data.Tenant.Migrations`. Design-time migration generation can load module configuration assemblies from `ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES` or `Atlas:TenantEntityConfigurationAssemblies`.
- WebApi only enqueues and queries BidOps long-running workflows. Crawl/import, attachment processing, and structured parsing are BackgroundJobs for Worker execution.
- MVP manual URL import is conservative: it records a public URL and provided/mock text through the background pipeline rather than implementing unrestricted live HTTP fetching. Real fetching should be added only with SSRF protection, timeouts, size limits, robots/site notes, and source throttling.
- HTML snapshots, attachment binaries, and large text are not stored in MySQL. `IBidOpsFileStore` stores content on local disk for MVP.
- Structured extraction writes only Staging tables. Formal Notice/Package/Requirement rows are created only after human approval.
- One active pursuit enforcement is reserved for the later pursuit phase; this first loop stops at approved Notice/Package/Requirement.

## 2026-06-11 State Grid ECP Adapter

- The State Grid adapter is limited to the public 国家电网新一代电子商务平台 portal and its public WCM API under `https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/`.
- State Grid channels should use `ListUrl = sgcc-menu:<menuId>` so crawling is driven by explicit public menu IDs, not open-ended site traversal.
- The adapter does not implement login, CAPTCHA handling, anti-bot bypass, private-data collection, or high-frequency scanning.
- Detail API failures are non-fatal for MVP: the crawler records a log entry and ingests list metadata as Raw so the review pipeline remains observable.
- Extraction after Raw uses deterministic structured parsing first, with optional external AI only when configured; no AI result writes directly to formal BidOps tables.

## 2026-06-11 Local Runtime Bootstrap

- For this local machine, Docker Desktop is installed but the daemon was not available to the current shell, and local MySQL/Redis already occupied the default Docker Compose ports.
- Local verification therefore uses isolated databases `atlas_global_bidops` and `atlas_bidops_runtime` on the existing local MySQL service.
- Because the local MySQL instance hit the known older InnoDB index-length limit during formal tenant migrations, `tools/Atlas.LocalSetup seed-bidops-state-grid` uses the existing local setup schema path for local-only runtime verification. The formal migration path remains the target for release environments.
- `TextPreview` is capped at 200 characters in ingestion. Full Raw notice text and synthetic HTML snapshots remain in `IBidOpsFileStore`.

## 2026-06-11 BidOps Production-MVP Hardening

- State Grid Raw -> Staging now uses deterministic structured extraction from real public WCM fields first. The old mock values are no longer the default extraction path.
- External AI is implemented as an optional OpenAI-compatible HTTP provider behind `BidOps:Ai:*`. It is disabled by default because it requires user-provided credentials and may incur paid-service usage.
- Attachment download and text extraction are Worker-only. MVP supports public HTTP/HTTPS attachments, size limits, local file storage, HTML/TXT extraction, DOCX text extraction through the ZIP/OpenXML document body, and basic non-OCR PDF text extraction. Scanned PDF OCR remains a later pluggable capability.
- Scheduled scanning is explicit-tenant only through `BidOps:ScheduledScan:TenantIds`; the Worker will not auto-discover or scan every tenant by default.
- Recovery is implemented as a recurring Worker task that re-enqueues `ParseQueued`/failed Raw notices and pending/failed attachment work. It uses BackgroundJobs deduplication keys with time/content context so changed notices can be reprocessed.
- Formal Notice/Package/Requirement creation continues to require review approval. AI or deterministic extraction writes only Staging rows.

## 2026-06-11 Local State Grid Restart

- BidOps URL hashing includes SPA fragments because State Grid ECP public detail links encode their document identity in `#/doc/...`.
- `BidOpsLocal` scheduled scanning is paused after the manual 4-channel verification run so the local Worker can drain attachment and structured-parse jobs without repeatedly enqueueing duplicate scans. Manual scan jobs can still be queued with `Atlas.LocalSetup seed-bidops-state-grid`.

## 2026-06-11 BidOps Admin Frontend

- Added the BidOps admin frontend as an independent Vite project under `frontend/atlas-admin`, not under `src/` and without changing the Atlas .NET solution.
- `pnpm` was not available on this machine, so npm was used according to the frontend execution spec fallback.
- The local Node runtime is `v18.16.0`; Vite is kept on the Node 18 compatible Vite 5 line instead of using current latest packages that require newer Node/Vite peers.
- Vue Router is pinned to the Vue 3 compatible 4.x line. The current latest router release in npm pulled Vite 7/8 peer expectations and was not used.
- The frontend only calls BidOps APIs that currently exist in `src/Atlas.Modules.BidOps/Controllers`. Missing detail/dashboard/attachment/timeline APIs are documented in `frontend/atlas-admin/docs/BIDOPS_FRONTEND_GAPS.md` and rendered as disabled/placeholder UI.
- The frontend uses the real Atlas login flow and permission context. Local development does not bypass authentication; it pre-fills the seeded BidOps account only for convenience.
- The Vite development proxy defaults to `http://localhost:5260` because `src/Atlas.WebApi` exposes its local HTTP launch profile there. `VITE_DEV_API_PROXY_TARGET` can still override this for HTTPS or custom ports.

## 2026-06-12 BidOps Admin Login Integration

- Atlas WebApi now exposes the existing user authentication operations from a built-in controller assembly at `/api/user/login`, `/api/user/refresh-token`, `/api/user/logout`, `/api/user/switch-store`, and `/api/user/accessible-stores`.
- Only authentication/session endpoints were promoted from the sample API surface. Sample user CRUD remains outside the production WebApi surface.
- The BidOps admin frontend stores real access/refresh tokens, tenant/store context, user display data, and permissions returned by `/api/auth/context`; it no longer seeds permissions locally.
- Added a `bidops-local` WebApi launch profile and `appsettings.BidOpsLocal.json` so local WebApi uses `atlas_global_bidops`, matching the seeded `bidops` tenant.
- The local seeded account remains a development account only: tenant domain `bidops`, username `bidops_admin`, password `Pass1234!`.

## 2026-06-12 BidOps Review Usability

- BidOps review task lists expose business review summary fields directly: project, buyer, region, key dates, package count, requirement count, reject-risk count, and parser confidence.
- Review detail reads Raw notice text from `IBidOpsFileStore` using the stored text key and falls back to `TextPreview` if the file is unavailable. Full Raw text remains outside MySQL.
- `BidOpsLocal` uses `var/bidops-storage` as the primary file-store root and keeps the old Worker bin storage as a read-only fallback for existing local data.
- Frontend BidOps identifiers are treated as strings because Atlas snowflake IDs exceed JavaScript's safe integer range. Route IDs must not be converted with `Number(...)`.

## 2026-06-12 State Grid ECP Detail URL Identity

- State Grid WCM procurement lists route public detail pages as `/doc/{doctype}/{id}_{firstPageMenuId}`, where `id` is the business `noticeId`/`id` value from `noteList`.
- `firstPageDocId` is retained as source metadata only. It must not be used as the public detail route ID for `doci-*` procurement notices because some values do not resolve in the portal detail APIs.

## 2026-06-12 State Grid ECP Attachment Identity

- State Grid `doci-win` detail pages load public attachments through `index/getWinFile` and download them through `index/downLoadWinFile`.
- The `FILE_PATH` value is an encrypted/volatile download token and can change between calls. BidOps therefore does not use the full `downLoadWinFile` URL as the stable attachment identity.
- For MVP, `BidOpsContentHasher.HashUrl` normalizes State Grid `downLoadWinFile` attachment hashes by public download path plus decoded `fileName`, ignoring the volatile `filePath` token. This is conservative for current SGCC result/candidate notices where each Raw notice exposes one public PDF attachment by name.
- Attachment binaries remain Worker-only local file-store content. The frontend opens the public source `FileUrl` returned by the backend and does not construct or expose local storage keys.

## 2026-06-12 Tenant Transaction Execution Strategy

- Tenant MySQL DbContexts enable retry-on-failure, so user-initiated transactions must run inside EF Core's configured execution strategy.
- BidOps and shared tenant services should use `IUnitOfWork.ExecuteInTransactionAsync` for multi-entity transactional work instead of calling `BeginTransactionAsync` directly from business services.
- BidOps business code still does not inject `AtlasTenantDbContext` or call `DbContext.Set<T>()`; execution strategy access stays encapsulated in `TenantUnitOfWork`.

## 2026-06-12 BidOps Attachment Text Storage

- Raw attachment metadata stays in `bidops_raw_attachment`; binary and extracted text content stay outside MySQL in `IBidOpsFileStore`.
- `RawAttachment.StorageKey` points to the downloaded source file, and `RawAttachment.TextContentStorageKey` points to the extracted `.extracted.txt` object.
- The frontend never exposes local storage keys. It can still show the public source `FileUrl`, reads extracted text through `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/text`, and reads original downloaded binaries through authorized `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file`.
- In `BidOpsLocal`, relative file-store paths resolve from the repository root first. WebApi also keeps `src/Atlas.Worker/var/bidops-storage` and the older Worker bin storage as fallback roots so local data created before the path alignment remains readable.

## 2026-06-12 BidOps Controlled Attachment Preview And Download

- Original downloaded attachment binaries are served only through authorized backend APIs. The API opens `IBidOpsFileStore` by `RawAttachment.StorageKey` after tenant/data-scope checks and never returns the storage key to the frontend.
- `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file` returns `inline` content by default for browser preview and uses `?download=true` for attachment download.
- The frontend uses Axios blob requests so the existing Authorization header, tenant id, and store id are sent. It does not rely on query-string tokens or unauthenticated direct file URLs.
- Missing RawNotice, missing attachment metadata, missing storage key, or missing local file returns 404 instead of placeholder success.

## 2026-06-12 BidOps PDF Text Extraction

- PDF extraction uses the stable `PdfPig` package instead of scanning raw PDF bytes with regular expressions.
- This is required for State Grid WPS/SimSun/Identity-H PDFs where real text is encoded through PDF font/CMap mappings; raw byte scanning surfaces compressed streams and PDF object markers as garbage.
- PDF text normalization now preserves page line breaks and horizontal spacing where PdfPig exposes it, instead of applying the general `\s+` whitespace collapse used for HTML/TXT snippets. This keeps review text readable and table-like rows separated, but it remains text extraction rather than pixel-perfect PDF rendering.
- MVP still does not perform OCR. Image-only or scanned PDFs will need a later OCR provider, while text-based PDFs should now produce readable text.

## 2026-06-12 BidOps Raw Notice Display Text

- Raw notice files remain source-oriented snapshots in `IBidOpsFileStore` so deterministic parsing, traceability, and source diffing can still use original public API field paths.
- Review and Raw detail APIs format Raw notice text into Chinese display labels at query time. This hides internal source field names such as `resultValue.notice.CONT`, converts known fields to labels such as `公告内容`/`发布单位`/`代理机构`, removes internal IDs and volatile file tokens, and strips HTML tags for readable review.
- Attachment extracted text is not passed through this source-field formatter because PDF/DOC/TXT attachment text should remain as extracted document text.

## 2026-06-12 BidOps Package Detail Timeline

- MVP package timeline is a read model synthesized from existing Notice, TenderPackage, and RequirementItem timestamps. It does not introduce a new workflow-event table yet.
- Timeline events currently cover public notice publication, formal notice import, package creation, requirement generation, and package update when an update timestamp exists.
- Package detail APIs stay under the existing `bidops.business.read` permission and tenant data-scope repository path.

## 2026-06-12 Background Task Observability P0

- Initial P0 task observability used the existing global `BackgroundJobs` table without adding `BackgroundJobEvent`, `RecurringTaskRun`, or `OperationLogEntry`. P0-07 later adds the lightweight `BackgroundWorkerHeartbeat` table for Worker liveness.
- Background job query/management code lives in `Atlas.BackgroundTasks` because module projects are not allowed to reference `AtlasGlobalDbContext` directly.
- BidOps operations pages call the shared background job operations service and keep BidOps tenant data queries on repositories/DataScope.
- P0 system operations routes use existing BidOps crawl permissions for local usability. Dedicated `ops.*` permissions and a standalone Operations module remain P1 hardening work.
- Running jobs are not force-canceled. P0 cancel only marks Pending/Failed/Dead jobs as `Canceled`; retry always creates a new job row.

## 2026-06-12 BidOps Worker Heartbeat P0

- Worker liveness is stored in the Atlas Global DB as `BackgroundWorkerHeartbeat` because Worker identity and queues are process-wide rather than tenant-owned business data.
- The Worker upserts heartbeat rows best-effort every 15 seconds and throttles warning logs if the table or database is unavailable. A heartbeat failure must not stop job processing.
- `WorkerId` is constrained to `varchar(191)` so its unique index remains compatible with older MySQL/InnoDB index byte limits.
- `GET /api/ops/workers` is implemented in the shared background task operations service and exposed through the BidOps module for P0 compatibility.
- The P0 endpoint keeps existing BidOps crawl read permission compatibility. Dedicated system operations permissions remain a separate hardening step.

## 2026-06-12 BidOps Non-Breaking Module Evolution

- The 13-module BidOps expansion is an additive product/module blueprint, not a destructive refactor mandate.
- Existing BidOps controllers, API routes, request/response semantics, permission codes, frontend routes, entities, and table ownership remain compatible.
- New module names such as `Intelligence`, `Processing`, `Opportunities`, `Operations`, and future fine-grained permissions must be added alongside the current `crawl`, `review`, and `business` surface.
- Any future cleanup or route/permission deprecation requires a separate compatibility decision and migration plan; it must not be bundled into feature delivery.

## 2026-06-12 BidOps Unimplemented API Semantics

- Planned BidOps APIs that are not implemented should remain unregistered or return `501 NotImplemented` with an explicit explanation.
- Unimplemented APIs must not return `200 OK`, empty arrays, empty objects, default DTOs, `success: true`, or placeholder text as if the operation succeeded.
- Empty results are allowed only for implemented read models where the data source and query path are real and the tenant simply has no matching data.
- Frontend ComingSoon routes must not force backend placeholder success endpoints into existence.

## 2026-06-12 BidOps Operations Permission Compatibility

- `bidops.ops.read` and `bidops.ops.manage` are now registered as BidOps P0 operations permissions and exposed to the frontend permission constants.
- Existing operations APIs still keep the old `bidops.crawl.read/manage` authorization policies for compatibility with seeded and existing local tenant roles.
- Switching runtime authorization from `crawl` to `ops` requires a separate compatibility step: either backend OR-permission policies or a role migration that grants `ops` permissions before old policies are removed.

## 2026-06-12 BidOps Raw Notice Reparse Entry

- P0 reparse is a reviewer action under the existing `bidops.review.approve` permission. A future `bidops.processing.reparse` permission can be added without removing the existing route.
- `POST /api/bidops/raw-notices/{id}/reparse` only changes RawNotice/staging/review task state and enqueues Worker work. WebApi does not run attachment processing or structured parsing synchronously.
- Approved RawNotice rows and RawNotice rows already imported into formal Notice records cannot be reparsed in MVP; callers receive an error instead of silently mutating formal business data.
- Manual reparse carries a force-run id through attachment processing so the subsequent structured parse uses a new deduplication key even when the RawNotice content hash has not changed.
- Attachment processing keeps duplicate-content runs idempotent by retaining a RawAttachment's existing URL identity hash when the downloaded file content hash is already used by another attachment on the same RawNotice.

## 2026-06-12 BidOps Opportunity MVP

- Opportunity is an additive BidOps tenant model layered on top of existing `Notice`, `TenderPackage`, and `RequirementItem`; it does not replace formal tendering data created by review approval.
- MVP enforces at most one active opportunity per package with tenant-scoped `(TenantId, PackageId, ActiveMarker)` uniqueness. Active rows use `ActiveMarker = "active"` and inactive/closed rows clear the marker.
- Existing package and business read surfaces stay compatible: opportunity list/detail APIs allow the current `bidops.business.read` policy, while create/update/watch/assess/stage use the new `bidops.opportunity.*` permissions.
- Dedicated opportunity calendar, notification delivery records, and separate assessment-record APIs remain unimplemented. Frontend calendar/assessment routes must stay `ComingSoon` until real APIs and tasks exist.
- The formal tenant migration lives in `src/Atlas.Data.Tenant.Migrations`. The existing local BidOps tenant database has historical tables but missing EF migration history, so local smoke used `tools/Atlas.LocalSetup ensure-bidops-opportunities` to create the new tables and seed permissions idempotently without replaying old migrations into existing tables.

## 2026-06-12 BidOps Dashboard Summary

- `GET /api/bidops/dashboard/summary` is the first real business dashboard API. It reads existing tenant data from RawNotice, ReviewTask, Notice, TenderPackage, RequirementItem, and Opportunity; it does not create mock dashboard rows.
- `bidops.dashboard.read` is registered and seeded, but the API currently uses `bidops.business.read` for runtime authorization compatibility with existing local roles. A later role migration can switch it to the dashboard-specific permission.
- Split APIs such as `/todos`, `/deadlines`, `/risks`, and `/pipeline` remain unimplemented until they have dedicated read models.

## 2026-06-12 BidOps Opportunity Maintenance Jobs

- Opportunity maintenance runs as a recurring task that enqueues auditable one-time jobs on the existing `bidops` queue.
- `bidops.opportunity.value-assessment` only fills missing initial value score/level for active opportunities. It does not overwrite human-entered assessment values or make Go/No-Go decisions.
- `deadline-reminder`, `watch-reminder`, and `stale-state-scan` currently produce background job results with real counts. They do not send external messages or create notification records until a later notification model exists.

## 2026-06-12 BidOps Supplier Capability MVP

- Supplier capability is implemented as an additive Phase C BidOps tenant model: `Supplier`, `SupplierContact`, `SupplierCapability`, and `SupplierEvidenceDocument`.
- Supplier evidence keeps only metadata, public/controlled file URL references, hash fields, and effective dates in MySQL. Binary content remains outside MySQL through file storage; `StorageKey` is not exposed in frontend DTOs.
- Local MySQL compatibility required avoiding long string composite indexes that exceed older InnoDB 767-byte key limits. MVP keeps tenant-scoped practical indexes and relies on normal filtered queries for supplier name/category search until a later search index or generated normalized key is introduced.
- `bidops.supplier.evidence-expiry-scan` updates evidence statuses to valid, expiring soon, or expired as a Worker job. Notification delivery and acknowledgement records are intentionally left for the later notification/config phase.
- Supplier list/detail APIs are real repository-backed endpoints. Dedicated child GET endpoints and the evidence-expiry list remain unimplemented until they have their own query surfaces; frontend routes for those views stay `ComingSoon`.

## 2026-06-12 BidOps Matching MVP

- Matching is implemented as an additive Phase D BidOps tenant model: `SupplierMatchRun`, `SupplierMatchResult`, `MissingEvidenceCheck`, and `GoNoGoDecision`.
- WebApi only starts and queries matching work. `POST /api/bidops/packages/{id}/match-suppliers` creates a run and enqueues `bidops.matching.supplier-match-run`; Worker executes matching and persists results.
- MVP scoring is deliberately rule-based and explainable, using package category, region, supplier quality, and evidence coverage. It records missing/expired/expiring evidence instead of hiding gaps behind a single score.
- Go/No-Go remains an explicit human decision record. The matching service does not automatically create pursuits, submit bids, contact suppliers, or override opportunity stages.
- Matching APIs and pages are real repository-backed surfaces. Matching rules, scoring versioning, and score-refresh APIs remain unimplemented until a `MatchingRuleSet` model and audit trail are added.
- The frontend auth guard now refreshes `/api/auth/context` once per app startup for authenticated sessions so newly seeded permissions such as `bidops.matching.*` appear without forcing users to clear cached permission state manually.

## 2026-06-12 BidOps Pursuit MVP

- Pursuit is an additive Phase E BidOps tenant model layered on top of `Notice`, `TenderPackage`, optional `Opportunity`, optional `GoNoGoDecision`, and optional supplier snapshots.
- MVP enforces at most one active pursuit per package with tenant-scoped `(TenantId, PackageId, ActiveMarker)` uniqueness. Active rows use `ActiveMarker = "active"` and closed/archived rows clear the marker.
- `PursuitTask` and `PursuitFollowRecord` are operational tracking records only. They do not generate bid documents, submit bids, contact suppliers, or create external side effects.
- Creating a pursuit from a Go/No-Go decision is allowed only when the referenced decision is `Go`; non-Go decisions fail explicitly instead of silently starting work.
- Work APIs and pages are real repository-backed surfaces. Calendar, deadline reminders, overdue-task scans, response matrix, bid file versioning, and submission checks remain unimplemented until their own models and tasks exist.
- The existing local BidOps tenant database predates formal migration history, so local smoke uses `tools/Atlas.LocalSetup ensure-bidops-pursuits --tenant atlas_bidops_runtime` to create tables and seed permissions idempotently without replaying historical migrations.

## 2026-06-12 BidOps Parsed Text Data Quality

- `UNSPECIFIED` is no longer a persisted package or lot number. Unknown package identifiers stay empty in Raw/Staging/Formal data and the frontend displays them as `待补录`.
- Values that are only question marks, replacement characters, or question marks plus generated numeric suffixes are treated as unreadable placeholders. Required supplier fields reject these values; optional supplier fields are normalized to empty.
- The deterministic parser now attempts to extract package and lot numbers from public text/HTML labels such as `包件号`, `包号`, `标包号`, `分包编号`, `标段号`, and `分标编号` before leaving the value empty.
- Local historical cleanup is handled through `tools/Atlas.LocalSetup repair-bidops-data-quality`; it only targets obvious placeholder values and does not invent real package numbers or supplier names.

## 2026-06-12 BidOps Supplier Analysis Read Model

- BidOps suppliers currently come from the tenant supplier master library (`Supplier`) through manual/API creation or local setup data. Public notice crawling does not automatically create supplier master records.
- `GET /api/bidops/suppliers/analysis/summary` is a real read model over existing Supplier, capability, evidence, matching result, Go/No-Go, and pursuit data. It uses `bidops.supplier.read` for compatibility and does not add a new analytics permission in this compatibility step.
- The new `/bidops/suppliers/analysis` page replaces the former `ComingSoon` capability route while keeping `/bidops/suppliers/capabilities` as a frontend alias.
- Outcome-announcement extraction of candidate/winning suppliers is implemented as a public-result lead table (`OutcomeSupplierRecord`). Durable `SupplierPerformance`, win-rate analytics, and review conclusions remain unimplemented until the results/review module owns `BidOutcome` or equivalent source data.

## 2026-06-12 BidOps Public Outcome Supplier Leads

- Public中标/成交/候选公示里的厂家信息 is stored as `OutcomeSupplierRecord`, not as automatic `Supplier` master data. The record preserves source announcement, evidence text, notice/package snapshots, outcome type, rank, amount, and a weak link to an existing supplier when the normalized name already matches.
- WebApi only exposes reads and enqueues `bidops.outcome.supplier-extract`; extraction runs in Worker and is idempotent per RawNotice by deleting and rebuilding that RawNotice's outcome leads.
- The extractor intentionally prefers precision over recall. It only accepts explicit supplier/candidate/winner fields or equivalent table keys, and skips announcement intros, buyer/agency/publisher fields, supplier instructions, fee/payment/postage text, and generic company-name mentions.
- Public result tables with explicit supplier columns such as `成交人`/`中标人` and package/lot context are treated as valid outcome leads, including institutional winners such as universities and institutes. Buyer, agency, contact, and date lines remain table boundaries and are not supplier leads.
- When PDF table extraction fractures rows, supplier cleanup keeps only a standalone final organization name. Rows that only expose package/service fragments, short generic company fragments, or buyer-side `供电公司` names are discarded to protect lead quality.
- Fragment names consisting only of formal organization suffixes or short suffix tails, such as `有限公司`, `技有限公司`, `工程有限公司`, `务有限公司`, `股份有限公司`, `科技有限公司`, and `研究院有限公司`, are discarded even if this may miss rare very short legal names. Unbalanced parenthesis fragments such as `周口龙润电力（集团` are also discarded.
- Award amounts are stored on `OutcomeSupplierRecord.AwardAmount` as CNY yuan when the public result text has explicit amount labels or an explicit result-table amount/price column with a recognized unit. Percentages, discount rates, fee rates, and score cells are intentionally ignored to avoid treating `97.50%` or `88.00` points as money.
- Outcome leads are used for analysis and manual package-to-supplier research. The system must not auto-create contacts, auto-contact suppliers, infer private relationships, or generate collusion/bribery-oriented recommendations.

## 2026-06-13 BidOps ZIP And Excel Attachment Extraction

- Tender attachments can carry material requirements inside ZIP files and Excel workbooks, so Worker text extraction now treats `.zip`, `.xlsx`, `.xlsm`, `.xltx`, and `.xltm` as first-class public attachment inputs.
- ZIP processing stays in memory, does not extract files to disk, skips unsupported entries, caps archive depth/entry count/entry size, and recursively processes supported inner documents before structured parsing.
- Excel OpenXML workbooks are parsed through their worksheet/shared-string XML so package tables, quantities, budget rows, qualification notes, and other sheet details become part of the Raw attachment extracted text.
- Legacy binary `.doc` and `.xls` files use a conservative readable-text fallback instead of a heavy conversion dependency. High-fidelity legacy Office conversion remains a later pluggable capability.
- AI/deterministic parsing still reads only extracted text and writes Staging rows. Excel/ZIP content does not bypass human review or write directly to formal BidOps business tables.

## 2026-06-13 BidOps State Grid Manual Detail Import

- Manual URL import detects public State Grid ECP portal detail routes such as `#/doc/doci-bid/{noticeId}_{menuId}` and delegates fetching to the Worker-side StateGridEcp crawler instead of storing only the pasted URL.
- `doci-bid` details with `fileFlag = 1/true` are modeled as a public `downLoadBid?noticeId=...` ZIP attachment so announcement ZIP files enter the normal attachment download, text extraction, staging, and human-review pipeline.
- `fileFlag = 0/false` is treated as an explicit no-attachment signal. If `fileFlag` is missing or unknown, the parser adds the `downLoadBid?noticeId=...` candidate only when no explicit attachment list was discovered, which recovers public SGCC ZIP公告附件 without duplicating known PDF/list attachments.
- Fallback State Grid detail documents also keep the `doci-bid` `downLoadBid?noticeId=...` candidate. Existing RawNotice reparse jobs process already-recorded attachment rows and do not rediscover missing attachment metadata; historical zero-attachment `doci-bid` rows need re-import/re-crawl or a dedicated backfill.
- The generic WebApi import endpoint remains enqueue-only. It does not fetch State Grid pages synchronously, and non-State-Grid URLs still use the existing conservative manual import fallback.
- Manual State Grid imports reuse source/channel enabled flags, no-login enforcement, allowed SGCC host checks, rate limiting, attachment metadata storage, and file-store rules.
- The frontend manual import page follows the same boundary: it submits only a public URL and optional metadata, then polls the background job and RawNotice pipeline. It does not add browser-side scraping, cookies, login-state capture, or synchronous crawling.

## 2026-06-13 BidOps State Grid Attachment Table Recognition

- DOCX extraction now preserves Word table row/column structure as Markdown tables in the existing extracted text artifact. This keeps the current storage and review flow intact while giving deterministic and AI parsers a table-shaped input.
- Standard State Grid ECP procurement attachment tables are parsed by rules before generic text heuristics: `项目概况与采购范围` produces package candidates, and `响应供应商专用资格要求` produces qualification, performance, personnel, and joint-venture requirements matched back to packages.
- State Grid qualification tables often use merged Word headers. The extractor merges selected child header rows with preceding parent header rows, and the parser also fills blank `分标` / `包号` / `包名称` columns for qualification-table Markdown that was produced before the extractor fix.
- MVP does not add new formal table-source columns. Table source row information is included in requirement explanations and the full Markdown table remains in the attachment extracted text for reviewer traceability.
- A structured sidecar JSON and orphan requirement review surface remain planned follow-ups; this step avoids schema churn while improving package/requirement recall for standard SGCC ZIP+DOCX公告附件.
- Manual reparse force-runs attachment text extraction before structured parsing so deterministic parser changes can be applied to already downloaded attachments without mutating formal approved data.
- Frontend extracted-text preview uses a narrow in-component Markdown subset renderer for headings, paragraphs, and tables instead of adding a full Markdown dependency. It avoids `v-html`, keeps arbitrary text escaped by Vue, and focuses on making extracted procurement tables reviewable.

## 2026-06-13 BidOps Historical Attachment Backfill

- Historical attachment repair is a Worker job (`bidops.raw.attachment-backfill`) started by WebApi, not synchronous crawling in the API request.
- The backfill is intentionally scoped to public State Grid ECP portal `doci-bid` detail URLs for 招标/采购公告. It does not add browser-side scraping, cookies, login-state capture, CAPTCHA handling, or anti-bot bypass.
- Backfill reuses the existing StateGridEcp crawler so source/channel pause flags, no-login checks, allowed SGCC host validation, and rate limiting still apply.
- Approved, ignored, and already-formal Raw notices are skipped in MVP. Missing ZIP attachments and parser improvements refresh Raw/Staging review data only where human review is still pending or can be regenerated safely.
- Requirement staging `OriginalText` is capped to the original tenant migration's `varchar(256)` storage shape for compatibility with existing local/historical tenant databases. Full attachment text remains in `IBidOpsFileStore`, so the cap only affects the review summary snippet persisted in MySQL.
- The frontend date formatter treats backend ISO datetimes with `Z` as UTC and renders them in the browser's current timezone. ISO datetime strings without an explicit zone are normalized as UTC before display so `采集时间` does not appear shifted by server/local parsing ambiguity.

## 2026-06-13 BidOps Outcome Supplier Amount Presentation

- Outcome supplier record search keeps its existing default publish-time ordering for compatibility, but now supports `hasAwardAmount` and `sortBy=AwardAmountDesc/AwardAmountAsc` so UI surfaces can explicitly ask for amount-bearing leads.
- The supplier analysis page defaults its lead table to `hasAwardAmount=true` and amount-descending sorting because users open this view to inspect public outcome clues with monetary value, not just the newest extracted rows.
- Public outcome supplier summary now prioritizes suppliers with positive cumulative parsed award amount, then cumulative amount, then lead counts. Count-only suppliers are still visible after amount-bearing suppliers rather than hidden.
- Parsed award amounts remain evidence-backed public-result clues. They are not treated as reviewed BidOutcome business facts and do not automatically update supplier master data, contacts, or pursuit decisions.
