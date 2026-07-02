# Decisions

## 2026-07-01 BidOps Closure Count Display Safety

- Lifecycle list reads may backfill amount candidates, but amount-candidate evidence must not be able to break closure row display. If an extracted amount exceeds the `decimal(18,6)` storage range, BidOps stores the candidate as unresolved with raw text and evidence instead of persisting an invalid numeric value.
- Opening the closure center with a specific `rawNoticeId` is an announcement-detail workflow. It should clear cached lifecycle status/match filters so operators see the complete public result set, including read-only `StatusOnly` 流标 rows, unless they explicitly filter again.

## 2026-07-01 BidOps Failed Outcome Display Rows

- Outcome rows whose supplier cell is `流标` / `废标` / `失败` remain non-actionable: they do not create persisted lifecycle links and cannot be confirmed, rejected, used for supplier master data, or used as final award amount evidence.
- Lifecycle closure filtered by a specific RawNoticeId still needs to display those public status rows. The API therefore synthesizes read-only `StatusOnly` rows from failed `OutcomeSupplierRecord` entries for display only, while persisted `LifecyclePackageLink` rows remain limited to actionable awarded/candidate suppliers.
- Result notices may award to individual business names such as `服务部`, `经营部`, `商行`, or `工作室`. These names are valid public supplier evidence and must not be dropped merely because they do not contain `公司` / `院` / `所`.

## 2026-07-01 BidOps AI Request Diagnostics

- Background jobs that call AI must include request-side diagnostics together with response diagnostics. The stored shape is `requestSummaryJson`, `requestBodyJson`, and `requestPrompt`, so operators can see what model, transport, schema, prompt, and notice/attachment context were sent.
- Request diagnostics may include public notice text, extracted attachment text, and reviewer prompts because these are the evidence needed to reproduce an AI extraction. Secrets are not evidence: API keys, authorization headers, tokens, cookies, passwords, and secret-like fields are redacted before diagnostics are stored.
- Structured parse, outcome supplier extraction, and lifecycle field enrichment use the same AI diagnostic shape. This keeps job-result troubleshooting consistent whether the provider is OpenAI-compatible HTTP or Codex CLI.

## 2026-07-01 BidOps Award Result Detail Source

- Award/result notices can mention `中标候选人公示活动已经结束` as background text. This phrase must not downgrade a notice titled or typed as `中标公告` / `成交公告` / result announcement into a candidate announcement; award/result signals are checked first.
- SGCC HTML table snapshots may be truncated before the closing `</table>` tag. BidOps treats open-ended `<table>` fragments as parseable evidence so visible table rows are not lost just because the storage preview was capped.
- Pipe-delimited table evidence generated from SGCC rows, for example `分标编号 | 分标名称 | 包号 | 中标状态 | 项目单位 | 中标人`, is trusted lot/package evidence when the first cell is a structured lot number and another nearby cell is a package number. This keeps the AI safety sanitizer from clearing a real `LotNo`.
- For award/result notices, closure detail rows are owned by `OutcomeSupplierRecord` plus `LifecyclePackageLink`. `PackageStaging` and `TenderPackage` remain procurement/package structured data and should not be used as the row count for award supplier details.

## 2026-07-01 BidOps Formal Notice Search Persistence

- The “正式公告库” list stores its last searched filters in browser `localStorage` through the shared `useTableQuery` option. This keeps the behavior consistent with BidOps review and lifecycle pages and avoids a backend/user-preference table for this lightweight operator convenience.
- The reset action intentionally overwrites the saved query with the default formal-notice filters, so a refresh after reset does not resurrect stale search conditions.
- Empty select values are stored as explicit empty strings so the operator's “全部” choice is treated as a real saved condition, not as a missing value that would fall back to the default notice type.

## 2026-06-30 BidOps Lifecycle Project Code Correction

- Explicit labels in award/result notices, such as `采购项目编号：SD26-FWSQ-KJ-JN02`, outrank historical lifecycle `ProjectCode`, lot-number prefixes, and attachment filenames for source-notice rematch. Those labels are closer to the public business identifier than derived evidence.
- Manual project-code edits are exposed as an action on the current “本次闭环公告” context. The edit is stored on `LifecyclePackageLink.ProjectCode`, marks touched links as requiring manual review, and updates every lifecycle detail row under the same award/result RawNotice because project code is announcement-level metadata, not a row-level supplier/package value.
- Once an operator manually edits the project code, that value outranks award/result notice auto-extraction for both list display enrichment and 前置公告 rematch. This prevents a stale extracted code from overriding a reviewed correction.
- Manual project-code edits may clear the existing `ProcurementRawNoticeId`. This is intentional when correcting a wrong code: keeping the old pre-notice link would continue to display the previous mismatch even though future search should use the new project code.
- A six-character lot prefix such as `06FA03` is only fallback evidence when no explicit or manual project/procurement code exists. If a result notice or operator correction provides a full code such as `SD26-FWSQ-KJ-JN02`, the lot prefix must not replace it during lifecycle refresh, list display enrichment, or source-notice rematch.
- Manual project-code audit remarks are treated as durable evidence. If a refresh rebuilds `EvidenceJson`, the service parses the latest `项目编号手动改为 ...` remark and writes it back to `ProjectCode` and `manualProjectCodeOverride` so reviewed corrections survive reparse jobs.
- When normalized `OutcomeSupplierRecord` rows already exist for an award/result RawNotice, lifecycle closure treats those rows as the primary supplier/package evidence. Parsed PDF attachment rows are only appended as standalone closure candidates when they carry strong evidence such as an explicit lot number or amount/package context; weak row fragments from PDF column shifts must not create extra lifecycle links.
- Reviewer-prompt outcome reparse may still merge deterministic fallback rows, but fallback rows with short or generic lot-name fragments are considered less specific duplicates when a same package/supplier row has an explicit lot number. This avoids keeping rows like `务 包 1 ...` beside the full `06FA03-... 中介服务-审计服务 包 1 ...` record.
- Lifecycle award evidence also prunes same-lot/same-package supplier-name fragments after merge. A parsed row such as `山东资德会计师事务所` must not create a second closure row when a more complete same-package row says `山东资德会计师事务所(普通合伙)`.
- Outcome rows whose supplier field is actually a status such as `流标状态`, `流标`, `废标`, or `采购失败` are kept only as public result display rows. They are normalized to `Failed`, must not carry award amounts, must not sync to supplier master data, must not create lifecycle closure links, and cannot be confirmed or selected as final award amount evidence.

## 2026-06-30 BidOps Final Amount Candidate Safety

- Lifecycle `FinalAwardAmount` is treated as the actual winning/deal/quote amount only. Agency/service fees, budgets, ceilings, deposits, unit prices, rates, discounts, and reduction rates remain visible evidence but cannot be adopted directly as final中标/成交金额.
- Batch clearing final amounts is intentionally non-destructive for evidence: it clears the lifecycle link's final amount fields and unselects selected amount candidates, but it does not delete amount candidates or change the lifecycle link's review decision status.
- When a selected amount candidate is unselected, its status is recalculated from its amount type. For example, `agency_fee` returns to `Rejected` because service fee is not a true final award amount.

## 2026-06-30 BidOps Lifecycle Rematch Project Code Guard

- Lifecycle 前置公告重匹配 must treat package labels such as `包` / `包1` as invalid project-code evidence. These values can exist in historical `LifecyclePackageLink.ProjectCode` rows, but they are package context, not 国网 `purOrgCode`.
- Project/procurement codes used for SGCC precise rematch must normalize to ASCII alphanumeric code forms, including six-character prefixes such as `23FEA1` from attachment names or lot numbers. When a stored value is invalid, matching continues to award attachment and outcome evidence instead of stopping early.
- SGCC `index/noteList` precise search payload should mirror the public portal request shape for this path: `index`, `size`, `firstPageMenuId`, `purOrgStatus`, `purOrgCode`, `orgId`, `key`, and `orgName`. Empty `purType` / `noticeType` fields are not needed for this rematch flow.

## 2026-06-29 BidOps Lifecycle Project Code Matching

- Raw notice source identifiers such as `url:*` are ingestion metadata, not business project/procurement codes. Lifecycle closure must not use them as project-code fallback values for source-notice lookup.
- When explicit `ProjectCode` / 采购编号 / 招标编号 is absent but the lot number begins with a 6-character alphanumeric prefix followed by a separator, for example `23FEA1-9012006-0001`, BidOps treats that prefix as the project/batch code for read-time closure matching.
- Award/result attachment file names can also be authoritative project-code evidence when they begin with the same 6-character project/batch code, for example `23FEA1 成交结果公告.pdf`.
- Metadata field names and labels such as `SourceUrl`, `ProjectCode`, `ListPublishTime`, `NoticeId`, `Doctype`, and `MenuId` are invalid project-code candidates even if they satisfy a broad alphanumeric token pattern.
- Already-linked 前置公告 can be manually rematched from the lifecycle closure page. Selecting a locally imported candidate replaces `ProcurementRawNoticeId`; when requested from the matched notice context, the replacement is applied to the same award/result RawNotice rows that still point to the same old 前置 RawNotice and are not confirmed/rejected.
- State Grid source-notice candidate search must use the resolved project/procurement code in `purOrgCode` across the tender and procurement source columns first. When a lot number such as `23FEA1-9012006-0001` is the available evidence, BidOps sends only the `23FEA1` project/batch prefix to `purOrgCode`. Keyword fallback is used only when both source columns return no project-code candidates.
- Source-notice candidates returned by SGCC must still echo the searched code in their list `code` field before they are shown as a rematch candidate. This prevents a default or unrelated SGCC response page from displaying Sichuan or other-province notices under a Jilin `23FEA1` search.
- Lifecycle closure supports 前置公告 reparse by reusing the existing RawNotice attachment extraction and structured-parse pipeline. The closure UI waits for the structured parse child job before refreshing so operators do not see stale package/amount evidence after only attachment processing has completed.

## 2026-06-29 BidOps P1 Amount Candidate Pool

- Amount candidates are now first-class BidOps tenant data in `bidops_amount_candidate`. The pool stores source metadata, evidence snippets, normalized value, type, status, and selection/rejection audit fields, but still does not store file binary content.
- Candidate generation is idempotent by tenant-scoped `SourceHash`. Public review and lifecycle closure may both trigger candidate backfill on read, but repeated opens should only fill missing candidates and preserve manual selected/rejected/type edits.
- Public review detail and lifecycle closure detail share the same candidate pool. Closure rows include candidates linked to the lifecycle row plus candidates from its award/result, candidate, and matched source/前置 RawNotice rows; rejected and unresolved candidates are intentionally included.
- Selecting an amount candidate updates the existing `LifecyclePackageLink.FinalAwardAmount` and `FinalAwardAmountSource` compatibility fields. The MVP enforces one selected final amount per lifecycle link rather than adding a new final-amount table.
- Budget, ceiling, agency fee, deposit, unit price, rate, discount, and reduction values are preserved as evidence but are not recommended as final award amounts. Operators may restore or re-type candidates when public evidence proves the automatic classification was wrong.
- Outcome tables headed `中标服务费（万元）` or `成交服务费（万元）` are normalized as service-fee evidence, for example `1.7000` becomes `17000` CNY. These candidates remain `agency_fee` and rejected by default because the header describes a service fee rather than the final中标/成交金额.
- ZIP-in-ZIP Word/Excel support remains in the existing attachment text extraction path. Inner Word/Excel/PDF/ZIP entries are recursively extracted into the parent attachment text with archive/file headers, so the amount candidate pool can scan those extracted text blocks without introducing child attachment rows in this phase.

