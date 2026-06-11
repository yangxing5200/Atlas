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