## 2026-06-29 BidOps P0 Source Notice And Candidate Consistency

- Lifecycle closure keeps the existing persisted `ProcurementRawNoticeId` compatibility field for now, but product-facing UI/API enrichment treats it as the matched source notice / 前置公告. This avoids a database migration and broad historical-row rewrite while fixing operator wording and search behavior.
- Result notices are classified by rule before source-notice lookup. Public/invited bidding and result/candidate signals prefer `tender_notice` / `bid_invitation`; negotiated, inquiry, single-source, and deal-result signals prefer `procurement_notice` / `procurement_invitation`. A bare `采购编号` is not treated as a non-bidding signal.
- State Grid ECP source-notice search maps `2018032700291334` to the tender/bid-invitation source column and `2018032900295987` to the procurement/procurement-invitation source column. For bidding projects this means 招标公告 / 投标邀请书 are searched and sorted ahead of 采购公告.
- Closure DTOs now carry the same candidate pools operators can inspect on the public review page: outcome supplier/amount rows, procurement detail staging rows, and attachment lists. The closure page may repeat these source-level candidates on each lifecycle row in the current page so no unresolved, unbound, or low-confidence candidate silently disappears.
- Nested ZIP handling remains inside the existing `RawAttachment` text extraction path rather than creating child attachment rows in this P0. Inner Word/Excel/PDF/ZIP files are recursively read into the parent attachment extracted text, with archive/file path headers and `ParseError:` lines for broken or unsafe entries. A future migration can add child file ids, parent/root ids, and archive-depth columns if operators need per-inner-file records.

## 2026-06-29 BidOps Reviewer-Prompt Outcome Reparse Fallback

- Reviewer-prompted outcome supplier reparse remains a replacement of the current stored `OutcomeSupplierRecord` rows for that RawNotice, but persistence no longer trusts only the AI-returned rows.
- The reparse selection now starts with AI rows so reviewer corrections still win for the same package/lot/outcome/rank, then appends deterministic rows that AI did not cover. This prevents a reviewer prompt reparse from reducing a public result notice from 35 rows to 29 rows when deterministic parsing can still identify the missing lines.
- Outcome row de-duplication must keep lot context in the supplier-level key. The same supplier and package number can legitimately appear in multiple lots or service categories, so cross-lot rows are not duplicates.

## 2026-06-28 BidOps Lifecycle Field Enrichment Source Completeness

- Lifecycle field enrichment should remain bounded by prompt budgets, but long source compression must preserve local context around relevant rows. A package row, amount cell, or supplier value can be split across neighboring lines in extracted Word/Excel/PDF text.
- Source snippet selection now uses both fixed procurement/outcome keywords and dynamic lifecycle-link values such as project code, package number, lot name, and supplier name.
- When source text is too long, the prompt includes the document opening plus relevant lines with nearby context lines. This is preferred over sending only isolated keyword lines because reviewers often need table headers and adjacent cells to understand package identity.
- Full unbounded source replay is still avoided to keep AI calls reliable and cost/prompt-size controlled. If evidence remains ambiguous, the AI must return conflicts and require manual review.

## 2026-06-27 BidOps Lifecycle Closure Notice Context Layout

- Lifecycle closure notice context is display-level information for the review center. Award/result notices and procurement/tender notices should be shown once at the top of the current closure, not repeated inside each award-detail row.
- The first implementation is frontend-only and derives the notice context from the current filtered lifecycle-link result. This avoids a backend contract, schema, or migration change while correcting the operator-facing layout.
- If the current filtered result spans multiple award/procurement notices, the UI keeps them out of the detail rows and shows a warning in the top context so operators can filter by `RawNoticeId` for a single closure.
- Procurement-notice search remains available from the top context for missing procurement links. It still uses the existing per-link search/import API because linking is persisted on lifecycle-link records in the current MVP model.

## 2026-06-27 BidOps Lifecycle Field-Level AI Enrichment

- Lifecycle closure now treats missing lot/package/amount context as field-level enrichment rather than one-off parser special cases. The same mechanism can apply to `LotNo`, `LotName`, `PackageNo`, `PackageName`, `SupplierName`, `FinalAwardAmount`, `ProjectCode`, and similar review fields.
- Field source priority is explicit: award/result notice evidence wins first, candidate notice can fill gaps second, and procurement/tender notice evidence can fill remaining gaps. Tender-side budget/max-price/guide-price values are not treated as confirmed award amounts; they are suggestions requiring manual review.
- The first implementation does not add a table or migration. AI enrichment writes a `fieldEnrichment` proposal into `LifecyclePackageLink.EvidenceJson` and may fill currently empty suggestion fields on the lifecycle link. It does not confirm the link or write formal business records.
- Automatic AI enrichment and reviewer-prompt enrichment use the same job pipeline. A blank prompt runs the normal enrichment path; a reviewer prompt uses the existing reviewer-prompt Codex scenario and the current runtime HTTP provider token settings when the provider is DeepSeek or Mimo.
- The UI exposes this as a single-record action in the lifecycle closure center. Operators can run automatic enrichment first, inspect field-level evidence/conflicts, then rerun with a manual prompt when the automatic pass cannot identify a field.

## 2026-06-27 BidOps Lifecycle Supplier Name Hardening

- Supplier-name cleanup must not treat a bare leading Chinese numeral as a rank marker. Names such as `四川...` are valid organization names and must not be shortened to `川...`; rank cleanup now requires an explicit rank suffix or list punctuation.
- Lifecycle reverse closure treats existing `OutcomeSupplierRecord` rows as higher-confidence award evidence when their row evidence text matches the lifecycle evidence text. This lets previously reviewed/extracted outcome rows correct parser fallbacks without mutating Raw or Staging source data.
- Read-time lifecycle link enrichment may correct the displayed supplier name from matching outcome evidence, in addition to lot/package context. Historical link rows remain auditable in storage, while the operator UI shows the more reliable outcome-context value.
- Prefix-truncated supplier names are considered compatible only when the shorter name is almost the full longer name with one or two leading characters missing. This is intentionally conservative so unrelated regional or parent/subsidiary names are not merged by broad substring matching.

## 2026-06-27 BidOps Lifecycle Review Table Sorting And Lot Enrichment

- The lifecycle closure center uses table-header custom sorting instead of a standalone sort dropdown. This matches the background-job table interaction pattern and keeps sorting close to the visible column being ordered.
- Lifecycle link rows may predate the richer outcome/package context and have empty stored `LotNo`, `LotName`, or `PackageName`. The review API enriches DTOs from existing outcome supplier records and review package staging rows at read time instead of mutating historical lifecycle-link audit rows.
- For historical rows where the same supplier and package number appear in multiple lots, enrichment first matches `EvidenceJson.award.evidence.evidenceText` to `OutcomeSupplierRecord.EvidenceText`. This mirrors the review-detail display source and avoids cross-linking reused package numbers.
- When a lifecycle page is filtered by `RawNoticeId`, lot-number/name sorting may use the same read-time enriched display context before pagination for small result sets. This keeps the table order aligned with the lot values operators actually see while avoiding broad cross-notice in-memory sorting.

## 2026-06-27 BidOps Lifecycle Procurement Notice Search

- Lifecycle closure keeps result/award notices as the main entry point, but missing procurement notices can now be searched from the closure center by project/procurement code.
- The SGCC public search uses the existing WCM `index/noteList` endpoint and the portal's `key` parameter. It searches both verified procurement-related public menus: `2018032700291334` for 招标公告及投标邀请书 and `2018032900295987` for 采购公告. This covers cases where the visible portal page supports project-number search but the actual base procurement announcement is under the tender-announcement menu.
- Search is a lightweight WebApi read operation over public list metadata only. Downloading detail content, attachments, and parsing still goes through the existing Worker-backed manual URL import job.
- Lifecycle import accepts only SGCC public `doci-bid` detail URLs for procurement/tender candidates. Existing local RawNotice rows are linked directly to the lifecycle record; otherwise an import job is queued and the closure page can infer the procurement notice after Worker ingestion.

## 2026-06-27 BidOps Review Sorting Defaults

- When operators filter the background-job list to `Succeeded` without choosing an explicit sort, the frontend defaults to `CompletedAt` descending so successful jobs show the latest completed work first.
- The default is applied only as a UI convenience and only while no manual sort is set. Existing backend background-job sorting remains the `SortBy=CompletedAt&SortDescending=true` contract.
- The lifecycle closure center continues to use a single `SortBy` query value. It now supports common review ordering fields including lot number/name, package number, supplier, project code, amount, match score, review-required flag, status, confirmation time, update time, and creation time without adding a new schema or API shape.

## 2026-06-25 BidOps Mimo AI Provider

- Mimo is integrated as another OpenAI-compatible HTTP AI provider behind the existing runtime `ai.provider` switch. Operators can select `Mimo` from the BidOps operations dashboard, and Worker resolves the provider when a job starts.
- Mimo credentials are runtime configuration, not source-controlled settings. The provider supports `BidOps:Mimo:ApiKey` or `MIMO_API_KEY`; local appsettings only carry the non-secret base URL and default model.
- Mimo resolves `BidOps:Mimo:*` before generic `BidOps:Ai:*`, and generic base URL/model/API key values are only applied to Mimo when the configured provider is also Mimo. This prevents existing DeepSeek-compatible defaults from leaking into Mimo requests after a runtime provider switch.
- Mimo calls are paced by provider and endpoint host in the Worker process. The local default is one Mimo request start every 15 seconds and a 180-second backoff after `HTTP 429`, because the current Token Plan credential rate-limited under the earlier multi-job backlog.
- If Mimo returns repeated `HTTP 429`, operators should pause BidOps background tasks through the operations dashboard instead of letting the queue spin through fast failures.
- Xiaomi Token Plan usage must be validated against the active account terms before enabling BidOps background extraction with that credential. The code is configuration-only and does not commit or automatically enable the provided token.

## 2026-06-24 BidOps Award-Driven Reverse Lifecycle Closure

- Award/result notices are the primary entry for lifecycle closure. The first implementation extends the existing `BidOpsReverseLifecycleClosureService` instead of creating a parallel pipeline.
- Phase 1 does not add a new table or migration. Closure suggestions are persisted into the existing `bidops_lifecycle_package_link` table, and detailed amount semantics are stored in `EvidenceJson` as a versionable proposal payload.
- Amount semantics are now explicit in the closure JSON: direct award amount, candidate final quote, inferred discount/reduction/coefficient amount, and unknown are separated. `FinalAwardAmountSource` remains a compact compatibility field, while `PricingDecision` carries source stage, base amount, rate, formula, confidence, evidence, and manual-review requirements.
- Rate-based inferred amounts are always marked for manual review. A bare percentage without discount/reduction/coefficient wording is treated as `Unknown` and is not multiplied by a base amount.
- Package-level base amount inference is conservative. A package guide price is preferred; otherwise only one unambiguous package budget or max price may be used. Conflicting budget/max-price values without a guide price produce `BaseAmountMissing` instead of an inferred成交金额.
- Lifecycle link persistence is idempotent by a tenant-scoped source hash built from award/candidate/tender raw notice evidence plus package and supplier identity. Confirmed links are not overwritten by subsequent automatic suggestions.
- The first UI/API surface remains the existing lifecycle debug controller plus Worker-backed enqueue, persist, confirm, and reject endpoints. A larger review UI can build on the same lifecycle link rows later.
- The operator UI is placed at `/bidops/outcomes` under the results center and backed by a paged search endpoint over `bidops_lifecycle_package_link`. The route replaces the previous result-entry placeholder without introducing a new table or lifecycle workflow boundary.
- The primary lifecycle-analysis entry is the formal result notice context, not a free-form URL form. The notice list/detail pages show `分析闭环` only for result-like notices and enqueue analysis with the linked `RawNoticeId`; `/bidops/outcomes` remains an operations/review center for progress, suggestions, evidence, failure/missing reasons, and manual decisions.
- Real SGCC sample inspection on 2026-06-24 confirmed the provided `doci-win` sample is publicly readable and exposes project code `0711-26OTL04213025`; the provided `doci-bid` sample downloaded a ZIP attachment and produced 28 package rows, 28 amount references, and 49 requirement rows through the existing inspector/extractor path.

## 2026-06-24 BidOps Local AI Worker Concurrency Observation

- Local `BidOpsLocal` Worker AI concurrency was raised one step for observation: `BackgroundTasks:OneTimeJobs:MaxConcurrency=8`, `bidops.ai.structured-parse=3`, and `bidops.outcome.supplier-extract=3`.
- This allows up to six concurrent AI parsing jobs while leaving two Worker slots for non-AI or lighter work. The State Grid ECP crawler remains capped at `1`.
- The change is intentionally below an aggressive Codex CLI fan-out because each Codex task starts a separate process and can hold CPU, memory, and API capacity for several minutes.

## 2026-06-23 BidOps Local Codex Worker Concurrency

- Local BidOps Worker concurrency was increased because Codex CLI parsing is materially slower than the DeepSeek-compatible HTTP provider and the previous per-AI-job cap allowed only one structured parse plus one outcome extraction at a time.
- `BidOpsLocal` now uses `BackgroundTasks:OneTimeJobs:MaxConcurrency=6`, with `bidops.ai.structured-parse` capped at `2` and `bidops.outcome.supplier-extract` capped at `2`. This allows up to four concurrent AI parsing jobs while keeping two slots available for lighter jobs such as attachment processing and recovery work.
- State Grid ECP crawl concurrency remains capped at `1` to keep public-source crawling gentle and checkpoint updates deterministic.
- Additional machines can be used as BidOps AI-only consumers, but they should use Worker job-type inclusion filters instead of consuming every `bidops` task. This prevents AI scale-out nodes from also claiming crawler, attachment, or maintenance jobs.
- Cross-machine Workers must use distinct Snowflake node ids and a shared file/object storage path for BidOps file-backed work. Local disk paths are acceptable only when the extra Worker is constrained to job types that do not require files unavailable on that machine.

## 2026-06-23 Frontend List Query Persistence

- BidOps review-task list and background-job list filters are persisted in browser `localStorage`, not server-side user preferences. This keeps the behavior lightweight and avoids a backend schema/API change for an operator convenience feature.
- Filter values are written to `localStorage` only when the operator explicitly clicks `查询`; typing into a field without searching does not change the restored state. `重置` writes the default state so old filters do not come back after a refresh.
- Background-job list uses separate keys for the global operations page and the BidOps operations page, because the BidOps page forces the `bidops` queue and should not inherit a general operations queue filter.
- Explicit route query parameters override cached values on page load. This preserves deep-link behavior while still restoring the last-used filters when the user refreshes or navigates back without query parameters.

## 2026-06-23 Background Job Completed-Time Sorting

- Background task list completion time uses the existing operator-facing `CompletedAt` alias backed by `BackgroundJobs.CompletedAtUtc`; no schema change or new timestamp column is needed.
- Completion-time sorting is an explicit operations-list option (`SortBy=CompletedAt`). Jobs without a completion time are placed after completed jobs in both ascending and descending modes so pending/running work does not hide completed-history ordering.
- When no explicit sort is requested, the operations list keeps its existing default order: running jobs first, pending jobs second, then newest created jobs.

## 2026-06-23 BidOps Background Job Backlog Parallelization

- BidOps background backlog was inflated by two separate issues: the one-time Worker claimed a batch but executed jobs serially, and automatic attachment/scan enqueue keys included the current minute so recovery/scheduled scans could enqueue the same unfinished work repeatedly.
- `BackgroundTasks:OneTimeJobs:MaxConcurrency` is a generic Worker option and defaults to `1` to preserve existing deployment behavior. `JobTypeConcurrency` can cap specific job types below the global concurrency.
- The Worker maintains active jobs and fills newly available slots while long-running jobs continue. A slow AI task occupies its own slot but no longer blocks quick attachment tasks from being claimed in later polling ticks.
- Local BidOps Worker runs with global concurrency `4`. Attachment processing can run in parallel because each job is scoped to one RawNotice and the downstream structured-parse job is deduplicated by parser version, tenant, RawNotice, and content hash.
- Structured notice parsing and outcome/candidate supplier extraction are capped at concurrency `1` in `BidOpsLocal` because they call external/CLI AI providers and can be expensive or timeout-prone. State Grid crawl jobs are also capped at `1` to keep public-source crawling gentle and checkpoint updates deterministic.
- Automatic attachment-process deduplication uses `TenantId + RawNoticeId + ContentHash`. If public notice content changes and the content hash changes, a new attachment/parse task can be enqueued; unchanged recovery scans no longer create minute-by-minute duplicates.
- Scheduled crawl deduplication uses the current channel/checkpoint progress state instead of wall-clock minutes. A backfill checkpoint can enqueue the next segment only after cursor/checkpoint state advances or is explicitly reset.
- Local backlog repair marked duplicate Pending attachment/scan jobs as `Canceled` while keeping one canonical pending job per RawNotice or crawl state. No Raw/Staging/Formal BidOps business rows were deleted or mutated.

## 2026-06-23 BidOps Task Procurement Number Search

- BidOps uses the existing `ProjectCode` field as the product-facing `采购编号/项目编号`; no new `ProcurementCode` database column is introduced for MVP.
- Review-task search accepts an explicit `ProjectCode` query parameter and resolves it through `NoticeStaging.ProjectCode`, `ProcurementDetailStaging.ProjectCode`, and `OutcomeSupplierRecord.ProjectCode`. This covers procurement announcements, attachment-derived package detail rows, and award/candidate result rows without denormalizing review-task rows.
- Background job search also accepts `ProjectCode`, but it stays within the background-task boundary by searching job `Payload`/`Result` text instead of injecting BidOps tenant repositories into the global operations service.
- New RawNotice-related BidOps jobs carry optional `projectCode` in payload and progress/result JSON where known. Older jobs remain searchable only when the procurement number already appears in their payload/result text.

## 2026-06-23 BidOps Ordinal-Prefixed Outcome Lot Evidence

- Public outcome tables may prefix each row with an ordinal before `LotNo + PackageNo + SupplierName`, for example `1 10FM03-9001006-0111 包 1 江苏科能岩土工程有限公司`.
- BidOps treats that row-leading lot number as explicit evidence only when the source text has a labeled lot/package outcome table signal. This preserves the conservative rule that package identity is not inferred from supplier name or package number alone.
- Historical local repairs may fill missing `LotNo` from such ordinal-prefixed public evidence and refresh derived review-quality issues. If the repaired row still has no compatible procurement package, the old multi-match warning is converted to the normal lifecycle missing-package warning instead of kept as a stale conflict.

## 2026-06-23 BidOps Manual Task Priority

- BidOps background jobs use the existing `BackgroundJobs.Priority` field; no new queue or migration is required.
- Operator-triggered BidOps jobs are enqueued with priority `100`, while automatic/scheduled jobs keep the default priority `0`. The existing Worker ordering by priority then naturally runs manual work before automatic backlog.
- Child jobs spawned from crawl/import/attachment-processing handlers inherit the parent job priority, so manual URL import and manual reparse remain high priority through attachment extraction and structured parsing.
- BidOps-only manual retries promote the retry job to priority `100`; non-BidOps background retries keep their original priority.

## 2026-06-23 BidOps Global Task Pause Switch

- The global BidOps task pause switch is stored as a tenant-scoped runtime setting in `bidops_runtime_setting` with key `runtime.task-pause`. This reuses the existing runtime settings table and avoids a new migration for a single operational flag.
- Pause behavior is conservative: new operator-triggered BidOps background task requests are rejected, recurring BidOps tasks skip enqueue for paused tenants, and queued BidOps jobs are deferred before handler execution without consuming retry attempts.
- The switch is not a force-kill. Already running jobs continue to rely on the existing cooperative cancellation/termination flow so handlers can stop at safe I/O and persistence boundaries.

## 2026-06-23 BidOps Runtime Codex CLI Settings

- Codex CLI model and reasoning effort are tenant-level runtime settings in `bidops_runtime_setting` under `ai.codex-cli.model` and `ai.codex-cli.reasoning-effort`.
- Worker-owned AI extraction reads the effective provider/model/reasoning settings before each Codex CLI request, so operations-dashboard changes apply to subsequent jobs without a Worker restart.
- The default Codex CLI reasoning effort is `low` instead of `xhigh`.
- Codex CLI settings are scenario-scoped for ordinary extraction, complex-source extraction, manual reparse, and reviewer-prompt extraction. Ordinary extraction defaults to `low`, complex-source and manual reparse default to `medium`, and reviewer-prompt extraction defaults to `xhigh`.
- Complex-source selection is currently a conservative Worker-side heuristic: extraction requests with at least 3 attachments or at least 60,000 characters of notice plus attachment text use the complex scenario. The threshold is intentionally not a new database setting in MVP; operators can tune the model and reasoning effort for that scenario from the operations dashboard.

## 2026-06-22 BidOps Outcome Lot Identity Reparse Quality

- Outcome supplier rows use `LotNo + LotName + PackageNo + SupplierName` as their review-time identity evidence. A repeated package number such as `包1` is not enough to infer a unique package when many lots reuse it.
- If an AI/deterministic outcome extract omits `LotNo` but its row evidence starts with a public lot number followed by the package number, persistence may fill `LotNo` from that evidence when the source text has a lot-number table/header signal.
- Review-detail UI must not display package metadata inferred from an ambiguous package number. Package fallback display is allowed only when the match is unique or when lot context is present.
- Review quality issue refreshes use explicit tenant-scoped repository operations so Worker reparses overwrite stale review issues for the same task instead of leaving old issue rows attached to current results.

## 2026-06-22 BidOps Review Quality Scoring Foundation

- Review quality automation is implemented as an审核辅助 layer, not automatic approval. Quality scores and recommendations can mark a task as a batch-confirm candidate, but a permitted human action is still required before Formal data is written.
- Quality summary fields are persisted on `ReviewTask` for efficient queue filtering. Detailed evidence stays in `bidops_review_quality_issue` so each warning can point back to the Raw notice, NoticeStaging, PackageStaging, optional ProcurementDetailStaging row, and original evidence JSON.
- Procurement quality rules are conservative. Unitless money remains yuan by default; high-risk amount issues are created only when original row/header evidence explicitly shows `万元/万` without yuan normalization, or when rate/percent/weight headers are persisted into money fields.
- Existing Raw/Staging/Formal boundaries remain unchanged. The quality service updates only staging-side review metadata and issue rows after structured parsing or reparse, and does not mutate approved Formal notice/package data.
- Review queue sorting uses persisted `ReviewTask` quality summary fields instead of joining issue rows for every page. Issue-type filtering may query the issue table first, then constrain review task IDs through the repository query surface.

## 2026-06-19 BidOps Procurement Reviewer Prompt Reparse

- Procurement-announcement reviewer prompts use the existing raw notice reparse flow instead of the outcome-supplier reparse endpoint. This keeps announcement/package/requirement extraction in the structured parse pipeline and keeps outcome/candidate supplier extraction separate.
- Reviewer prompts are optional for raw notice reparse. Empty prompt means normal deterministic/AI structured reparse; non-empty prompt is carried only in background job payloads and injected into the structured DeepSeek prompt.
- The prompt can override deterministic reference output only when supported by public notice text, HTML, or extracted attachment text. AI results still go to staging/review and do not bypass human approval.

## 2026-06-18 BidOps Procurement Amount Header Units

- Procurement announcement amount fields continue to be stored as CNY yuan.
- DeepSeek prompts require yuan-normalized `budgetAmount` and `maxPrice` even when the source table uses varied headers such as `采购金额（万元）`, `分项估算金额（万元）`, `包估算金额（万元）`, `行报价最高限价（含税/万元）`, or similar aliases.
- Backend parsing still performs deterministic reconciliation when AI returns a bare numeric value that appears to be the original `万元` cell value. If deterministic table parsing sees the same package amount as 10,000 times the AI value, the deterministic yuan value wins.
- Headers for percentage limits, tax rates, weights, scores, calculation methods, and price parameters are not treated as money columns. Mixed headers such as `元或折扣比例` are treated as yuan only when the cell itself is not a percent/rate/discount value.

## 2026-06-18 BidOps Procurement Detail And Lifecycle Link Model

- Procurement attachment rows are modeled as first-class procurement details instead of being flattened entirely into `TenderPackage`. This keeps `TenderPackage` focused on package identity while preserving messy attachment-row evidence for analysis.
- `ProcurementDetailStaging` stores AI/rule-extracted row facts before review. `ProcurementDetail` stores the approved formal row facts. Both keep normalized fields and original header/row JSON for traceability and future field mapping.
- The first schema version includes common SGCC service/procurement fields used for package identity, money, scope, schedule, qualification, scoring, quote, and contract analysis. Rare or source-specific columns stay in JSON until they justify promoted columns.
- Long source text fields such as procurement content, scope, project overview, qualification, performance, personnel, and other requirements use MySQL `text` instead of very wide `varchar` columns to avoid row-size pressure from messy attachment tables.
- `LifecyclePackageLink` is the durable bridge for procurement detail, formal package, candidate result, and award result records. It stores match score, reasons, missing fields, evidence JSON, and manual confirmation state.
- Lifecycle links are tenant-scoped and idempotent by `TenantId + SourceHash`; matching services must not create cross-tenant links.

## 2026-06-18 BidOps Outcome Amount Unit Semantics

- `OutcomeSupplierRecord.AwardAmount` continues to store public final quote, award, and transaction amounts in CNY yuan.
- Amounts are multiplied by 10,000 only when the source cell, same table header, or immediate surrounding context explicitly says `万元` or `万`.
- Unitless amount columns default to yuan. A header such as `投标报价` or `成交金额` without an explicit `万元` marker is not treated as ten-thousand-yuan.
- Percentages, discount rates, fees expressed as rates, and score-like values remain excluded from `AwardAmount` and should stay null when they are the only numeric evidence.
- BidOps review and supplier-analysis UIs display/edit outcome amounts as yuan so the operator is not forced into an implied `万元` assumption.

## 2026-06-16 DeepSeek Response Visibility In Background Job Details

- BidOps DeepSeek/OpenAI-compatible parsing jobs persist AI response diagnostics into the background job result so operators can inspect the exact provider response from the job detail page after a reparse.
- The stored diagnostics include provider, model, endpoint host/path, HTTP status, elapsed time, response/assistant character counts, finish reason, the raw response body, and extracted assistant content. They intentionally do not store request bodies, authorization headers, or API keys.
- `BackgroundJobs.Result` is widened to `mediumtext` and AI parsing handlers explicitly request a larger result storage cap. Other background jobs keep the default short result cap so normal lists and history remain lightweight.
- Operations detail keeps list previews short but allows BidOps structured parse and outcome supplier extraction details to return up to the AI diagnostics cap. The frontend shows a dedicated `AI 返回` tab when provider diagnostics are present.
- Historical jobs that completed before this change cannot show DeepSeek raw content from the job detail because only Worker logs held that data. Rerunning/reparsing the Raw notice creates a new job with persisted diagnostics.

## 2026-06-15 Cooperative Background Job Termination

- Background job cancellation now supports Running jobs through cooperative termination, not process/thread killing. The operations API records a cancellation request on the job, and `BackgroundJobWorker` cancels the `CancellationToken` passed to the active handler after it observes the request.
- Running jobs remain `Running` while termination is pending and are exposed with `IsCancellationRequested=true`. The frontend displays this as `终止中` and disables duplicate termination clicks.
- When the handler exits because of the cancellation token, Worker marks the job `Canceled`, clears the lock fields, records completion time with server local time, and stores a short operator-canceled result. Pending, Failed, and Dead jobs are still canceled immediately without handler execution.
- New cancellation fields use local operator time semantics and avoid a new `Utc` suffix: `CancellationRequestedAt`, `CancellationRequestedBy`, and `CancellationReason`.
- Handlers are expected to pass their `CancellationToken` into HTTP, file, database, delay, and AI calls. Work that ignores the token cannot be force-stopped safely and will finish according to its own control flow.

## 2026-06-15 Local Background Job Operator Times

- Background job operator-facing task types are exposed with Chinese display names (`JobTypeName` / `CurrentJobTypeName`) while keeping the original `JobType` code for routing, filtering, idempotency, and debugging.
- `BackgroundJobs` and `BackgroundWorkerHeartbeat` lifecycle writes now use the server's local time (`DateTime.Now`) so task creation, availability, start, lock, completion, retry, and heartbeat times line up with Atlas audit fields and local operator expectations.
- Existing database column names such as `AvailableAtUtc`, `StartedAtUtc`, `CompletedAtUtc`, `NextAttemptAtUtc`, `StartedAtUtc`, and `LastSeenAtUtc` are retained for migration/index compatibility. Operations DTOs expose local-time aliases without the `Utc` suffix (`AvailableAt`, `StartedAt`, `CompletedAt`, `NextAttemptAt`, `LastSeenAt`), and the frontend displays those aliases.
- UTC remains appropriate for security/session expiry, authorization entitlement windows, distributed messaging outbox/inbox timestamps, and other cross-region protocol semantics outside the background job operations surface.

## 2026-06-15 Local Audit Timestamps

- Atlas infrastructure-managed audit fields (`CreatedAt`, `UpdatedAt`, and soft-delete `DeletedAt`) use the server's local time (`DateTime.Now`) rather than UTC. This matches the local BidOps operator workflow, where database rows and UI timestamps should read as local time without mental conversion from Greenwich time.
- Bulk insert/update helpers use the same local audit time source as the EF save interceptor so batch-created rows do not drift from normal repository writes.
- Fields whose names explicitly end in `Utc` generally keep UTC semantics when they drive security, messaging, cross-region coordination, expiration, or entitlement behavior. Background job operations are the local-operator exception documented in `2026-06-15 Local Background Job Operator Times`.
- The frontend shared date formatter treats timezone-less datetime strings as browser-local time. Values with explicit `Z` or numeric offsets still rely on normal parsing and render in the user's local timezone.

## 2026-06-15 BidOps Outcome Supplier Announcement Order

- Public outcome/candidate supplier rows must follow the order they appear in the announcement. When the source has no explicit sortable field, DeepSeek's returned array order or the deterministic parser's scan order is treated as the announcement order.
- `OutcomeSupplierRecord.ExtractionOrder` is the durable order field. Review-detail APIs and frontend award/candidate tables must use this order and must not sort by package number, lot number, supplier name, amount, or rank unless the user explicitly requests a separate analysis view.
- Reviewer-prompted DeepSeek reparses are replacement operations and persist DeepSeek rows in returned order. Automatic extraction uses AI rows as the primary ordered source, then appends deterministic-only fallback rows to avoid losing rule-based recall.
- Existing outcome supplier lead rows created before `ExtractionOrder` are deleted during the v0.2.12 migration instead of being approximate-backfilled. Those rows did not store a reliable announcement position, so reviewers should regenerate them through manual import or Raw notice reparse.
- Manual review additions append after the current maximum `ExtractionOrder`; editing an existing row does not change its order.

## 2026-06-15 Frontend DateTime Timezone Formatting

- Earlier in the day, frontend datetime display treated backend datetime strings without an explicit timezone as UTC when they included both date and time.
- This decision was superseded by `2026-06-15 Local Audit Timestamps`: audit timestamps are stored as local time, so timezone-less values are now parsed as local time by the shared frontend formatter. Values with explicit `Z` or numeric offsets still keep normal parser semantics.

## 2026-06-15 BidOps AI HTTP Timeout

- BidOps DeepSeek/OpenAI-compatible structured notice extraction and outcome supplier extraction use a 30-minute typed `HttpClient.Timeout` because large SGCC announcement bundles and reviewer-prompted reparses can legitimately exceed the default 120-second HTTP timeout.
- The longer timeout is scoped to AI parsing clients only. State Grid crawling and attachment download/extraction keep shorter dedicated HTTP timeouts so public-source crawling remains bounded and does not inherit long AI provider waits.
- Worker one-time job processing timeout is already configured at 1,800 seconds in the default Worker settings, matching the 30-minute AI HTTP upper bound for local parsing runs.

## 2026-06-15 BidOps Local State Grid Manual Import Reset

- Local cleanup for the State Grid MVP source deletes source-scoped runtime data and referenced local files, but keeps `bidops_crawl_source` and `bidops_crawl_channel` records because manual URL import validates enabled source/channel configuration.
- Local manual stabilization should rely on `BidOps:ScheduledScan:Enabled=false` to prevent unattended 4-channel crawling while keeping Worker one-time jobs available for one-by-one manual imports.
- During the local manual-import stabilization phase, `BidOpsLocal` opportunity and supplier maintenance recurring tasks are disabled as well. They are not crawl jobs, but they create background-job noise on startup and are not needed while validating one notice at a time.
- Supplier master data may be deleted only when it is directly traceable to the reset Raw outcome notices through `CreatedFromRawNoticeId` or `LastOutcomeRawNoticeId`; unrelated supplier/buyer master data must be preserved.

## 2026-06-15 BidOps Reviewer-Prompted DeepSeek Reparse Gate

- Reviewer-prompted outcome/candidate DeepSeek reparse is an explicit human request and should attempt the AI provider even when the automatic result/candidate notice detector does not match the Raw notice text.
- Unattended structured parsing remains conservative: without a reviewer prompt, outcome supplier extraction still skips non-result/non-candidate notices to avoid creating supplier leads from procurement announcements.
- When a reviewer prompt forces extraction past the automatic detector, Worker logs an explicit information entry before the DeepSeek request logs, so operators can distinguish intentional human-forced reparse from normal automatic extraction.
- AI clients now log configuration diagnostics when HTTP settings are unavailable. The diagnostics include enabled/use/provider/model/endpoint/has-key booleans and key source name, but never log API key values.
- DeepSeek request/response diagnostic JSON is serialized for logs with relaxed JSON escaping so Chinese prompt text, source evidence, and model responses stay readable in Worker logs instead of being written as `\uXXXX` sequences.
- DeepSeek responses with empty `message.content` are treated as provider/model output exhaustion or malformed output, not as parser crashes. Worker logs `finish_reason` and falls back to deterministic extraction or empty outcome leads without throwing `JsonReaderException`.
- BidOps local DeepSeek extraction uses `deepseek-v4-pro` instead of the lower-latency flash model so reviewer-triggered reparses prefer the higher-quality model.
- DeepSeek prompts are JSON-only contracts: the model is explicitly told that the first output character must be `{`, the last must be `}`, and reasoning traces, analysis, Markdown, code fences, prefixes, and suffixes are invalid.
- `BidOps:Ai:MaxOutputTokens` is optional. If it is unset or non-positive, BidOps omits `max_tokens` from the OpenAI-compatible request and lets the provider/model apply its own output policy; operators can still set a positive value when a run explicitly needs a cap.
- The local Worker restart script resolves `DEEPSEEK_API_KEY` inside the launched Worker process from process, User, then Machine environment variables. This keeps the key out of tracked `appsettings` files while making User-scope key rotation effective after a Worker restart.
- Local Worker shutdown should match `Atlas.Worker.exe` by process name even when WMI does not expose a command line, and may fall back to WMI termination when `Stop-Process` is denied for a stale local process tree.

## 2026-06-15 Worker Timestamped Logging

- `Atlas.Worker` now registers the existing Atlas Serilog logging pipeline instead of relying on the default .NET console logger. Default console output is explicitly cleared so redirected Worker logs do not fall back to timestampless default formatting.
- Worker application logs use dedicated file paths (`atlas-worker-.log`, `atlas-worker-errors-.log`, and `worker-audit-.log`) to avoid mixing long-running background job diagnostics with WebApi logs.
- The Atlas console sink uses a full local timestamp (`yyyy-MM-dd HH:mm:ss.fff zzz`) because local Worker restart scripts redirect console output to `var/logs/worker-local.log`.
- The Serilog `Environment` enrichment checks `ASPNETCORE_ENVIRONMENT` first and then `DOTNET_ENVIRONMENT`, matching Worker hosts that are commonly configured with `DOTNET_ENVIRONMENT`.

## 2026-06-15 BidOps DeepSeek Chinese Prompts And Request Logging

- DeepSeek/OpenAI-compatible prompts use Chinese natural-language instructions because the source notices, reviewer correction text, and expected reviewer workflow are Chinese. JSON field names and enum values remain English to preserve the existing internal parsing contract.
- AI prompts provide the stored announcement body HTML plus attachment URLs/metadata/extracted text as source material, and do not include the public notice detail URL as extraction input. The notice URL remains database/job traceability metadata instead of model context.
- AI request logs record provider, model, endpoint host/path, notice type, prompt length, attachment count, status code, elapsed time, response length, parsed row/package counts, the full JSON request body sent to DeepSeek/OpenAI-compatible chat completions, and the full raw response body returned by DeepSeek.
- Authorization headers and API keys are never logged. Full prompt/source/response logging is intentionally Worker-side operational diagnostics for parser tuning and should be handled as potentially large public-source audit data.

## 2026-06-15 BidOps Review Outcome Editing And DeepSeek Replacement

- Reviewer-prompted DeepSeek outcome reparse is treated as a replacement operation. The Worker still deletes existing `OutcomeSupplierRecord` rows for the Raw notice first, but when a reviewer prompt is present it persists only DeepSeek-returned rows instead of merging deterministic rows back in.
- Automatic structured parsing keeps the previous deterministic-plus-AI enrichment behavior, because unattended parsing should preserve rule-based recall when no human correction prompt exists.
- Review-detail manual editing is scoped to public outcome/candidate lead rows for unapproved review tasks. Editing does not write formal Notice/Package/Requirement facts and does not create contacts, capabilities, private relationships, or pursuit decisions.
- Edited outcome rows are treated as reviewer-confirmed staging/lead data with `ExtractionConfidence = 1`. If the supplier or buyer name changes, weak existing organization links are cleared and approval-time sync creates or relinks buyer/supplier master records idempotently.
- Background job payload/result display should format JSON before rendering so Chinese text from DeepSeek prompts/results is shown as readable Chinese instead of raw unicode escape sequences.

## 2026-06-14 BidOps Local Worker DeepSeek Configuration

- DeepSeek credentials remain Worker runtime configuration, not background job payload. Jobs record the Raw notice id, review prompt, and source context; the Worker resolves `BidOps:Ai:*` and `DEEPSEEK_API_KEY` when the job executes.
- `BidOpsLocal` enables BidOps AI locally and intentionally does not define an empty `BidOps:Ai:ApiKey`, so local runs can inherit a base config value or use the safer `DEEPSEEK_API_KEY` environment variable.
- Local Worker restarts use a dedicated script that sets `DOTNET_ENVIRONMENT=BidOpsLocal` and `ASPNETCORE_ENVIRONMENT=BidOpsLocal` before launching `Atlas.Worker`. This avoids accidentally loading the default RabbitMQ-enabled Worker configuration during BidOps local parsing tests.

## 2026-06-14 BidOps DeepSeek Job Result Drilldown

- DeepSeek/outcome reparse jobs continue to persist parsed rows through Worker-owned services before the UI displays them. The review page and job detail page read the current database state instead of treating task result text as the source of truth.
- Outcome reparse rebuilds only the current Raw notice's `OutcomeSupplierRecord` rows and syncs conservative buyer/supplier master-data shells. Structured parse rebuilds staging review data. Formal Notice/Package/Requirement import still requires human approval.
- Background job `Result` now stores a compact JSON summary for structured parse and outcome supplier extraction jobs so operators can see RawNoticeId, review task id, saved row counts, and buyer/supplier sync counts without exposing full prompt input or large source documents in the global job table.
- Frontend extraction of RawNoticeId from job payload/result uses raw string matching before JSON parsing because Atlas snowflake ids exceed JavaScript's safe integer range.

## 2026-06-14 BidOps Review Outcome AI Adjustment

- Review-page DeepSeek adjustment is scoped to public outcome/candidate detail rows for the current Raw notice. It enqueues `bidops.outcome.supplier-extract` with the reviewer prompt instead of running AI work in WebApi.
- The job deletes and rebuilds only that Raw notice's `OutcomeSupplierRecord` rows. Formal Notice/Package/Requirement import and approved organization association still require human approval.
- Approved or already-formal Raw notices are protected from this AI reparse endpoint in MVP, matching the existing Raw notice reparse boundary.
- Amounts remain stored as CNY yuan in `OutcomeSupplierRecord.AwardAmount`. The earlier review candidate list displayed `最终报价` in `万元`, but that display/edit assumption is superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`.
- Reviewer prompts are saved in the background job payload for traceability. They should describe public extraction corrections, not private supplier intelligence or non-public influence.
- DeepSeek receives the stored public announcement HTML snapshot plus extracted attachment text and attachment metadata. The prompt tells it which field set matters for the detected notice type so HTML tables, PDF/Office/Excel/ZIP attachment text, and body-level procurement numbers can all be used as evidence.

## 2026-06-14 BidOps Review Approval Organization Linking

- Review detail now shows the parsed buyer and public outcome supplier rows before approval, so reviewers can see whether approval will create or link master-data organizations.
- If a historical or failed parse review task has no persisted outcome rows yet, review detail builds a read-only preview from the stored Raw notice text/HTML and extracted attachment text. This preview is not persisted; Worker extraction and approval-time fallback remain the write paths.
- Approval remains the human gate for reviewed notice import and review-scoped organization association. If public outcome extraction has already linked or created a master-data shell, approval refreshes it and records this notice's association idempotently.
- Public result/candidate supplier extraction is treated as a resilient side path of structured parsing. If generic tender/procurement staging parsing fails on an outcome announcement, the Worker still attempts outcome supplier extraction so public award rows are not lost.
- Review approval also checks whether the Raw notice has outcome supplier rows; when missing, it runs extraction before entering the approval transaction so existing review tasks can still create/link suppliers from the source announcement.
- Buyer procurement facts are stored in `bidops_buyer_procurement_record` as source-backed purchase history linked to the approved notice and Raw notice. They do not create opportunities, pursuits, contacts, or private relationship facts.
- Supplier master records keep `CreatedFrom*` fields for the announcement that first created the supplier shell, and `LastOutcome*` fields for the latest related public outcome announcement. Existing suppliers are updated and associated instead of duplicated.
- The approval sync uses the original Raw notice detail URL as the source URL for buyer procurement and supplier outcome traceability.

## 2026-06-14 BidOps Reverse Closure State Grid Award HTML Tables

- State Grid WCM `CONT` HTML tables are parsed deterministically inside the evidence table parser instead of adding a browser/rendering dependency.
- State Grid WCM Raw HTML snapshots preserve the original `CONT` HTML when available, so Word-style tables such as `p.MsoNormal`/`MsoNormalTable` remain reviewable and re-extractable instead of being encoded into a synthetic `<pre>`.
- Award table parsing treats procurement/project code as announcement context and allows regex fallback from the surrounding正文. Lot number/name fallback is limited to the text immediately before the table so multi-lot announcements do not inherit the first global lot accidentally.
- `forceRefresh` manual import re-fetches the public source through Worker, refreshes stored Raw text/HTML even when the content hash is unchanged, forces attachment text extraction, and then reruns structured parsing. Approved Raw notices are not force-refreshed in MVP.
- Reverse closure debug DTOs now expose award `ProjectUnit` and `LotName`, plus closure-level `ProjectUnit` and `LotName`, because State Grid award notices commonly provide `项目单位` and `分标名称` directly in the result table.
- Reverse lifecycle linking treats repeated package numbers as lot-scoped when lot numbers are available. A candidate/tender row with `包1` should not link to an award `包1` from a different `分标编号`.

## 2026-06-13 BidOps Reverse Lifecycle Closure Debug

- Package lifecycle reverse closure is implemented as DTO/debug output first. It does not add lifecycle persistence tables, does not write SupplierCapability, and does not promote public outcome/candidate clues into confirmed business facts.
- The debug API does not crawl State Grid or download attachments in the WebApi request thread. If the award URL is not already a RawNotice, it enqueues the existing manual import Worker job and returns an explicit warning/job id.
- Existing State Grid `doci-win` support is reused for award-result import: detail API `index/getNoticeWin`, attachment list API `index/getWinFile`, and download API `downLoadWinFile`.
- Award amount precedence is conservative: use AwardNotice amount when present; otherwise use the matched rank-1 or unique matched candidate final quote; otherwise mark `award amount missing`.
- Candidate and tender reverse matching uses project code exact match first, then project-name similarity, normalized package number, supplier-name equality, rank, and tender/procurement title/type hints. All suggested closures still require manual review in this phase when confidence is below 0.90 or any field is missing.
- Development derived-data cleanup is exposed only as `tools/Atlas.LocalSetup reset-bidops-derived-data`. It is dry-run by default, requires `--confirm` to delete, and protects RawNotice, RawAttachment, CrawlSource, CrawlChannel, CrawlRunLog, attachment files, and supplier master data.

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

## 2026-06-14 BidOps Crawl Adapter And Organization Master Data

- Crawl source-specific behavior is represented behind `IBidOpsCrawlAdapter`. The first adapter, `StateGridEcpCrawlAdapter`, participates in SGCC source/detail URL validation and declares public SGCC ECP support for inline HTML tables and public attachments including PDF, Office, Excel, and ZIP/RAR metadata; actual crawling, download, and text extraction remain Worker-side jobs.
- Public result extraction now combines generic result-text parsing with the evidence/table parser used by reverse lifecycle closure, so HTML中标公告 tables can populate project unit, lot number/name, package number/name, awarded supplier, and amount fields.
- Public outcome/candidate notices conservatively upsert `bidops_buyer` and `bidops_supplier` master records by normalized organization name. Existing records are linked and lightly refreshed instead of duplicated.
- Auto-created supplier records are master-data shells only. The pipeline does not create contacts, capabilities, qualifications, private relationships, pursuit decisions, or any non-public influence data from public award notices.
- `OutcomeSupplierRecord.BuyerId` is a weak traceability link to the buyer master record. AI/parser results still originate from Raw/attachment text and remain separate from formal reviewed tender business facts.

## 2026-06-14 BidOps DeepSeek Outcome Extraction

- DeepSeek is integrated through the existing optional `BidOps:Ai:*` switch as an OpenAI-compatible HTTP provider. It is disabled by default, uses config or `DEEPSEEK_API_KEY` for credentials, and defaults to `https://api.deepseek.com/chat/completions` with model `deepseek-v4-pro`.
- Outcome supplier extraction remains Worker-owned and staging/lead-only. Deterministic parsers run first; DeepSeek may enrich or correct public result rows, but the persistence service still deletes/rebuilds only that RawNotice's `OutcomeSupplierRecord` rows and then syncs conservative buyer/supplier master data.
- Re-extraction uses the existing RawNotice `重解析` path. It force-runs attachment text extraction, then structured parsing, then outcome supplier extraction; approved/formal Raw notices remain protected from mutation in MVP.
- `采购代理服务费` is stored separately as `OutcomeSupplierRecord.ProcurementAgencyServiceFeeAmount`. It must not be folded into `AwardAmount`, because service fee is a public transaction-administration clue rather than the supplier award amount.

## 2026-06-14 BidOps Review Detail Structured Modules

- The review detail page infers a display-only notice kind from staged notice type, title, and Raw notice text so reviewers see different structured modules for `中标/成交结果公告`, `推荐中标候选人公示`, and `采购公告`.
- This inference does not create new formal business facts by itself. It only chooses which existing staging/lead rows to display before the existing human review approval flow.
- Candidate notices show candidate leads and a separate package-detail module even when candidate lead extraction is empty, because reviewers still need to inspect the parsed package and requirement staging data before deciding whether to approve, ignore, or reparse.

## 2026-06-14 BidOps Wrapped PDF Outcome Table Extraction

- Wrapped SGCC PDF result tables are parsed deterministically before AI enrichment, because common public PDF extraction output breaks table cells across lines in a repeatable way and should not depend on DeepSeek availability.
- Candidate/result notice kind is decided before routing evidence parsers. Candidate announcements remain `Candidate` records even when column labels contain `推荐中标人`; only result/award announcements are allowed to create `Awarded` records from award evidence parsing.
- Monetary cells are normalized to CNY yuan only when the surrounding table/header makes the unit explicit, such as `万元人民币`. Discount rates, percentages, and score-like values are preserved in evidence but not stored as `AwardAmount`.
- Existing Raw notices are not mutated automatically by code deployment. Review modules receive corrected candidate/winning detail rows after the existing Worker-owned RawNotice `重解析` flow rebuilds extracted attachment text, staging rows, and outcome supplier records.

## 2026-06-14 BidOps Notice List Filters And Updated Time

- `最后更新时间` uses Atlas entity `UpdatedAt` with `CreatedAt` as the UI fallback, so existing rows display a stable timestamp without a tenant migration.
- Review-pool公告类型 filtering is based on `NoticeStaging.NoticeType`, matching the source used to enrich the review-list project, buyer, region, deadline, and confidence columns.
- Formal notice search now has a dedicated query object instead of reusing the generic paged query, so notice-type filters are accepted by the API contract rather than ignored by the backend.

## 2026-06-17 BidOps SGCC Procurement Attachment Preview

- Public SGCC procurement attachment extraction now treats Excel workbooks as table-shaped evidence instead of loose tab text. OpenXML `.xlsx` and legacy `.xls` tables are emitted as Markdown tables so existing deterministic parsers and reviewer previews can consume the same artifact.
- Legacy `.xls` support uses `ExcelDataReader` plus .NET 8 code-page encodings. This avoids Office COM automation in Worker/runtime code and keeps extraction server-safe.
- ZIP filename decoding uses the default decoder first and falls back to GB18030 only when entry names look garbled. This supports older SGCC/Windows ZIP files without corrupting normal UTF-8 Chinese filenames.
- Procurement attachment parsing preserves table order, parses all recognized scope tables instead of only the first one, derives `包1/包2` from `包名称` when package numbers are embedded, and stores explicit `最高限价/预算金额` values only when the public attachment provides a money amount. Percentages, rates, and guide-price ratios are intentionally not converted to money.
- `tools/Atlas.LocalSetup inspect-bidops-sgcc-notices` is a local diagnostic command only. It fetches public SGCC detail URLs and attachments, prints collectable metadata/package summaries as unescaped Chinese JSON, and does not write tenant or global data.

## 2026-06-17 BidOps Raw Notice Business Identity

- Raw notice deduplication now uses a business identity before source-local URL identity. New Raw notices store `SourceNoticeId=code:<normalized procurement/project code>` when a public notice exposes `采购编号/项目编号/ProjectCode`; if no code is available, they store `SourceNoticeId=url:<DetailUrlHash>`.
- The tenant database enforces `TenantId + NoticeType + SourceNoticeId` as a unique key. This prevents the same notice type and procurement code from being inserted twice when one path is manual URL import and another path is automatic crawling.
- URL dedupe now ignores `SourceId` at the application lookup layer. This keeps same-URL manual and automatic imports idempotent even when the procurement code cannot be parsed.
- Existing historical Raw notices are not deleted by the migration. Legacy `SourceNoticeId` values are upgraded to URL-based identities; if historical duplicates already exist, one canonical URL identity is kept and the rest receive a `:legacy:<Id>` suffix so the unique index can be created without destructive cleanup.

## 2026-06-18 BidOps Outcome Project Name Evidence

- Outcome supplier `ProjectName` is persisted only when the public source or row evidence has an explicit `项目名称/工程名称/采购项目名称/招标项目名称/子项目名称` signal. Announcement titles, batch names, lot names, package names, attachment names, and unlabeled row values are not promoted to `ProjectName`.
- PDF extracted text may split one table cell across lines. When the source table header explicitly contains `项目名称` and DeepSeek returns row-level evidence containing the reconstructed project-name value, that row-level `ProjectName` is considered supported even if the full value is not contiguous in one raw extracted line.
- If DeepSeek puts a value from a source table's explicit `项目名称` column into `PackageName`, persistence sanitization may move that value back to `ProjectName` and clear `PackageName`. This preserves source-column semantics while avoiding a second AI call.
- Outcome supplier `PackageName` must stay distinct from `ProjectName`. Matched package metadata and review/query previews cannot use `ProjectName` or `LotName` as a package-name fallback for result rows.
- Review UI outcome rows display only row-level `ProjectName`; they no longer fall back to the notice/task announcement title, so missing or cleared project names remain visible for reviewer correction.

## 2026-06-22 BidOps Review Automation Guardrails

- Low-risk bulk approval is an operator-triggered review action, not automatic import. Each task still runs through the existing single-task approval flow so Formal writes, organization sync, and review audit fields stay consistent.
- Review quality backfill is Worker-owned and only updates review quality summaries plus quality issue rows. It does not mutate Formal notice/package data and respects paused crawl sources by default.
- Correction samples are used as operational evidence for rule and prompt improvement. MVP does not automatically rewrite parsing rules from samples, because rule changes still need tests and human review.
- Batch AI reviewer prompts are stored as correction samples, but request headers, cookies, API keys, and authorization tokens are not captured.

## 2026-06-22 BidOps Crawl Progress And Backfill

- Crawl progress is stored per tenant/channel/mode in `bidops_crawl_checkpoint`; each Worker execution segment is stored in `bidops_crawl_run`.
- `CrawlChannel.Enabled` remains the MVP switch for opening or closing a specific notice category such as `AwardAnnouncement` or `ProcurementAnnouncement`.
- StateGrid ECP list scanning now supports page-based incremental and backfill modes. Incremental mode returns to page 1 after each run; backfill mode advances `NextCursor` until the configured date range or remote list ends.
- Crawl channels can choose `Interval` or `Daily` scheduling. `DailyScanTime` is stored as `HH:mm` and evaluated with the Worker server's local clock; interval channels use `CrawlChannel.ScanIntervalMinutes` when set, otherwise they fall back to the source interval.
- Previously collected notices are skipped by URL identity before fetching details when possible. If a detail is still fetched and deduplication finds existing unchanged RawNotice, the ingestion result is counted as duplicate and attachment processing is not re-enqueued.
- Failure alerting is implemented as operations-surface status and alert fields in MVP, not as external notification delivery. Operators can pause, resume, continue, or reset backfill cursors from the crawl operations UI.

## 2026-06-22 BidOps Codex CLI AI Provider

- BidOps uses Codex CLI as the default AI extraction provider when `BidOps:Ai:Provider` is not configured. DeepSeek/OpenAI-compatible HTTP extraction remains available by switching the provider to `DeepSeek`.
- Codex CLI integration uses `codex exec` in non-interactive mode with JSON Schema output, `read-only` sandbox by default, `--ephemeral`, `--ignore-rules`, and `--skip-git-repo-check`. The prompt explicitly forbids reading the working directory, shell execution, web search, or file mutation for extraction tasks.
- Codex CLI ordinary extraction defaults to model `gpt-5.5` with reasoning effort `low`; complex-source/manual-reparse extraction defaults to the same model with `medium`; reviewer-prompt extraction defaults to the same model with `xhigh`. `BidOps:CodexCli:Model`, `BidOps:CodexCli:ReasoningEffort`, and runtime operations settings can override those defaults.
- Codex CLI output still lands in staging/lead-only paths and remains subject to the existing human review/import gates. This provider does not bypass review or import controls.

## 2026-06-22 BidOps Runtime AI Provider Switch

- BidOps stores the tenant-level runtime AI provider in `bidops_runtime_setting` under key `ai.provider`. Supported values are `DeepSeek`, `Mimo`, and `CodexCli`.
- The operations dashboard exposes the switch so operators can move extraction between DeepSeek/OpenAI-compatible HTTP, Xiaomi Mimo/OpenAI-compatible HTTP, and Codex CLI without redeploying Worker/WebApi.
- The switch only controls provider selection. API keys, endpoints, Codex binary path/model/reasoning settings, and other secrets remain in configuration/environment variables and are not stored in tenant data.

## 2026-06-22 BidOps Outcome Package Identity

- Public award/candidate supplier rows must not use `PackageNo + SupplierName` as a unique package identity, because different lots often reuse the same package number such as `包1`.
- Outcome package matching uses the available parts of the composite package identity `LotNo + LotName + PackageNo`. If both sides provide `LotNo` or `LotName`, that field must match; missing lot-number or lot-name values are treated as missing evidence rather than proof that two package contexts are the same.
- Outcome supplier extraction merge, manual-edit source hash, and extraction source hash include `LotNo`, `LotName`, `PackageNo`, and supplier name. This preserves distinct result rows such as the same winning supplier appearing in `9001005/综合服务/包1` and `9001005/运维服务/包1`.

## 2026-06-22 BidOps Codex CLI Windows Launch

- BidOps Worker may run on Windows where `where codex` can return an extensionless npm shim before `codex.cmd`. .NET `ProcessStartInfo` cannot execute that shim and fails with `Access is denied`.
- Codex CLI process startup resolves Windows command names to `.cmd`, `.bat`, or `.exe` explicitly, preferring the executable wrapper over extensionless shims.
- Codex CLI output schema temp files are written as UTF-8 without BOM and validated before launch, because the CLI JSON reader may reject a BOM at byte 0 as `expected value at line 1 column 1`.
- Background job result payloads expose provider-neutral `aiResponses` while keeping `deepSeekResponses` as a compatibility alias for existing UI/history.

## 2026-06-22 BidOps Review Detail Background Jobs

- Review detail surfaces only background jobs explicitly launched from that review task. The association is the real background `jobId`, recorded at enqueue time in the review correction sample evidence JSON.
- The review-detail jobs endpoint no longer infers ownership by scanning `rawNoticeId` in background job payloads/results, because announcement-level jobs and review-page actions can legitimately overlap on the same RawNotice.
- Announcement-level pipeline/history jobs should be shown in a separate RawNotice pipeline view with clear labeling, not mixed into the review-task action history.

## 2026-06-22 BidOps Historical Outcome Quality Repair

- Historical outcome quality repair is exposed through `tools/Atlas.LocalSetup repair-bidops-data-quality` and defaults to dry-run. Operators must pass `--confirm` before the command updates outcome rows or removes derived quality issues.
- The repair only fills missing outcome `LotNo` when the row evidence begins with a clear public lot-number token followed by package wording such as `包1`. It does not infer lot identity from supplier name or package number alone.
- Stale review quality issues may be deleted when they point to an outcome supplier record that no longer exists, when the current outcome record no longer lacks both lot and package identity, or when the current package staging rows have exactly one compatible `LotNo/LotName + PackageNo` match.
- Review task quality summaries are recalculated from remaining unresolved quality issues after the derived cleanup. This is a pragmatic historical repair path and does not regenerate AI extraction output or mutate formal notice/package business records.

## 2026-06-22 BidOps Supplier Business Numbers

- BidOps supplier and buyer business numbers must not be generated from only the low six digits of the Snowflake ID. High-volume outcome synchronization can create different IDs with the same low six digits on the same day, causing `TenantId + SupplierNo` collisions.
- New BidOps supplier and buyer numbers use the existing date prefix plus a base36 encoding of the full Snowflake ID, for example `SUP-20260622-<full-id-base36>`. This keeps numbers readable while preserving uniqueness without a database round trip.
- Existing historical supplier and buyer numbers are not rewritten automatically. They remain valid display identifiers, and new full-ID numbers no longer collide with the old six-digit suffix format.

## 2026-06-23 Background Job Hard Timeout And Progress

- One-time background jobs now have a hard maximum running time through `BackgroundTasks:OneTimeJobs:MaxRunningSeconds`, defaulting to 7200 seconds. Jobs exceeding that limit are marked `Dead` with a timeout result instead of staying `Running` indefinitely.
- The Worker enforces this in two places: it sweeps already-running stale jobs before each polling cycle, and it links each active job execution to a timeout cancellation token. The sweep uses the current `LockedAtUtc` before `StartedAtUtc`, so retries are measured from the current execution lock rather than the first historical start.
- Running progress is stored in the existing background job `Result` field as a short JSON heartbeat and updates `UpdatedAt`; final success still overwrites `Result` with the normal handler output. Progress is an operator visibility signal only and is not used as business evidence.
- BidOps AI parsing jobs use the generic progress reporter while Codex CLI is running so operators can distinguish slow model calls from dead jobs.
- Codex CLI process cancellation kills the full process tree for both CLI timeout and upstream Worker/job cancellation. This prevents orphaned `cmd/node/codex.exe` processes after a Worker restart or hard job timeout.
- Codex prompt construction now caps deterministic/reference JSON at 12,000 characters. The public source text budget remains controlled by `BidOps:Ai:MaxInputCharacters` / `BidOps:CodexCli:MaxInputCharacters`; the cap prevents large rule-derived references from inflating prompts to very slow sizes.

## 2026-06-23 BidOps Outcome Supplier Codex Prompt Budget

- Outcome supplier extraction uses deterministic parsing as the primary safety net, so the Codex CLI prompt should be a focused correction/enrichment prompt rather than a full raw-document replay.
- The outcome supplier Codex prompt caps deterministic reference JSON at 40 records / 6,000 characters. This keeps ordinary public-result recognition suitable for `low` reasoning while still preserving enough examples for merge/correction.
- Public source material for outcome supplier Codex calls is compressed around result-specific evidence such as `中标/成交/候选`, `分标编号`, `包号`, supplier names, and amount columns. Attachments receive most of the source budget, HTML receives a smaller budget, and reviewer-prompt calls receive a larger source ceiling.
- The source compaction is intentionally conservative: it may omit irrelevant raw notice prose, but it must retain explicit lot/package/supplier evidence so review quality matching can still use `LotNo/LotName + PackageNo`.

## 2026-06-23 Background Job Force Cancellation

- Normal cancellation remains cooperative: it writes `CancellationRequestedAt`, and the Worker passes cancellation tokens to handlers and external AI calls.
- Force cancellation is an operator action for stuck running jobs. It immediately marks the job `Canceled`, clears the lock, stops retry scheduling, and keeps the task history instead of deleting the row.
- Force cancellation is not a business rollback. Handler code must remain idempotent, and the Worker re-checks cancellation state before writing a successful result so a late external response cannot overwrite a forced cancellation.
- BidOps job progress messages use provider-neutral `AI 正在...` wording because runtime provider can be DeepSeek or Codex CLI and can change between jobs.

## 2026-06-25 BidOps Bulk Approval Timeout Mitigation

- Review approval must not synchronously call external AI providers. If a result/candidate notice is approved before outcome supplier records exist, the WebApi approval path enqueues `bidops.outcome.supplier-extract` after the approval transaction instead of calling Mimo/DeepSeek/Codex directly.
- The post-approval outcome extraction job is audit-linked to the review task through a dedicated `ApprovalOutcomeExtract` correction-sample source kind, so review detail can still show the background job without counting it as a reviewer-prompt reparse.
- Supplier master-data matching needs the same normalized-name index pattern already used for buyers. `bidops_supplier.NameNormalized` is non-unique because historical supplier data may contain duplicates; the index is for lookup performance, not uniqueness enforcement.
- Local `atlas_bidops_runtime` had no EF migration history despite existing BidOps tables, so the development database was updated with a targeted additive DDL patch for `bidops_supplier.NameNormalized`; the repository migration remains the canonical deployment path.

## 2026-06-26 BidOps Runtime HTTP AI Tokens

- BidOps operators can manage Mimo and DeepSeek API tokens from the operations UI. Values are stored in `bidops_runtime_setting` under `ai.mimo.api-key` and `ai.deepseek.api-key`, using the existing Atlas crypto service when `Security:Crypto:Key` is configured.
- Mimo HTTP extraction resolves API keys in this order: runtime token, `BidOps:Mimo:ApiKey`, provider-matching `BidOps:Ai:ApiKey`, then `MIMO_API_KEY`. DeepSeek resolves runtime token, `BidOps:Ai:ApiKey`, `BidOps:DeepSeek:ApiKey`, then `DEEPSEEK_API_KEY`.
- The operations API never returns raw tokens. It exposes only configured status, source, masked suffix, update metadata, and test status.
- The token test endpoints validate token/model/endpoint connectivity even when WebApi `BidOps:Ai:Enabled` is false, because WebApi may not run AI extraction while Worker does. Worker extraction still respects the Worker host's own AI enablement settings.
- Worker hosts need the same `Security:Crypto:Key` as WebApi to decrypt runtime tokens. The local Worker appsettings now includes the same development crypto key pattern already used by WebApi; production should override it through secure configuration.

## 2026-06-27 BidOps Lifecycle Retry Idempotency

- Retrying `bidops.lifecycle.reverse-closure` jobs should not mutate the original retry semantics just to hide deterministic business errors. If a retry repeatedly dies, the handler must make its persistence idempotent or surface a real validation failure.
- Lifecycle link uniqueness remains tenant-scoped by `TenantId + SourceHash`. The reverse-closure handler now collapses duplicate suggestions with the same persistence hash before saving, preferring the suggestion with less manual review risk, higher confidence, and richer amount/candidate/tender evidence.
- Duplicate closure suggestions are an extraction/persistence artifact, not separate business outcomes. Collapsing them before writing prevents a single job from inserting two `bidops_lifecycle_package_link` rows that would violate the existing unique index.

## 2026-06-27 BidOps Lifecycle Lot Context Enrichment

- Lifecycle closure should align with the review detail's visible package context when the same RawNotice already has parsed outcome rows or review package rows. Those rows are treated as previously extracted/reviewed public evidence, not as a separate business import.
- New lifecycle reverse-closure runs enrich award evidence from `bidops_outcome_supplier_record` and the associated review `bidops_package_staging` rows before building package links. This fills lot number/name when the award-document parser only extracted package number and supplier.
- Lifecycle link list/detail responses may display-enrich missing lot/package text from outcome/package context without mutating the stored link row. This keeps historical suggestions readable while preserving their original audit record.
- The lifecycle closure center should function as a review workbench, not just a raw link list. Each row should expose the winning supplier, lot/package identity, final award amount, matched procurement notice evidence, and original public attachments when available.
- If the corresponding procurement notice has not been collected into RawNotice, the UI should show an explicit missing-procurement reason instead of silently leaving the procurement notice blank. Operators can then import/crawl the procurement announcement and rerun or refresh closure review.
- Attachment evidence is shown from the underlying RawNotice rows. Procurement attachments are primary for package-scope review; award/result attachments remain visible because they are the public source for supplier and amount evidence.

## 2026-06-28 BidOps 22FK09 Wrapped Award Table Repair

- Do not treat missing 22FK09 lifecycle lot fields as an AI reparse problem first. The public award attachment already contains `分标编号/分标名称`, and the local procurement staging rows already contain the package context. The durable fix is deterministic parsing plus read-time enrichment from existing reviewed evidence.
- State Grid award PDFs can extract as header-driven wrapped text without row sequence numbers. The outcome parser should reconstruct these rows by table header, lot-code fragments, package token, package-name fragments, and trailing supplier-name fragments instead of relying on an AI provider to infer row boundaries.
- Generic lot labels such as `未分标段` are placeholders in lifecycle display. When a unique procurement package staging row matches by normalized package number, its lot number/name may override the generic display value without mutating the stored lifecycle link row or original outcome record.
- Project-code matching must normalize business-code wrappers and punctuation. `code:22FK09`, `22FK09）`, and `22FK09` should match for procurement lookup and lifecycle display; the original stored audit text is not rewritten by this normalization.
- AI provider provenance remains owned by background-job diagnostics. The lifecycle closure page links to the existing BidOps job list filtered by RawNoticeId, where job detail already displays `aiResponses` / `deepSeekResponses` provider, model, endpoint, status, and raw response. BidOps lifecycle business services should not query the global job store directly just for display diagnostics.

## 2026-06-28 BidOps Lifecycle Reparse Scope

- Row-level `AI补全` / `提示词补全` in the lifecycle closure center is field enrichment for one `bidops_lifecycle_package_link` row. It updates only that lifecycle link's missing/empty fields and enrichment evidence; it does not rewrite RawNotice text, NoticeStaging, PackageStaging, or outcome supplier records.
- Outcome supplier re-extraction is announcement-scoped, not row-scoped. Running it for an award/result RawNotice may replace all `bidops_outcome_supplier_record` rows for that RawNotice, so the button belongs in the top award-notice context rather than in each supplier detail row.
- Approved RawNotice full reparse remains blocked by the review workflow. The lifecycle closure button intentionally enqueues only `bidops.outcome.supplier-extract` against already-collected public source material, allowing historical result-detail extraction fixes without changing the approved RawNotice/review status.
- Operators should inspect the generated background job for provider/model diagnostics and refresh or rerun lifecycle closure after completion when newly extracted supplier/package rows should create additional lifecycle links.

## 2026-06-28 BidOps Lifecycle Procurement Package Amounts

- Lifecycle closure package identity should display `包号` as its own column. Package names are useful context, but they should not hide the normalized package number operators use to locate procurement rows.
- Procurement package amount is a read-model enrichment for the closure center. It is derived from reviewed procurement staging evidence and is not persisted back to `bidops_lifecycle_package_link`, preserving the historical audit row.
- Amount matching prioritizes `分标名称 + 包号`, then other explicit package context. If multiple procurement detail rows match and the relevant amount field is not unique, the service leaves the display amount blank instead of summing or inventing a package amount.

## 2026-06-28 BidOps Lifecycle Amount Defaults And Chinese Review Text

- When a lifecycle row lacks a direct award amount and lacks a usable candidate final quote, a unique matched procurement package amount may be used as the default final award amount for review. This is marked as `DefaultedFromProcurementPackageAmount`, not as a direct award-notice amount.
- Defaulted procurement amounts still require manual review. They are a practical review default for comparing procurement and result rows, not a guarantee that the supplier's final payable amount equals the procurement estimate.
- Lifecycle detail labels for match reasons and missing fields are translated in the UI. Stored `MatchReasonsJson`, `MissingFieldsJson`, and evidence JSON remain unchanged to preserve deterministic audit/debug values.

## 2026-06-28 BidOps Product-Facing Enum Display

- Product-facing UI must not display raw English enum/code values directly. Backend payloads and stored JSON may keep stable English identifiers, but Vue pages/components must format them through Chinese labels before rendering.
- Editable enum fields should submit the internal value while showing the Chinese label, for example the lifecycle confirmation dialog's amount source selector.

## 2026-06-28 BidOps Lifecycle Batch Review Scope

- Lifecycle closure batch review operates on explicit table selection for the current result set/page, not on hidden cross-page rows.
- Batch review only allows `Suggested` lifecycle links to be selected. Confirmed, rejected, failed, or other non-reviewable rows must remain visible but not batch-mutable.
- The MVP batch action reuses the existing single-row confirm/reject API calls. This preserves per-link audit updates and avoids introducing a backend bulk mutation contract before approval semantics need it.

## 2026-06-28 BidOps Lifecycle Reparse Refresh Semantics

- Lifecycle-center outcome reparse is an operator action to repair closure review data, so it must refresh lifecycle links after replacing outcome supplier rows. Leaving reparse and reverse closure as two hidden steps causes correct supplier extraction to remain invisible in the closure center.
- General post-approval outcome supplier extraction remains supplier-record scoped. Only lifecycle-center reparse jobs set `RefreshLifecycleLinks=true`.
- The closure center should default to showing all link statuses. Bulk review controls can still restrict selectable rows to `Suggested`, but status filtering should not hide confirmed rows by default when operators are reconciling counts.
- Re-running closure for the same award notice should replace stale non-confirmed suggestions for that award notice. Manually confirmed links are preserved and treated as equivalent by award notice, project code, package number, and supplier, even when improved extraction changes the persistence hash.

## 2026-06-28 BidOps Amount Unit Context

- Public tender/result tables often place amount units in the column header or a nearby `金额单位：万元` line while cells contain only numbers. Amount normalization must consider this unit context before persisting or displaying review amounts.
- A cell-level explicit unit wins first. If the cell has no unit, an explicit `元` column header wins over nearby context; otherwise `万元` context means the stored amount is RMB yuan after multiplying by 10,000.

## 2026-06-28 BidOps Manual Lifecycle Analysis Reruns

- The "分析闭环" action is an operator rerun, not a forever-idempotent create operation. A historical background job may have succeeded with zero links, failed before persistence, or used an older parser, so it must not permanently block a fresh lifecycle analysis for the same RawNotice.
- The global background job table keeps the existing `TenantId + DeduplicationKey` unique index. BidOps avoids changing global queue semantics by adding a per-run suffix only to manual lifecycle reverse-closure enqueue keys.
- Lifecycle closure pages with a RawNotice filter and no link rows should point operators to the filtered background-job list, because task state and persisted lifecycle rows are separate audit surfaces.

## 2026-06-28 BidOps Procurement Notice Official Search

- State Grid ECP procurement notice lookup should use the official project/procurement-code field before title/full-text keyword search. The public `noteList` API exposes this as `purOrgCode`, and it is more precise for codes such as `22FK09` than the generic `key` field.
- The crawler normalizes common wrappers and trailing punctuation before sending the code to `purOrgCode`, so values like `code:22FK09` and `22FK09）` are searched as `22FK09`.
- Generic `key` search remains a fallback when project-code search returns fewer candidates, preserving recall for historical notices whose list metadata lacks the code field.

## 2026-06-28 BidOps Formal Notice Lifecycle Review Status

- The formal notice library displays lifecycle review state as a read model derived from `bidops_lifecycle_package_link` rows where `AwardRawNoticeId` equals the notice's `RawNoticeId`.
- Exact row display statuses are `NotAnalyzed`, `PendingReview`, `PartiallyApproved`, `Approved`, and `Rejected`. The query-only status `NotApproved` means anything except fully approved, including not analyzed notices.
- This status does not mutate formal `Notice.Status` and does not require a schema change. It is meant to help operators distinguish outcome notices that have passed closure review from notices that still need closure work.

## 2026-06-28 BidOps Lifecycle Prompt Parse Placement

- Lifecycle prompt-assisted parsing belongs to the source notice context, not to each lifecycle detail row. The closure center keeps row operations focused on detail review, confirmation, and rejection.
- Award notice prompt parsing uses the announcement-scoped outcome supplier re-extraction flow, which replaces outcome supplier records for the award RawNotice and refreshes lifecycle links on job completion.
- Procurement notice prompt assistance is triggered from the procurement notice context and enqueues field-enrichment jobs for the currently associated non-final lifecycle links. This avoids using the general RawNotice reparse path that can reset review state or reject already-approved formal notices.
- Prompt tasks started from the closure context must refresh the lifecycle list after their background jobs reach a terminal success state, so operators do not have to manually reload after AI work completes.

## 2026-07-01 BidOps Award Auto Procurement Closure

- Result notice collection should refresh lifecycle links and then try to collect the corresponding 前置公告 automatically. This keeps the collection workflow and the closure review page aligned instead of requiring operators to manually run "找前置公告" after each award parse.
- Auto procurement collection must reuse the same State Grid project-code search path used by the closure page. It only proceeds on exact project-code candidates and skips ambiguous same-priority matches so a public default-list response cannot silently create a wrong province/project link.
- Source notice priority follows the existing classifier: bidding projects prefer `招标公告 / 投标邀请书`, while non-bidding projects prefer `前置公告 / 采购邀请`.
- Auto-review is deliberately narrower than manual batch review. It confirms only suggested, actionable award rows with a linked procurement RawNotice, adequate match score, non-empty project code/supplier, and no service-fee-looking final amount source. Flow-failed rows remain display-only.
- The closure page's batch review now uses a backend batch contract. This gives the server one place to enforce status-only/failed-row safeguards and keeps browser-side loops from drifting from backend approval rules.

## 2026-07-01 BidOps Queue Health Clock

- BidOps crawl timestamps such as `LastScanTime`, `LastSuccessTime`, checkpoint run times, and crawl-run completion times are stored as UTC values.
- Operations channel health must use a UTC clock when calculating `MinutesSinceLastSuccess`, `Due`, and `Stale`; local wall-clock time is only suitable for product-facing date grouping such as "today" counts.
- A successful crawl that discovers only duplicates or skipped records still counts as a successful queue run as long as the crawler completed without item failures.

## 2026-07-02 BidOps Review Pool Bulk Approval

- Review-pool bulk approval keeps the existing synchronous endpoint for small selections because operators expect immediate success/failure feedback for a few rows.
- Large selections must be represented as one backend background job, not as many browser requests. The `bidops.review.bulk-approve` job carries the selected review-task IDs and executes the existing per-item approval rules in Worker.
- The background job intentionally reuses the single-item approval path instead of introducing set-based mutations first. This preserves risk checks, correction samples, formal notice persistence, post-approval extraction enqueueing, and organization master-data updates while moving long-running work out of the HTTP timeout window.
- Creating a large bulk-approval job must also reserve eligible pending tasks as `InReview` before the Worker consumes the job. Otherwise the review pool still shows queued work under `待审核`, allowing duplicate operator actions.

## 2026-07-02 BidOps AI Output Versus Persisted Outcome Rows

- Large review-detail tables need stable visible row numbers. Operators often diagnose extraction issues by saying "第 N 行", and relying on scroll position alone is brittle for 100+ row notices.
- When comparing AI quality with persisted outcome rows, the first diagnostic split should be: AI response rows from the background-job `aiResponses[].assistantContent.records`, selected/merged extracts, and final `bidops_outcome_supplier_record` rows. This separates "AI did not extract it" from "merge/sanitize/persistence lost or duplicated it".
- Empty `lotNo` is not automatically an AI failure. Current outcome prompts intentionally require `lotNo` to stay empty unless the source explicitly labels a 分标编号/标段编号; 分标名称 belongs in `lotName`.
- Weak fallback rows with the same supplier, package, outcome type, rank, and evidence as a stronger row should be pruned before persistence. If historical data already contains weak duplicates, rerun outcome-supplier extraction for the RawNotice to replace the persisted set.

## 2026-07-02 BidOps Amount Candidate Reparse Freshness

- Amount candidates derived from `OutcomeSupplierRecord` are cached review evidence, not independent source facts. When outcome supplier records are re-extracted and replaced, candidates pointing at deleted outcome rows must be removed before loading the review or lifecycle candidate pool.
- Raw notice and attachment full-text scans may produce `unknown` numeric candidates from package numbers, page numbers, or voltage levels such as `500 千伏`. If such candidates have no lot/package/supplier context, they should not be shown as reviewable amount candidates and should not be generated going forward.
- Review-detail amount context should prefer business names (`lotName`, `packageName`) and only fall back to codes (`lotNo`, `packageNo`) when names are absent. This matches how operators identify problematic rows in result notices.
