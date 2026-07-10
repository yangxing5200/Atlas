# Implementation Log

## 2026-07-09 BidOps Local Restart Script Recovery

Completed:

- Investigated `scripts/restart-bidops-local.ps1` startup failure and confirmed the script file had been overwritten with NUL bytes, causing PowerShell to try to execute an invalid command at line 1.
- Rebuilt the combined local restart script so it starts WebApi, Worker, and Atlas Admin frontend by default, keeps `-SkipBuild`, `-SkipWebApi`, `-SkipWorker`, and `-SkipFrontend` escape hatches, and reuses the existing WebApi/Worker restart scripts.
- Preserved the local startup safety flow: stop backend processes before backend builds, shut down .NET build servers, build WebApi/Worker, then launch both with `--no-build` to avoid shared output DLL locks.
- Added frontend restart and health checks for `http://localhost:5173/` and `http://localhost:5260/api/auth/context`.

Verification:

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -SkipBuild -SkipWebApi -SkipWorker -SkipFrontend -StartupWaitSeconds 0` succeeded after repair.
- `dotnet --info` confirmed the machine has .NET 9 SDK plus .NET 8 runtime; Atlas still targets `net8.0`.
- `node --version` returned `v18.16.0`; `npm.cmd --version` returned `9.5.1`.
- `docker compose config` succeeded, and `Test-NetConnection -ComputerName localhost -Port 3306` confirmed MySQL is reachable.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -StartupWaitSeconds 8` succeeded: WebApi and Worker built with 0 warnings/0 errors, frontend returned HTTP 200, and WebApi auth context returned the expected unauthenticated HTTP 401.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -SkipBuild -StartupWaitSeconds 5` succeeded against already-running services and restarted WebApi, Worker, and frontend cleanly.

## 2026-07-09 BidOps Local Restart Default Worker

Completed:

- Changed `scripts/restart-bidops-local.ps1` so right-click/default execution starts WebApi, Worker, and Atlas Admin frontend together.
- Kept `-SkipWorker` as the explicit UI-only escape hatch and updated the startup message to show whether Worker is enabled or intentionally skipped.

Verification:

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -SkipBuild -SkipWebApi -SkipWorker -SkipFrontend -StartupWaitSeconds 0` succeeded. The no-op smoke test returned frontend `200` and expected unauthenticated WebApi auth `401` without stopping running services.

## 2026-07-07 BidOps Outcome Rebuild DryRun P0-5

Completed:

- Added a minimal outcome-supplier rebuild dry-run job: `bidops.outcome.supplier-rebuild-dry-run`.
- Added `OutcomeSupplierRebuildDryRunJobPayload`, `OutcomeSupplierRebuildDryRunResultDto`, and a Worker handler that returns existing count, preview extracted count, preview saved count, delta count, source counts, lot-number validation counts, and AI diagnostics.
- Added `IBidOpsOutcomeSupplierExtractionService.DryRunRawNoticeAsync`, reusing the current extraction pipeline while avoiding deletion, insert, quality recomputation, lifecycle refresh, and organization master-data sync.
- Added an enqueue API at `POST /api/bidops/raw-notices/{id}/outcome-suppliers/rebuild-dry-run`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "OutcomeSupplierRebuildDryRunJobHandler_ReturnsJsonResultSummaryWithoutApplying|BidOpsModule_RegistersServicesAndBackgroundHandlers|RawNoticesController_DeclaresPipelineAndReparseRoutes" --no-restore --nologo --verbosity minimal` succeeded: 3 passed.
- `git diff --check` succeeded for the touched P0-5 files.
- Final combined verification also succeeded:
  - `npm run typecheck` in `frontend/atlas-admin`.
  - `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtracts_CarrySourceDiagnostics|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|BidOpsOutcomeSupplierExtractionService_AutomaticMergeDropsLegacyWeakRowCoveredByAi|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames|BidOpsOutcomeSupplierExtractionService_AutomaticMergePrioritizesAiAnnouncementOrder|BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsDeterministicRowsMissingFromAi|BidOpsOutcomeSupplierExtractionService_PreservesPipeDelimitedLotNoEvidence|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo|OutcomeSupplierRebuildDryRunJobHandler_ReturnsJsonResultSummaryWithoutApplying|BidOpsModule_RegistersServicesAndBackgroundHandlers|RawNoticesController_DeclaresPipelineAndReparseRoutes" --no-restore --nologo --verbosity minimal` succeeded: 11 passed.
  - `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\\"` succeeded with existing unrelated warnings. A normal build to project `bin` failed first because running local `Atlas.Worker` and `Atlas.WebApi` processes locked `Atlas.Modules.BidOps.dll`.

## 2026-07-07 BidOps LotNo Validation Diagnostics P0-4

Completed:

- Changed outcome lot-number sanitization to produce validation status and reason instead of only returning a boolean decision.
- Added in-memory lot-number diagnostics to outcome extracts: `RawLotNo`, `LotNoValidationStatus`, and `LotNoValidationReason`.
- Preserved lot-number diagnostics through post-processing clone paths.
- Added `lotNoValidationCounts` to outcome extraction job summaries so rejected/accepted/empty lot-number decisions are visible without adding database columns yet.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PreservesPipeDelimitedLotNoEvidence|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary" --no-restore --nologo --verbosity minimal` succeeded: 3 passed.
- `git diff --check` succeeded for the touched P0-4 files.

## 2026-07-07 BidOps Weak Outcome Candidate Merge P0-3

Completed:

- Changed automatic outcome merging from hard `supplier + lotNo/lotName + package` grouping to an AI-first survivor flow with compatibility checks for deterministic fallback rows.
- Added weak-candidate pruning inside exact dedupe so higher-trust candidates can cover lower-trust fragmented rows when supplier, outcome, rank, package, and lot context are compatible.
- Kept reviewer-prompt semantics that allow AI-corrected supplier names to replace deterministic rows when package and lot context match.
- Protected same-source rows that both have meaningful lot context from being merged solely because supplier and package match.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_AutomaticMergeDropsLegacyWeakRowCoveredByAi|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames|BidOpsOutcomeSupplierExtractionService_AutomaticMergePrioritizesAiAnnouncementOrder|BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsDeterministicRowsMissingFromAi" --no-restore --nologo --verbosity minimal` succeeded: 4 passed.
- `git diff --check` succeeded for the touched P0-3 files.

## 2026-07-07 BidOps Outcome Candidate Source Diagnostics P0-2

Completed:

- Added in-memory source diagnostics to `BidOpsOutcomeSupplierExtract`: `SourceType`, `SourceParserVersion`, and `SourceCallId`.
- Tagged outcome candidates from AI OutcomeSuppliers, legacy text parser, wrapped table parser, PDF structured table parser, candidate evidence parser, and award evidence parser.
- Preserved source diagnostics when non-award rows and sanitized rows are cloned during post-processing.
- Added `sourceCounts` to outcome extraction job summaries so operators can see which sources survived into the save flow without adding database columns yet.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtracts_CarrySourceDiagnostics|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary" --no-restore --nologo --verbosity minimal` succeeded: 2 passed.
- `git diff --check` succeeded for the touched P0-2 files.

## 2026-07-07 BidOps AI Call Diagnostics P0-1

Completed:

- Updated the background job AI diagnostics tab to show business labels for AI calls: `NoticeStaging` as 公告结构解析, `OutcomeSuppliers` as 中标成交明细解析, and `LifecycleFieldEnrichment` as 闭环字段补全.
- Added a compact assistant-content summary for each AI call, including records, packages, requirements, and completeness counts for supplier, lot number, lot name, package number, and evidence text.
- Added an operator note that AI raw responses are diagnostic evidence and can differ from final persisted review details because deterministic parsing, merge, cleanup, and persistence still run after the model returns.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check -- frontend/atlas-admin/src/modules/operations/pages/BackgroundJobDetailPage.vue` succeeded.

## 2026-07-04 BidOps Fragmented PDF Outcome Lot Context

Completed:

- Investigated outcome supplier record `331349894998659075`. Its evidence row was reduced to `2-02 房屋维修-总承包 包 38 中恒诚信建设有限公司`, so the full lot number prefix was no longer present in the row.
- Added an outcome post-processing enrichment step that builds lot contexts from complete rows in the same extraction result, then fills missing `LotNo` / `LotName` only when the current row's leading lot-name fragment maps to one unique structured lot number.
- Restricted enrichment to the current row prefix before the first package marker, so a long evidence snippet containing later table rows does not accidentally supply the wrong lot context.
- Added regression tests for unique fragmented-row enrichment, ambiguous lot-name protection, and long-evidence protection.
- Backfilled local historical rows with the same conservative rule: short evidence row, structured unique same-notice lot context, and one inferred context per row. Updated 2,513 rows; record `331349894998659075` now has `LotNo=18FV2F9003002-02` and `LotName=房屋维修-总承包`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_EnrichesFragmentedLotNoFromUniqueOutcomeContext|BidOpsOutcomeSupplierExtractionService_DoesNotEnrichFragmentedLotNoWhenLotNameIsAmbiguous|BidOpsOutcomeSupplierExtractionService_DoesNotUseLaterTableRowsForFragmentedLotContext|BidOpsOutcomeSupplierExtractionService_FillsSingleHyphenLotNoFromInlineOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_KeepsInlineLotNoEvidenceWithLotName|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo" --no-restore --nologo --verbosity minimal` succeeded: 6 passed.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 errors; existing nullable/EF warnings remain in unrelated test projects.
- `git diff --check` succeeded.
- Restarted local WebApi and Worker. WebApi returned the expected unauthenticated `HTTP 401` at `http://localhost:5260/api/auth/context`, and the frontend returned `HTTP 200` at `http://localhost:5173`.

## 2026-07-04 BidOps Single-Hyphen Outcome LotNo Fix

Completed:

- Investigated outcome supplier record `331349894168186882`. Its evidence row contained `18FV2F9001005-29`, but the sanitizer only recognized structured lot numbers with at least two separators, so the single-hyphen SGCC lot code was left empty.
- Extended outcome lot-number parsing to support long-prefix single-separator codes such as `18FV2F9001005-29` when the same evidence row also contains package context.
- Added regression coverage for the `中国电建集团江西省电力设计院有限公司` evidence row shape.
- Backfilled local historical outcome supplier records where `LotNo` was empty and the evidence row safely exposed a single-hyphen lot number plus package context. Updated 2,123 rows; record `331349894168186882` now has `LotNo=18FV2F9001005-29`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_FillsLotNoFromOrdinalPrefixedOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_KeepsInlineLotNoEvidenceWithLotName|BidOpsOutcomeSupplierExtractionService_FillsSingleHyphenLotNoFromInlineOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo|BidOpsOutcomeSupplierExtractionService_KeepsLotNoWhenSourceHasExplicitLotHeader" --no-restore --nologo --verbosity minimal` succeeded: 5 passed.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 errors; existing nullable/EF warnings remain in unrelated test projects.
- `git diff --check` succeeded.
- Restarted local WebApi and Worker. WebApi returned the expected unauthenticated `HTTP 401` at `http://localhost:5260/api/auth/context`, and the frontend returned `HTTP 200` at `http://localhost:5173`.

## 2026-07-03 BidOps Review Outcome Dynamic Columns

Completed:

- Restored dynamic column rendering on the review detail comparison table for award/candidate outcome rows, including project, lot name, package name, status, amount, fee, and evidence columns when those fields have data.
- Removed the remaining award-detail pagination in the parsing review tab so award rows stay visible as a single comparison list.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-07-03 BidOps Outcome LotNo Preservation Fix

Completed:

- Investigated background job `331347575418523648` and review task `328034536208338949`. The AI response contained `lotNo=18FV2F9003001-14-05` for supplier `巨商控股 （山东） 集团有限公司`, but the persisted `bidops_outcome_supplier_record` row had an empty `LotNo`.
- Found the cause in outcome extraction sanitization: inline evidence rows shaped like `序号 分标编号 分标名称 包号 供应商` were not accepted as explicit lot-number evidence unless a nearby labeled table header was also detected.
- Extended the lot-number support check to trust inline public outcome rows that start with an ordinal plus a structured lot code and later contain package/supplier context.
- Backfilled the affected local RawNotice `327873693386674176`: 11 rows had `LotNo` restored from their evidence text; the `巨商控股 （山东） 集团有限公司` row now has `LotNo=18FV2F9003001-14-05`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_FillsLotNoFromOrdinalPrefixedOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_KeepsInlineLotNoEvidenceWithLotName|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo|BidOpsOutcomeSupplierExtractionService_KeepsLotNoWhenSourceHasExplicitLotHeader" --no-restore --nologo --verbosity minimal` succeeded: 4 passed.

## 2026-07-03 BidOps Review Detail Comparison Layout

Completed:

- Changed the review detail default tab to “公告对照”, showing the original announcement text beside the award/candidate detail table.
- Kept edit/add and review decision actions available on the comparison view so operators can approve while comparing the two primary sources.
- Compressed “异常复核” into a compact summary strip; unresolved issue rows are now behind a collapsed details panel.
- Restored the original announcement attachment table on the comparison view, above the announcement text.
- Moved the AI prompt reparse panel above the award/candidate detail table on the comparison view.
- Removed pagination from the comparison-view award/candidate detail table so all rows are visible while checking against the announcement.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded.

## 2026-07-03 BidOps Review Detail Amount Candidate Display

Completed:

- Removed `Unresolved` / `Rejected` amount-candidate groups from the review detail amount candidate pool. The panel now shows only actionable `Selected`, `Recommended`, and `Candidate` rows.
- Updated the amount-candidate count and empty state to reflect only visible rows, so hidden pending/rejected candidates do not leave a misleading total.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-07-03 BidOps Background Job Business Link Backfill

Completed:

- Added structured business-link fields to `BackgroundJob`: `SourceModule`, `BusinessType`, `BusinessId`, and `CorrelationId`.
- Added Global DB migration `20260703090000_v0.2.6-background-job-business-link` with indexed lookup by `(TenantId, SourceModule, BusinessType, BusinessId, CreatedAt)` and `(TenantId, CorrelationId, CreatedAt)`.
- Changed BidOps RawNoticeId job search/summary to use the structured columns instead of scanning `Payload` / `Result` text.
- Added enqueue-time inference for BidOps background jobs from payload JSON and known deduplication-key formats. Manual URL import and mock crawl jobs update the link after they create the RawNotice.
- Added a temporary one-time backfill job `atlas.background-jobs.business-link-backfill`, enabled only in local `BidOpsLocal` config with run id `bidops-rawnotice-20260703`.
- Backfilled the local `atlas_global_bidops.BackgroundJobs` table. The first pass scanned and updated 32,201 historical BidOps jobs; after fixing the handler to avoid clearing the Worker job tracking state, the final pass completed successfully with `scanned=520;updated=0;skipped=520`. The remaining 520 are BidOps jobs without RawNoticeId evidence, such as scan-level jobs.
- Verified review task `328034536208338949` maps to RawNoticeId `327873693386674176`; the structured business-link query returns 8 related background jobs and `EXPLAIN` uses `IX_BackgroundJobs_Tenant_BusinessLink` with `rows=8`.

Verification:

- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_FiltersBidOpsJobsByExactRawNoticeId|AddAtlasBackgroundTaskRuntime_WorkerDefaults_RegisterHostedWorkers|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-build --nologo --verbosity minimal` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Local Global DB schema now has `SourceModule varchar(64)`, `BusinessType varchar(64)`, `BusinessId bigint`, `CorrelationId varchar(128)`, plus both new indexes. The standard EF update could not be used on this local database because its `__EFMigrationsHistory` table is empty while base tables already exist, so the equivalent DDL was applied manually for `atlas_global_bidops`.
- Restarted local Worker and WebApi in `BidOpsLocal`; WebApi returned the expected unauthenticated `HTTP 401` at `http://localhost:5260/api/auth/context`, and the frontend returned `HTTP 200` at `http://localhost:5173`.

## 2026-07-03 BidOps Review Detail Timeout Diagnosis And Fix

Completed:

- Inspected local WebApi logs under `src/Atlas.WebApi/logs/application`. The review detail body endpoint was returning successfully in roughly 226-824 ms, while `GET /api/bidops/review-tasks/{id}/jobs` repeatedly failed after about 30,011-30,084 ms with `MySqlConnector.MySqlException: The Command Timeout expired before the operation completed`.
- Identified the regression source: the review detail page auto-loaded announcement-related background jobs on every page entry/refresh, and the RawNoticeId job filter used `Payload` / `Result` text searches over large background-job diagnostic fields.
- Changed the review detail page so background jobs are loaded only when the operator opens the “后台任务” tab. Reparse actions no longer wait on the jobs list unless that tab is visible.
- Changed RawNoticeId background-job search/summary to avoid database `LIKE` scans over large payload/result fields for the normal review-detail path. It now scans a bounded, tenant/BidOps-scoped recent candidate window and performs exact RawNoticeId matching in memory, including deduplication keys.
- Added test coverage for exact RawNoticeId matching, including guarding against `123` matching `1234` and supporting jobs whose RawNoticeId is only present in the deduplication key.
- Restarted local WebApi and Worker after the fix.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_FiltersBidOpsJobsByExactRawNoticeId" --no-restore` succeeded: 1 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Initial full build failed because running local `Atlas.WebApi` / `Atlas.Worker` processes held output DLL locks. After stopping those two local processes, `dotnet build Atlas.sln --no-restore` succeeded with 0 warnings and 0 errors.
- Frontend returned `HTTP 200` at `http://localhost:5173`. WebApi returned the expected unauthenticated `HTTP 401` at `http://localhost:5260/api/auth/context`.
- Post-restart log tail showed no new `/api/bidops/review-tasks/{id}/jobs` timeout entries; the remaining timeout records were the pre-fix `16:19:47` entries.

## 2026-07-03 BidOps Review Reparse Quality Cleanup And Detail Layout

Completed:

- Changed review reparse enqueue paths so stale `ReviewQualityIssue` rows for the current review task are removed immediately when a raw-notice or outcome-supplier AI reparse is submitted.
- Reset the review task quality summary to a neutral pending-recalculation state at enqueue time; successful Worker parsing still writes the new quality issues through the existing quality evaluator.
- Added defensive review-detail issue de-duplication by issue type, field, and target object so historical duplicate issue rows do not overwhelm the detail page.
- Reworked the review detail page so the default view shows core parsed fields and the review decision panel first. Background jobs, announcement evidence/full text, and heavy detail review tables now render in lazy tabs.
- Added client-side pagination for quality issues, amount candidates, outcome supplier rows, generic supplier leads, and package tables; package requirement details now render only for the selected package instead of every package on the page.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "EnqueueRawNoticeReparseAsync_ClearsStaleQualityIssues|EnqueueBulkApproveAsync_ReservesEligibleTasks" --no-restore` succeeded: 1 passed.
- `dotnet build Atlas.sln --no-restore` succeeded with 0 errors. Existing nullable/EF warnings remain in unrelated test projects.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsModuleTests" --no-build` succeeded: 150 passed.

## 2026-07-03 BidOps Review Detail Related Jobs

Completed:

- Changed review-task job lookup to show BidOps background jobs related to the review task's `RawNoticeId`, covering attachment processing, structured parse, outcome supplier extraction, and reparse jobs for the same public announcement.
- Added exact `RawNoticeId` filtering to the background job operations query, matching JSON payload/result and `rawNoticeId=...` result summaries without partial ID matches.
- Updated the review detail page task panel wording to “公告相关后台任务”, displayed the active RawNoticeId, and added a task-center shortcut pre-filtered by RawNoticeId.
- Exposed AI request diagnostics (`requestSummaryJson`, `requestBodyJson`, `requestPrompt`) in the BidOps background job detail AI tab.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_FiltersBidOpsJobsByExactRawNoticeId" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 1 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_FiltersAndMapsBidOpsProjectCode|SearchByIdsAsync_ReturnsOnlyRequestedTenantScopedBidOpsJobs|SearchAsync_FiltersBidOpsJobsByExactRawNoticeId" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Initial WebApi/Worker builds failed because local `Atlas.WebApi` PID 18580 and `Atlas.Worker` PID 20076 held output DLL locks. After stopping those local host processes, both `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` and `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded with 0 warnings and 0 errors.
- `git diff --check` succeeded.
- Restarted local WebApi and Worker. WebApi returned `HTTP 401` at `http://localhost:5260/api/auth/context`, frontend returned `HTTP 200` at `http://localhost:5173`, and Worker process restarted.

## 2026-07-01 BidOps Lifecycle Count Refresh Fix

Completed:

- Reproduced the refreshed closure page for RawNotice `328250278660935680` and found the page was not simply stuck at 6 rows: the list endpoint had been failing with `Out of range value for column 'AmountValue'` while backfilling amount candidates during read.
- Guarded `BidOpsAmountCandidateService` so values outside the `decimal(18,6)` storage range are kept as unresolved candidates with raw text/evidence instead of writing an invalid `AmountValue`.
- Adjusted the closure page route entry behavior so opening `/bidops/outcomes?rawNoticeId=...` clears cached status/match filters. This lets the page show both actionable `Suggested` rows and read-only `StatusOnly` 流标 rows for that announcement.
- Restarted local WebApi on `http://localhost:5260` and Worker after rebuilding.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsAmountCandidateService_DropsOutOfRangeStoredAmountValue|BidOpsOutcomeSupplierExtractionService_KeepsIndividualBusinessSupplierNames|BidOpsReverseLifecycleClosureService_MapsFailedOutcomeAsStatusOnlyDisplayRow" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Chrome verification on `http://localhost:5173/bidops/outcomes?rawNoticeId=328250278660935680` showed `Total 9`: 7 actionable rows and 2 read-only `仅展示` 流标 rows.

## 2026-07-01 BidOps Outcome Count Mismatch Diagnosis

Completed:

- Investigated background job `330633078471004160` for RawNotice `328250278660935680`. The job result showed `extractedCount=8`, `savedCount=8`, and lifecycle refresh `closureCount=6`, while the AI raw body contained 9 public result rows.
- Found two separate shrink points: the awarded supplier `牡丹江市爱民区华能工程劳务服务部` was filtered before persistence because supplier-name validation did not allow individual-business names such as `服务部`; two `流标` rows were saved as `Failed` outcome records but intentionally skipped by lifecycle closure.
- Expanded valid supplier-name recognition to include individual-business / shop-style suffixes such as `服务部`, `经营部`, `营业部`, `门市部`, `商行`, and `工作室`.
- Added read-only `StatusOnly` lifecycle display rows for failed outcome records when the closure page is filtered by a specific RawNoticeId. These rows show public流标/废标状态 but do not persist lifecycle links, refresh amount candidates, or allow confirm/reject actions.
- Re-ran the affected RawNotice through a new outcome supplier extract job `330633078472004160`. The local database now has 9 `bidops_outcome_supplier_record` rows, 7 persisted lifecycle links for awarded suppliers, and 2 failed outcome rows available as `StatusOnly` display rows.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_KeepsIndividualBusinessSupplierNames|BidOpsReverseLifecycleClosureService_MapsFailedOutcomeAsStatusOnlyDisplayRow|BidOpsReverseLifecycleClosureService_ClearsFailedOutcomeAmountForDisplayEvidence|BidOpsReverseLifecycleClosureService_DoesNotBuildClosureForFailedOutcomeStatus" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 4 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `npm run typecheck` succeeded in `frontend\atlas-admin`.
- Local DB check for RawNotice `328250278660935680`: `outcome_count=9`, `lifecycle_link_count=7`; the two unlinked rows are `Failed` / `流标` and are expected to render as `StatusOnly` display rows.

## 2026-07-01 BidOps AI Request Diagnostics In Background Jobs

Completed:

- Extended BidOps AI diagnostics with request-side fields: `requestSummaryJson`, `requestBodyJson`, and `requestPrompt`.
- Structured parse, outcome supplier extraction, and lifecycle field enrichment now record the AI request parameters used for HTTP-compatible providers and Codex CLI.
- HTTP diagnostics persist the JSON request body sent to the AI provider, including model, response format, messages, and prompt content, while redacting authorization/token/cookie/password-style values.
- Codex CLI diagnostics persist the CLI request shape, model, reasoning effort, sandbox, timeout, output schema, and stdin prompt, while redacting API key values.
- Lifecycle field enrichment background jobs now include `aiResponses` / `deepSeekResponses` in their job result, matching structured parse and outcome supplier extraction diagnostics.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|StructuredParseJobHandler_ReturnsJsonResultSummary|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|StructuredParseJobHandler_ReturnsJsonResultSummary|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary" --no-build --nologo --verbosity minimal` succeeded after service restart: 4 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded after stopping the local WebApi process that held the old BidOps assembly.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded after stopping the local Worker process that held the old BidOps assembly.
- Restarted the local frontend, WebApi, and Worker stack check: frontend `5173` responded `200`, WebApi `5260` responded `401`, and `Atlas.Worker.dll` was running.
- Background job queue check showed no `Status=0` or `Status=1` pending/running jobs after restart.

## 2026-07-01 BidOps State Grid Award Detail Reparse

Completed:

- Investigated review task `328427936724160523` / RawNotice `328250092823908352`. The stored outcome supplier records and lifecycle links had only 3 rows, while the SGCC award HTML snapshot contained a large result table.
- Found the root cause: award/result notices that say `中标候选人公示活动已经结束` were classified as candidate announcements before award/result signals were checked, so the deterministic award table parser was skipped and the system relied on a small AI result.
- Changed outcome notice-kind detection to prefer explicit award/result title/type/source-url signals and award-result body phrases before candidate-publicity phrases.
- Hardened HTML table parsing so SGCC table fragments without a closing `</table>` are still parsed. This covers WCM text snapshots truncated at the raw-text storage limit.
- Preserved SGCC pipe-delimited table evidence such as `SG2670-9001-13049 | 线路施工 | 包1 | 中标 | ...` as trusted lot-number evidence during outcome supplier persistence, preventing the safety sanitizer from clearing `LotNo`.
- Re-ran local outcome supplier extraction for RawNotice `328250092823908352` after the parser fix. The local database now has 92 `bidops_outcome_supplier_record` rows, all 92 with `LotNo`, and 92 `bidops_lifecycle_package_link` rows for the award RawNotice.
- Confirmed the formal notice still has 10 package staging/formal package rows from the original structured package parse. Closure detail rows are sourced from outcome supplier records and lifecycle links, not from package staging.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PreservesPipeDelimitedLotNoEvidence|BidOpsOutcomeSupplierExtractBuilder_TreatsAwardAnnouncementAsAwardWhenBodyMentionsCandidatePublicityEnded|BidOpsOutcomeSupplierExtractBuilder_ExtractsOpenEndedStateGridHtmlAwardTable" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- Local DB recheck: `outcome_records=92`, `outcome_records_with_lot_no=92`, `lifecycle_links=92`, `package_staging=10` for review task `328427936724160523`.

## 2026-07-01 BidOps Formal Notice Search Persistence

Completed:

- Added local query persistence for the “正式公告库” list. After operators click “搜索”, the current keyword, notice type, lifecycle review status, page index, and page size are saved and restored on browser refresh.
- Reused the shared `useTableQuery` storage mechanism instead of adding a page-specific persistence path. The page “重置” action writes the default formal-notice filters back to the same cache.
- Fixed the clearable selects to persist an explicit empty-string value, so choosing “全部” for 公告类型 or 闭环审核 is restored instead of falling back to the default result-notice filter.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModule_RegistersServicesAndBackgroundHandlers|ReviewTasksController_DeclaresReviewAutomationContracts" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReviewBulkApproveJobTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after stopping the running WebApi that locked DLLs.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after stopping the running Worker that locked DLLs.
- Restarted local WebApi and Worker with the existing scripts. WebApi returned `HTTP 401` at `http://localhost:5260/api/auth/context`, frontend returned `HTTP 200` at `http://localhost:5173`, and Worker logs showed BidOps recurring tasks starting successfully.

## 2026-06-30 BidOps Lifecycle Project Code Edit And Explicit Extraction

Completed:

- Added explicit extraction support for `采购项目编号：SD26-FWSQ-KJ-JN02` style values, including hyphenated project codes and full-width dash/slash variants.
- Changed lifecycle rematch project-code resolution so an explicit project/procurement code in the award/result notice text or extracted attachment text is checked before historical lifecycle fields, lot numbers, or attachment filenames.
- Added `POST /api/bidops/lifecycle/debug/links/{linkId}/project-code` to manually update a lifecycle row's project code, optionally syncing related pending rows under the same award/result RawNotice and clearing stale 前置公告 links.
- Added the closure-page “修改项目编号” action to the “本次闭环公告” header. Applying the dialog updates all pending rows under the same award/result RawNotice by default, then refreshes the list so the project-code column changes together.
- The “本次闭环公告” header now shows `当前项目编号` next to the announcement label. Project-code edits now update every lifecycle detail row under the same award/result RawNotice, including rows that were already confirmed or rejected, because the code is announcement-level metadata.
- Manual project-code edits now take precedence over award/result notice auto-extraction during lifecycle list enrichment and 前置公告 rematch. Existing rows with the `项目编号手动改为 ...` audit remark are honored, and new edits also persist `manualProjectCodeOverride` into `EvidenceJson`.
- Fixed a refresh regression where lifecycle reparse preserved the manual remark but rebuilt `EvidenceJson` / `ProjectCode` from lot-number evidence, causing rows such as `06FA03-...` to display `06FA03` again after the operator had corrected the announcement code to `SD26-FWSQ-KJ-JN02`. Manual remarks are now parsed for the latest corrected code and reapplied after closure refresh.
- Repaired local lifecycle rows for award RawNotice `328207478753988608`: 17 rows whose manual remark already said `SD26-FWSQ-KJ-JN02` but whose `ProjectCode` was `06FA03` were updated back to `SD26-FWSQ-KJ-JN02`.
- Investigated dirty lifecycle detail rows for RawNotice `328207478753988608`. The root cause was a second, weak PDF table parse path: the clean `OutcomeSupplierRecord` rows were 17, but `MergeAwardEvidence` appended 11 unmatched attachment rows where the PDF extractor had shifted columns into values like `lotNo=业形象及文化宣传`, `lotName=包`, and truncated supplier names. Reviewer-prompt outcome reparse also kept one deterministic fallback duplicate with evidence `务 包 1 山东资德会计师事务所`.
- Hardened lifecycle award-evidence merge so unmatched parsed PDF rows are appended only when they have a real explicit lot number or strong standalone amount/package evidence, then pruned same-lot/same-package supplier-name fragments such as `山东资德会计师事务所` when a fuller `山东资德会计师事务所(普通合伙)` row exists. Also hardened outcome supplier merge so short lot-name fragments are pruned when a full explicit-lot row exists for the same package/supplier.
- Repaired local data for RawNotice `328207478753988608`: deleted 11 weak PDF lifecycle rows plus one stale same-package supplier-fragment lifecycle row from `bidops_lifecycle_package_link`, and deleted one weak duplicate outcome supplier row from `bidops_outcome_supplier_record`.
- Added a shared non-award outcome policy for `流标状态` / `流标` / `废标` / `采购失败` result rows. Such rows are retained as outcome display records with `OutcomeType=Failed`, but their award/service-fee amounts are cleared, they are excluded from supplier master sync, lifecycle closure construction, lifecycle confirmation, field-enrichment amount writes, and amount-candidate selection.
- Added the review-page outcome type option “流标/失败” for manual display rows.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsEvidenceText_ExtractsExplicitProcurementProjectCodeWithHyphenSegments|BidOpsReverseLifecycleClosureService_PrefersExplicitProjectCodeBeforeLotNumber|BidOpsReverseLifecycleClosureService_DerivesProjectCodeFromAwardAttachmentFileName|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded after stopping the prior local WebApi process that held `Atlas.Modules.BidOps.dll`.
- Restarted local WebApi with `dotnet run --project src\Atlas.WebApi\Atlas.WebApi.csproj --launch-profile bidops-local --no-build`; `http://localhost:5173/` returns 200 and the WebApi process is running on the local profile.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded, and the local Worker was restarted on the `bidops-local` profile so background reparse jobs use the same extraction fix.
- Re-ran `npm run typecheck`, `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsReverseLifecycleClosureService_PrefersExplicitProjectCodeBeforeLotNumber" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`, and rebuilt/restarted WebApi + Worker after moving the project-code edit to the announcement-level UI.
- Re-ran the same frontend typecheck and backend focused tests after making project-code edits apply to all same-award lifecycle detail rows, then rebuilt and restarted WebApi + Worker.
- Re-ran focused regression tests for manual project-code preservation, frontend typecheck, rebuilt WebApi + Worker, and restarted local services after fixing manual override priority.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_PreservesManualProjectCodeOverrideDuringReadEnrichment|BidOpsReverseLifecycleClosureService_PreservesManualProjectCodeWhenPersistingRefreshedClosure|BidOpsReverseLifecycleClosureService_PrefersExplicitProjectCodeBeforeLotNumber|BidOpsReverseLifecycleClosureService_DerivesSixCharacterProjectCodeFromLotNo" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DropsWeakParsedPdfRowsWhenOutcomeRecordsExist|BidOpsReverseLifecycleClosureService_DropsSupplierFragmentDuplicateWithSameLotAndPackage|BidOpsOutcomeSupplierExtractionService_PrunesShortLotNameFragmentsWhenFullPackageRowsExist|BidOpsOutcomeSupplierExtractionService_PrunesWrappedAwardTableFragmentsWhenFullPackageRowsExist|BidOpsReverseLifecycleClosureService_PreservesManualProjectCodeWhenPersistingRefreshedClosure" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DoesNotBuildClosureForFailedOutcomeStatus|BidOpsReverseLifecycleClosureService_ClearsFailedOutcomeAmountForDisplayEvidence|BidOpsAmountCandidateService_FiltersFailedOutcomeSupplierCandidatesForDisplay|BidOpsReverseLifecycleClosureService_DropsWeakParsedPdfRowsWhenOutcomeRecordsExist|BidOpsReverseLifecycleClosureService_DropsSupplierFragmentDuplicateWithSameLotAndPackage" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 5 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin` after adding the “流标/失败” manual outcome option.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- Local DB recheck for RawNotice `328207478753988608` after cleanup: `bidops_lifecycle_package_link` has 16 rows, `bidops_outcome_supplier_record` has 16 rows, weak PDF rows are 0, and the exact weak evidence text `务 包 1 山东资德会计师事务所` is no longer present in lifecycle evidence.
- Local DB recheck found no existing `流标状态` / `流标` / `废标` outcome rows, lifecycle rows, or amount candidates requiring cleanup.

## 2026-06-30 BidOps Lifecycle Batch Clear Final Amount

Completed:

- Added a lifecycle closure batch action to clear selected rows' `FinalAwardAmount` / `FinalAwardAmountSource` without confirming or rejecting the lifecycle link.
- Clearing final amount also revokes any selected amount candidate for those links and restores each candidate to its rule-based status. Agency/service-fee candidates return to `Rejected` with the original "不是中标/成交金额" reason.
- Hardened amount candidate selection so agency fees, budgets, ceilings, deposits, unit prices, and rates cannot be directly selected as final中标/成交金额. Operators must first mark a candidate as an actual winning/deal/quote amount before it can be adopted.
- Added the closure-page “清空金额” batch button for selected pending review rows with existing final amount/source values.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsAmountCandidateExtractor_ClassifiesMoneyCandidates" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 8 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-30 BidOps Lifecycle 23FEA1 Rematch Fix

Completed:

- Reproduced the lifecycle candidate endpoint for link `329867278139133957` returning unrelated candidates before the fix; frontend was correctly filtering them as non-current project-code rows.
- Confirmed the public SGCC request `firstPageMenuId=2018032900295987` with `purOrgCode=23FEA1` returns the single吉林长春前置公告.
- Found the backend root cause: historical lifecycle rows could store `ProjectCode=包`, and project-code resolution accepted that Chinese package label before checking award attachment evidence such as `23FEA1 成交结果公告`.
- Hardened project-code normalization so only ASCII project/procurement code forms are valid for rematch, causing invalid stored package labels to fall through to attachment/outcome evidence and resolve `23FEA1`.
- Added a backend candidate出口过滤 layer so SGCC candidates with a returned `code` different from the current project/procurement code cannot reach the page.
- Matched the SGCC `noteList` precise-search payload to the public portal request shape used for `purOrgCode` searches.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DerivesProjectCodeFromAwardAttachmentFileName|BidOpsReverseLifecycleClosureService_FiltersProcurementCandidatesWithDifferentReturnedCode|StateGridEcpCrawler_BuildsNoticeListPayloadWithProjectCodeField" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- Restarted local WebApi with `dotnet run --project src\Atlas.WebApi\Atlas.WebApi.csproj --launch-profile bidops-local --no-build`; Vite remained available on `http://localhost:5173`.
- Authenticated smoke for `GET /api/bidops/lifecycle/debug/links/329867278139133957/procurement-candidates` through both `http://localhost:5260/api` and `http://localhost:5173/api` returned exactly one candidate: `projectCode=23FEA1`, menu `2018032900295987`, notice `2603235010183212`, title `国网吉林电力吉林省长春电力勘测设计院有限公司2026年第一次服务授权竞争性谈判采购`.

## 2026-06-30 BidOps Lifecycle Source Notice Rematch

Completed:

- Added a “重新匹配前置公告” action for lifecycle closure pages where a 前置公告 is already linked but visibly wrong.
- Reused the existing State Grid candidate search and import/associate endpoint; no duplicate crawler or parser path was added.
- Added an explicit `ApplyToRelatedLinks` request switch so rematch can replace the same wrong `ProcurementRawNoticeId` across unconfirmed/unrejected rows under the same award/result RawNotice.
- Renamed the prior source-notice parsing button to “重新解析内容” so operators can distinguish fixing the match from fixing extracted package/amount evidence.
- Tightened State Grid source-notice search so `purOrgCode` exact project-code lookup runs on the tender and procurement columns first. Lot-number evidence like `23FEA1-9012006-0001` is normalized to `23FEA1`, and keyword fallback now runs only when exact project-code lookup returns no candidates.
- Added a returned-code guard to the source-notice candidate search: if the current project code is `23FEA1`, candidates whose SGCC `code` is not `23FEA1` are discarded even if the remote endpoint returns a default or unrelated list.
- Cleared the lifecycle closure candidate table before each new 前置公告 search request, so a failed or stale search cannot keep showing the previous project's candidates under the new project-code banner.
- Added a frontend cache-buster and second-pass project-code filter for 前置公告 candidate search. Even if the browser receives a cached or stale response, candidates whose displayed `projectCode` does not match the current search code are removed before rendering.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 1 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "StateGridEcpCrawler_BuildsNoticeListPayloadWithProjectCodeField" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 1 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:IntermediateOutputPath=... -p:OutDir=...` succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run typecheck` succeeded again after adding the frontend cache-buster and candidate-code filter.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded after restarting the previously running WebApi process that locked `Atlas.Modules.BidOps.dll`.
- Local WebApi was restarted with `dotnet run --project src\Atlas.WebApi\Atlas.WebApi.csproj --launch-profile bidops-local --no-build`; `http://localhost:5260/swagger/index.html` returns the expected login redirect and `http://localhost:5173/` returns 200.
- SGCC public noteList smoke confirmed `purOrgCode=23FEA1` returns 0 rows from `2018032700291334` and 1 exact row from `2018032900295987`: `国网吉林电力吉林省长春电力勘测设计院有限公司2026年第一次服务授权竞争性谈判采购`.
- Authenticated local API smoke against `rawNoticeId=328160928082300928` returned 14 lifecycle rows with `projectCode=23FEA1`; `GET /api/bidops/lifecycle/debug/links/{linkId}/procurement-candidates` returned exactly one candidate with `projectCode=23FEA1`, menu `2018032900295987`, and the吉林长春 title above.
- Re-ran the authenticated candidate API after the returned-code guard: `non-23FEA1 candidates=0`, candidate count remains 1, and the only candidate is the吉林长春 `23FEA1` notice.

## 2026-06-29 BidOps Entity Chinese Documentation Comments

Completed:

- Added XML Chinese summaries for BidOps database entity classes and mapped fields under `src/Atlas.Modules.BidOps/Entities`.
- Added XML Chinese summaries for C# enum members and string-based enum/value classes used by BidOps database fields, including statuses, stages, source kinds, amount candidate types, crawl modes, and lifecycle link types.
- Kept the change documentation-only: no entity property names, types, EF mappings, indexes, migrations, or runtime behavior were changed.

Verification:

- Coverage scan confirmed all public entity/model fields and enum-like values in scope have XML summaries.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.

## 2026-06-29 BidOps Lifecycle Source Notice Reparse

Completed:

- Added a closure-page action to reparse the matched 前置公告 RawNotice from the “对应前置公告” context area.
- Changed 前置公告 “AI提示词辅助解析” to run RawNotice structured reparse with the reviewer prompt, instead of only queuing per-link lifecycle field enrichment.
- Reused the existing attachment extraction and structured parsing pipeline; no duplicate 前置公告 parser was added.
- Extended the attachment-process job result with `structuredParseJobId` so the closure page waits for the actual structured-parse child job before refreshing package/amount evidence.
- Repaired the local BidOps Global DB background-job cancellation columns idempotently, then restarted WebApi and Worker so source-notice reparse jobs can be enqueued and consumed locally.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "AttachmentProcessJobHandler_ReturnsStructuredParseChildJobId|StructuredParseJobHandler_ReturnsJsonResultSummary|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-background-job-cancellation ...` succeeded.
- Local WebApi is listening on `http://localhost:5260`, Vite remains on `http://localhost:5173`, and Atlas.Worker is running with `Environment=BidOpsLocal`.

## 2026-06-29 BidOps Lifecycle Project Code And Source Notice Matching Fix

Completed:

- Fixed lifecycle source-notice matching so `SourceNoticeId=url:*` metadata is not treated as a business project code. The previous normalization could collapse this to `URL`, then infer unrelated 前置公告 from broad `url:` matches.
- Added guarded extraction of 6-character alphanumeric project/batch codes from lot numbers such as `23FEA1-9012006-0001` when explicit `ProjectCode` is blank or only contains metadata.
- Pre-enriched lifecycle link project codes from link lot number and reviewed outcome supplier evidence before source-notice lookup, so inferred 前置公告 search uses the corrected project code.
- Removed raw `SourceNoticeId` as a fallback for award-evidence project-code construction and rejected metadata tokens such as `URL`, `SourceUrl`, `ProjectCode`, and `ListPublishTime`.
- Reused the corrected project-code resolution in single-link source-notice search, import, confirmation, and field-enrichment flows. Award attachment names such as `23FEA1 成交结果公告.pdf` now participate in project-code resolution, so old rows persisted with `ProjectCode=URL` still search State Grid by `23FEA1`.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsJobProjectCode_DoesNotUseMetadataFieldNamesAfterBlankProjectCode|BidOpsReverseLifecycleClosureService_DerivesSixCharacterProjectCodeFromLotNo|BidOpsReverseLifecycleClosureService_KeepsAllVisibleOutcomeAndProcurementCandidates|BidOpsRawNoticeTextFormatter_ConvertsStateGridFieldsToChineseDisplayText" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DerivesSixCharacterProjectCodeFromLotNo|BidOpsReverseLifecycleClosureService_DerivesProjectCodeFromAwardAttachmentFileName|BidOpsReverseLifecycleClosureService_KeepsAllVisibleOutcomeAndProcurementCandidates" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded.

## 2026-06-29 BidOps P1 Closure Detail Amount Candidate Fix

Completed:

- Fixed closure amount-candidate reads returning an empty pool by registering `BidOpsDataResources.AmountCandidate` in the BidOps authorization catalog.
- Hardened amount-candidate upsert idempotency by de-duplicating drafts with `TenantId + SourceHash` and ignoring unique-key races as a read-time backfill no-op.
- Added a deterministic fallback for outcome evidence rows whose source notice table declares `中标服务费（万元）`: values such as `1.7000` are normalized to `17000` CNY and classified as `agency_fee`, not as a final award amount.
- Cleared stale derived amount candidates for local RawNotice `328250250680733696` only, then regenerated them through the normal lifecycle detail API.
- Restarted the local WebApi; it is listening on `http://localhost:5260` behind the existing Vite proxy.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded before WebApi restart.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModule_RegistersAuthorizationCatalog|BidOpsAmountCandidateExtractor|LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsReverseLifecycleClosureService_KeepsAllVisibleOutcomeAndProcurementCandidates" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded: 13 passed.
- Authenticated smoke through `http://localhost:5173` returned 11 lifecycle links and 8 amount candidates for the first detail row of RawNotice `328250250680733696`.
- Smoke confirmed `1.7000` appears as `AmountValue=17000`, `AmountUnit=万元`, `AmountType=agency_fee`, `Status=Rejected`.
- `git diff --check` succeeded.

## 2026-06-29 BidOps P1 Local Amount Candidate Migration Repair

Completed:

- Fixed the P1 tenant migration metadata so `20260629093000_v0.2.21-bidops-amount-candidates` is visible to the Atlas tenant migration planner.
- Added `tools/Atlas.LocalSetup ensure-bidops-amount-candidates` for local BidOps databases that already have legacy tables but no EF migration history.
- Applied the local helper to `atlas_bidops_runtime`, creating `bidops_amount_candidate` and its indexes idempotently.
- Re-ran the failing lifecycle closure URL through the Vite proxy with a fresh local login token; it now returns `HTTP 200` with 11 rows.

Verification:

- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-amount-candidates ...` succeeded.

## 2026-06-29 BidOps P1 Amount Candidate Pool And Evidence Chain

Completed:

- Added the tenant-scoped `bidops_amount_candidate` table and `AmountCandidate` entity for formal amount candidates, including raw/source notice, attachment, source location, context/evidence text, amount type, normalized value, status, reject reason, selection audit fields, and tenant-scoped `SourceHash` idempotency.
- Added rule-based amount candidate extraction for money, percentages, and Chinese discount expressions such as `八五折`; normalization covers `元`, `万元`, `亿/亿元`, `￥/¥`, `%/％`, and discount/rate values.
- Added amount type recognition for winning/deal/quote/budget/ceiling/agency fee/deposit/unit/rate/discount/reduction/unknown. Budget, ceiling, agency fee, deposit, unit price, rate, discount, and reduction candidates are preserved but not treated as final award amounts by default.
- Wired public review detail and lifecycle closure detail to the same `amount_candidate` pool. Public review now returns/displays `AmountCandidates`, and closure rows display all related candidates grouped by `Selected`, `Recommended`, `Candidate`, `Unresolved`, and `Rejected`.
- Added lifecycle debug/operation endpoints for amount candidates: list, debug, select as final amount, mark type, reject, and restore. Selecting a candidate updates the existing `LifecyclePackageLink.FinalAwardAmount` / `FinalAwardAmountSource` fields while keeping only one selected candidate per lifecycle link.
- Replaced the closure detail's split outcome/procurement candidate tables with a unified evidence-chain table showing source file/title, source location, amount type/status, context snippet, and operations.
- Updated product-facing UI wording from generic “采购公告” to “前置公告” in BidOps frontend surfaces touched by this flow. Matching keywords still retain “采购公告” where needed to recognize real public notice text.
- Confirmed no second ZIP parser was added: ZIP-in-ZIP Word/Excel parsing is already implemented in `BidOpsTextExtractor.ExtractZipTextAsync`, which recursively extracts supported inner files into the parent attachment text with `Archive:` / `File:` headers and `ParseError:` diagnostics.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "AmountCandidate|BidOpsAmountCandidateExtractor|LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP1Tests\"` succeeded: 13 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP1WebApi\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP1Worker\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP1TenantMigrations\"` succeeded with 0 warnings and 0 errors.

## 2026-06-29 BidOps P0 Source Notice And Candidate Consistency

Completed:

- Added a rule-based source-notice classifier for award/result notices. Public bidding results now prefer 招标公告 then 投标邀请书 before procurement columns; negotiated/deal results prefer procurement columns first.
- Updated State Grid lifecycle source-notice search to pass classified menu order and sort candidates by exact project-code match plus preferred source-notice type.
- Changed lifecycle closure UI wording from “采购公告” as the generic corresponding notice to “前置公告”, including missing-state and search-dialog copy.
- Extended lifecycle link DTO/read enrichment with project process type, procurement method, source notice type/column, searched-column candidate counts, outcome amount candidates, candidate-notice rows, procurement detail candidates, and existing attachment lists.
- Updated the closure detail drawer to show “中标金额：未确认” when final amount is empty and to display unbound/unconfirmed candidate amount rows instead of hiding them.
- Increased ZIP recursion depth to 5 and kept nested ZIP Word/Excel parsing in the existing text extractor. Inner archive entries now emit file path headers and `ParseError:` lines for broken or unsafe entries instead of being silently ignored.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP0BuildFinal\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsSourceNoticeClassifier_ClassifiesPublicTenderAwardAsBidding|BidOpsSourceNoticeClassifier_ClassifiesNegotiatedDealAsNonBidding|BidOpsTextExtractor_ExtractsNestedZipExcelEntries|BidOpsTextExtractor_RecordsNestedZipParseErrors|BidOpsReverseLifecycleClosureService_KeepsAllVisibleOutcomeAndProcurementCandidates" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP0Tests\"` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsReverseClosureTests|BidOpsTextExtractor_ExtractsHtmlText|BidOpsTextExtractor_ExtractsXlsxWorksheetText|BidOpsTextExtractor_ExtractsGbkZipEntryNames|BidOpsTextExtractor_ExtractsDocxTablesAsMarkdown|BidOpsTextExtractor_ExtractsNestedZipExcelEntries|BidOpsTextExtractor_RecordsNestedZipParseErrors" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP0BroadTests\"` succeeded: 57 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP0WebApiBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsP0WorkerBuild\"` succeeded with 0 warnings and 0 errors.

## 2026-06-29 BidOps Review Page Top/Bottom Jump Controls

Completed:

- Added fixed top/bottom jump controls to the BidOps review task list so batch-review users can move between the selection table, toolbar, and pagination without long manual scrolling.
- Added the same top/bottom jump controls to the lifecycle closure review center for long selected review result sets.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-29 BidOps Reviewer-Prompt Outcome Reparse Fallback

Completed:

- Changed reviewer-prompted outcome supplier reparse selection to merge AI rows with deterministic fallback rows instead of persisting only AI rows.
- Kept AI rows preferred for the same package/lot/outcome/rank so reviewer corrections still override rule-derived values.
- Included lot identity in supplier-level outcome de-duplication so the same supplier and package number in different lots remains as separate result details.
- Restored the pricing guard that leaves ambiguous percentage evidence as manual-review-only instead of defaulting to a procurement package amount.
- Added regression tests for AI-missed rows being restored by deterministic parsing and for same-supplier/same-package rows across different lot names.
- Restarted local BidOps WebApi/Worker and refreshed RawNotice `328161083921666048` for project code `232606` through the lifecycle outcome-supplier reparse action.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsAiCorrectionForSamePackage|BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsDeterministicRowsMissingFromAi|BidOpsOutcomeSupplierExtractionService_ReviewerPromptPreservesAiAnnouncementOrder|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames|BidOpsOutcomeSupplierExtractionService_PrunesWrappedAwardTableFragmentsWhenFullPackageRowsExist" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ReviewerPromptFallbackTests3\"` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsReverseClosureTests|BidOpsOutcomeSupplierExtractionService_" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOutcomeFallbackBroad2\"` succeeded: 71 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOutcomeFallbackBuild\"` succeeded with 0 warnings and 0 errors.
- Local reparse job `329821003494592512` completed with `extractedCount=35`, `savedCount=35`, and `lifecycleRefresh.persistedLifecycleLinkCount=35`.
- Authenticated lifecycle-link API smoke for `rawNoticeId=328161083921666048` returned `total=35`; direct tenant database checks also showed 35 outcome supplier records and 35 lifecycle links.

## 2026-06-28 BidOps Lifecycle Field Enrichment Source Completeness

Completed:

- Hardened lifecycle field-enrichment source construction so long RawNotice/attachment text keeps the document opening plus contextual lines around relevant rows instead of only isolated keyword-matching lines.
- Added dynamic keywords from the current lifecycle link fields, including project code/name, lot, package, and supplier, when selecting relevant source snippets.
- Kept source budgets bounded, but now allocates remaining source budget fairly across the remaining evidence documents during prompt construction.
- Added regression coverage for a procurement table row where the relevant package row needs neighboring non-keyword lines to preserve context.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsLifecycleFieldEnrichmentAiService_SourceBundleKeepsNeighborRowsAroundRelevantRows|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleFieldSourceTests2\"` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsFieldSourceBuild\"` succeeded with 0 warnings and 0 errors.

## 2026-06-27 BidOps Lifecycle Closure Notice Context Layout

Completed:

- Moved lifecycle closure award/procurement notice context to the top of `闭环任务与审核中心`.
- Removed the repeated `采购公告` column from the lifecycle package-link detail table so rows focus on award details such as lot, package, supplier, amount, match score, status, and review actions.
- Kept procurement-notice search available from the top context when the current closure has not linked a procurement notice.
- Added current-filter notice de-duplication in the frontend so one award/procurement notice is shown once for the closure context, with a warning when the current filtered list spans multiple notices.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded.
- Backend build/tests were not rerun because the change is frontend layout-only and does not alter API contracts or C# code.

## 2026-06-27 BidOps Lifecycle Field-Level AI Enrichment

Completed:

- Added a field-level lifecycle AI enrichment service that outputs structured field suggestions with source stage, source RawNotice/attachment, evidence text, confidence, reason, conflicts, and manual-review flag.
- Added the `bidops.lifecycle.field-enrichment` Worker job type and payload. The job collects award/candidate/procurement public evidence, invokes the current runtime AI provider or Codex CLI, writes the result to `LifecyclePackageLink.EvidenceJson.fieldEnrichment`, and only fills missing suggestion fields.
- Added `POST /api/bidops/lifecycle/debug/links/{linkId}/field-enrichment/enqueue` for queuing automatic or reviewer-prompt enrichment.
- Added lifecycle center UI actions for `AI补全` and `提示词` enrichment, plus a detail-section table showing the latest field suggestions, evidence, confidence, and conflicts.
- Kept award/result evidence as the highest-priority source for amount and other fields. Procurement-side budget/max-price/guide-price values are treated as review-required suggestions, not confirmed award amounts.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModule_RegistersServicesAndBackgroundHandlers|ReviewTasksController_DeclaresOutcomeAiReparseContract|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleFieldEnrichmentTests\"` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleFieldEnrichmentWebApi\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleFieldEnrichmentWorker\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-27 BidOps Lifecycle Supplier Name Hardening

Completed:

- Fixed supplier-name cleanup so a valid company prefix such as `四川` is not interpreted as an ordinal/rank prefix and stripped to `川`.
- Hardened lifecycle award evidence merging to treat one- or two-character prefix truncations as compatible only when the rest of the supplier name matches.
- Updated lifecycle outcome-context matching to prefer exact row evidence text before supplier-name comparison, so lifecycle links can be corrected from existing `OutcomeSupplierRecord` evidence even when the stored link supplier name is truncated.
- Updated lifecycle link read-time enrichment to backfill the displayed supplier name from the matched outcome record, not just lot/package fields.
- Added regression coverage for the observed `四川利安易昂科技有限公司` paragraph evidence and for correcting a truncated lifecycle-link supplier from matching outcome evidence.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsAwardEvidenceParser_DoesNotStripChineseNumberProvincePrefix|BidOpsReverseLifecycleClosureService_EnrichesLifecycleLinkLotContextByEvidenceText|BidOpsAwardEvidenceParser_ExtractsParagraphPackageSupplier" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleSupplierHardeningTests\"` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleSupplierHardeningReverseClosureAll\"` succeeded: 42 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleSupplierHardeningWebApi\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleSupplierHardeningWorker\"` succeeded with 0 warnings and 0 errors.

## 2026-06-27 BidOps Lifecycle Review Table Sorting And Lot Enrichment

Completed:

- Replaced the lifecycle closure center's standalone sort dropdown with table-header sorting on the commonly used review columns.
- Added header sorting for project code, lot number, lot name, package number, supplier, award amount, match score, status, manual-review flag, and update time.
- Investigated `RawNoticeId=328339628681728000` / review task `328547208658030783`. Local data showed `bidops_lifecycle_package_link` had 160 rows with empty stored lot number/name, while `bidops_outcome_supplier_record` had 251 rows with populated lot names and matching row evidence text, and `bidops_package_staging` had 191 rows with lot number/name context.
- Updated lifecycle link DTO enrichment to match historical link `EvidenceJson.award.evidence.evidenceText` against outcome supplier `EvidenceText` before falling back to supplier/package matching.
- Added read-time display-context sorting for `rawNoticeId` filtered lifecycle pages, so historical rows with empty stored lot fields can still sort by the enriched lot number/name shown in the table.
- Added a regression test for same-supplier/same-package historical rows so the correct lot name is selected from evidence text.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_EnrichesLifecycleLinkLotContextByEvidenceText|BidOpsReverseLifecycleClosureService_BuildsOutcomeAwardEvidenceWithReviewPackageLotContext" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleEvidenceTextEnrichTests3\"` succeeded: 2 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after rerunning separately. A prior parallel WebApi/Worker build hit a transient shared `obj` file lock.
- `git diff --check` succeeded with the existing EF snapshot line-ending warning.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker restarted as process `18204`.

## 2026-06-27 BidOps Lifecycle Procurement Notice Search

Completed:

- Added State Grid ECP public notice search by project/procurement code through the existing WCM `index/noteList` API with `key=<projectCode>`.
- Search covers the verified SGCC 招标公告及投标邀请书 menu `2018032700291334` and 采购公告 menu `2018032900295987`, then filters lifecycle procurement candidates to `doci-bid` notices.
- Added lifecycle debug APIs to search procurement candidates for a lifecycle link and import/link a selected candidate.
- Linked an already-collected procurement RawNotice directly to `ProcurementRawNoticeId`; otherwise the selected candidate queues the existing Worker-backed manual URL import job.
- Added frontend types/API wrappers and UI actions in `闭环任务与审核中心`: missing procurement notices can be searched from the table or detail drawer, candidates show project code, notice type, publish org/time, local RawNotice status, and support open/import/link actions.

Verification:

- Direct SGCC API probe confirmed project `282602` returns the base procurement/tender notice under menu `2018032700291334` with title `国网青海省电力公司2026年第二次（282602）物资公开招标采购`; the screenshot menu `2018032900295987` returned no match for that project.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsModule_RegistersServicesAndBackgroundHandlers|StateGridEcpWcmParser_ParsesNoticeListAndDetail" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Initial normal WebApi/Worker builds hit Windows DLL locks from running local `Atlas.WebApi` and `Atlas.Worker` processes. After stopping those processes, `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings and 0 errors.
- After stopping the running Worker, `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker from their built DLLs with `BidOpsLocal` environment. `GET http://localhost:5260/api/auth/context` returned `401 Unauthorized` as expected for an unauthenticated probe, and `http://localhost:5173/` returned `200`.

## 2026-06-25 BidOps Mimo AI Provider

Completed:

- Added `Mimo` as a BidOps AI provider value alongside DeepSeek and Codex CLI.
- Extended the OpenAI-compatible HTTP settings factory to resolve Mimo endpoint, model, and credential configuration from `BidOps:Mimo:*` or `MIMO_API_KEY`.
- Protected Mimo runtime switching from accidentally inheriting existing DeepSeek-compatible generic `BidOps:Ai:BaseUrl`, `BidOps:Ai:Model`, or `BidOps:Ai:ApiKey` values unless the configured provider is also Mimo.
- Exposed Mimo in the operations dashboard AI provider options through the existing backend settings DTO, so the frontend switch can select it without a separate hardcoded UI option.
- Added non-secret local Mimo base URL/model defaults to WebApi and Worker BidOps local appsettings. The provided token was not written to source-controlled files.
- Added provider/endpoint scoped HTTP pacing for OpenAI-compatible AI calls. Mimo defaults to a 15-second minimum request interval and a 180-second backoff after `HTTP 429`.
- Documented the Mimo provider, credential location, and Token Plan usage assumption in BidOps operations/decision docs.

Verification:

- `Get-Content src\Atlas.WebApi\appsettings.BidOpsLocal.json -Raw | ConvertFrom-Json | Out-Null` succeeded.
- `Get-Content src\Atlas.Worker\appsettings.BidOpsLocal.json -Raw | ConvertFrom-Json | Out-Null` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_UsesMimoProviderSettings|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoProviderTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiMimoBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerMimoBuild\"` succeeded with 0 warnings and 0 errors.
- `git diff --check` succeeded.
- A direct Xiaomi Mimo Token Plan smoke request to `/v1/chat/completions` returned `HTTP 200` for model `mimo-v2.5-pro`.
- Authenticated WebApi smoke as `bidops_admin` confirmed `GET /api/bidops/operations/ai-settings` reports `EffectiveProvider=Mimo`, `EffectiveModel=mimo-v2.5-pro`, and the Mimo option is available.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_UsesMimoProviderSettings|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoRateLimitTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoRateLimitBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiMimoRateLimitBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerMimoRateLimitBuild2\"` succeeded with 0 warnings and 0 errors. A prior parallel build attempt hit a transient Windows `obj` file lock.

Local restart note:

- Stopped the stale local WebApi/Worker process tree, rebuilt WebApi and Worker sequentially into their normal `bin\Debug\net8.0` outputs, and started both executables directly to avoid `dotnet run` rebuild locks.
- Started WebApi and Worker with the Mimo credential only in process environment variables. The token was not written to appsettings or docs.
- Temporarily paused BidOps jobs after earlier Mimo `429 Too many requests` responses, then resumed after the Mimo direct smoke succeeded and Worker-side pacing was enabled.
- Worker completed live Mimo extraction after resume: structured parsing returned `statusCode=200` with package counts, and outcome supplier extraction returned `statusCode=200` with record counts. No new `429` appeared during the observation window.
- Final authenticated dashboard smoke showed `EffectiveProvider=Mimo`, `MimoAvailable=true`, `TaskPaused=false`, `PendingJobs=267`, `RunningJobs=6`, `FailedJobs=0`, and `DeadJobs=8`.

## 2026-06-25 BidOps Lifecycle Closure UI

Completed:

- Added paged lifecycle-link search through `GET /api/bidops/lifecycle/debug/links`, with filters for keyword, procurement number, lot, package number, supplier, link status, match type, manual-review flag, raw notice id, and sort order.
- Exposed `UpdatedAt` on `LifecyclePackageLinkDto` for operator sorting and list display.
- Added the `/bidops/outcomes` lifecycle closure page, replacing the previous result-entry placeholder.
- Added UI actions to enqueue award-driven reverse closure from a public award URL or RawNoticeId, generate persisted suggestions for an already collected RawNotice, inspect evidence JSON, and confirm or reject suggested lifecycle links.
- Added a front-end API wrapper and TypeScript DTO/request types for lifecycle closure operations.
- Updated the BidOps results-center menu entry to `生命周期闭环`.
- Moved the primary `分析闭环` action to the formal notice list and new notice detail page. The action is shown only for result-like notices and passes the linked `RawNoticeId` to the Worker-backed lifecycle reverse-closure job.
- Added `GET /api/bidops/notices/{id}` and the `/bidops/notices/:id` detail page so operators can launch lifecycle analysis from a specific result notice without re-entering the public URL.
- Reframed `/bidops/outcomes` as `闭环任务与审核中心`, removing the free-form URL/RawNotice trigger panel and keeping it for progress inspection, lifecycle package suggestions, evidence JSON, missing/failure reasons, and manual confirm/reject.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsLifecycleUi2\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiLifecycleUi\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleUiTests\"` succeeded: 1 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsLifecycleEntry\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiLifecycleEntry\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsNoticeListContracts_ExposeNoticeTypeFiltersAndUpdatedAt|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleEntryTests3\"` succeeded: 2 passed. A prior attempt hit a transient Windows file lock while WebApi was running; rerun after stopping WebApi succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin` after moving the lifecycle entry.

## 2026-06-24 BidOps Award-Driven Reverse Lifecycle Closure

Completed:

- Extended lifecycle reverse closure output with structured amount semantics, including amount kind, source stage, base amount, rate type/value, formula, confidence, evidence, and manual-review flag.
- Added conservative rate parsing for discount rate, reduction rate, coefficient, and ambiguous bare percentages.
- Added package guide-price extraction to procurement/tender evidence parsing.
- Added `BidOpsPricingInferenceService` so direct award amount, candidate final quote, and rate-based inferred amount selection are tested independently from orchestration.
- Added `BidOpsNoticeCorrelationService` to score candidate/procurement notices by project code, project name, lot/package hints, supplier evidence, notice type, and publish-time sequence, returning score, confidence level, reasons, and missing reasons.
- Extended `BidOpsReverseLifecycleClosureService` to emit structured failure reasons, use targeted raw-notice metadata lookup, persist suggested lifecycle links idempotently, and preserve confirmed manual links.
- Added Worker-backed lifecycle reverse-closure job support plus lifecycle debug API endpoints for enqueue, persist, confirm, and reject.
- Registered the lifecycle link data resource, pricing service, and reverse-closure job handler in the BidOps module.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseClosureTests|BidOpsModule_RegistersServicesAndBackgroundHandlers|BidOpsModule_RegistersAuthorizationCatalogEntries" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosurePlanTests\"` succeeded: 39 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureWideTests\"` succeeded: 166 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureWebApiBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureWorkerBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureLocalSetupBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore -- inspect-bidops-sgcc-notices --url "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522123684_2018060501171111" --url "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606028335014767_2018032900295987" --package-take 5` succeeded. The `doci-win` sample returned project code `0711-26OTL04213025`; the `doci-bid` sample downloaded one ZIP attachment and extracted 28 package rows, all with amount references, plus 49 requirement rows.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded.

## 2026-06-24 BidOps Local AI Worker Concurrency Observation

Completed:

- Increased local `BidOpsLocal` Worker total one-time-job concurrency from `6` to `8`.
- Increased local AI parsing job-type caps from two concurrent `bidops.ai.structured-parse` and two concurrent `bidops.outcome.supplier-extract` jobs to three of each.
- Kept `bidops.crawl.state-grid-ecp-scan` capped at `1`.

Verification:

- `Get-Content src\Atlas.Worker\appsettings.BidOpsLocal.json -Raw | ConvertFrom-Json` succeeded.
- Restarted the local `BidOpsLocal` Worker after temporarily enabling the BidOps runtime pause switch, then restored the switch to `{"paused":false,"reason":"","deferredUntil":null}`.
- Runtime verification after restart showed three concurrent `bidops.ai.structured-parse` jobs and one `bidops.crawl.state-grid-ecp-scan` job; process inspection showed three Codex CLI child processes.
- Recovered one background job that was left `Running` by the brief pause/restart window back to `Pending` without consuming an attempt.

## 2026-06-23 BidOps Local Codex Worker Concurrency

Completed:

- Increased local BidOps Worker total one-time-job concurrency from `4` to `6`.
- Increased local AI parsing job-type caps from one concurrent `bidops.ai.structured-parse` and one concurrent `bidops.outcome.supplier-extract` to two of each.
- Kept `bidops.crawl.state-grid-ecp-scan` capped at `1` to avoid aggressive public-source crawling.
- Added generic Worker `IncludedJobTypes` / `ExcludedJobTypes` configuration so extra machines can consume only selected job types, for example BidOps AI parsing jobs.
- Documented an AI-only BidOps Worker configuration example and the cross-machine requirements for Snowflake node ids, shared storage, and AI provider setup.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "Worker_OnlyClaimsIncludedJobTypes|Worker_RespectsJobTypeConcurrencyLimitAndFillsOtherWork|Worker_ProcessesConfiguredConcurrencyInParallel" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerJobTypeFilters\"` succeeded: 3 passed.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundJobTypeFiltersBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `Get-Content src\Atlas.Worker\appsettings.BidOpsLocal.json -Raw | ConvertFrom-Json` succeeded.
- Restarted the local `BidOpsLocal` Worker after temporarily enabling the BidOps runtime pause switch, then restored the switch to `{"paused":false,"reason":"","deferredUntil":null}`.
- Runtime verification after restart showed four concurrent BidOps AI jobs: two `bidops.ai.structured-parse` and two `bidops.outcome.supplier-extract`.

## 2026-06-23 Frontend List Query Persistence

Completed:

- Added optional `localStorage` query persistence to the shared `useTableQuery` composable.
- Enabled cached filters for the BidOps review-task list (`待审核池`), so refresh and returning from detail pages restore the last searched conditions.
- Added cached filters for the background-job list, with separate storage keys for general operations jobs and BidOps jobs.
- Changed persistence timing so typing into fields does not update the cache; the cache is written only after explicit `查询` and reset to defaults after `重置`.
- Kept explicit route query parameters as a higher-priority source than cached values for background-job deep links.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-23 Background Job Completed-Time Sorting

Completed:

- Added `SortBy` and `SortDescending` to background job operations search queries.
- Added backend ordering support for `CompletedAt` / `CompletedAtUtc`, keeping incomplete jobs after completed jobs while preserving the existing default ordering when no sort is requested.
- Added the `完成时间` column to the operations background-job list and wired Element Plus custom table sorting to the backend query.
- Added a regression test proving explicit completed-time sorting overrides the default running/pending priority order.
- Updated the background task guide with the completion-time sort query contract.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_SortsByCompletedAtWhenRequested|SearchAsync_ReturnsChineseJobTypeNameAndLocalTimeAliases" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundCompletedAtSortTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundCompletedAtSortBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Restarted local WebApi with the `bidops-local` launch profile. WebApi returned `HTTP 401` for unauthenticated `GET /api/auth/context`, and the frontend returned `HTTP 200` at `http://localhost:5173/`.
- `git diff --check` completed with only the existing line-ending warning for `src/Atlas.Data.Tenant.Migrations/Migrations/AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-23 BidOps Background Job Backlog Parallelization

Completed:

- Investigated the local BidOps queue and found `6888` Pending jobs, dominated by `6669` `bidops.document.attachment-process` rows.
- Confirmed the attachment backlog represented only `202` distinct RawNotice ids; `6467` of those attachment jobs were duplicate Pending rows caused by minute-based enqueue deduplication keys.
- Confirmed scheduled State Grid scan backlog represented repeated channel/checkpoint states, for example Backfill jobs for the same channel and cursor.
- Added generic one-time Worker concurrency settings: `MaxConcurrency` and per-job-type `JobTypeConcurrency`.
- Updated the Worker execution loop to keep active jobs and fill free slots while long-running jobs continue, so a slow AI parse no longer blocks quick attachment jobs from later polling ticks.
- Kept default Worker concurrency at `1`; set `BidOpsLocal` to global concurrency `4` with `bidops.ai.structured-parse`, `bidops.outcome.supplier-extract`, and `bidops.crawl.state-grid-ecp-scan` capped at `1`.
- Changed automatic attachment-process deduplication to use `TenantId + RawNoticeId + ContentHash` instead of current-minute keys.
- Changed scheduled/manual scan deduplication to use channel/checkpoint progress state instead of current-minute keys.
- Locally canceled duplicate Pending backlog rows without deleting history or business data: first pass canceled `6245` duplicate attachment jobs and `99` duplicate State Grid scan jobs.
- After starting the stable-key Worker once, canceled a second transition batch that preferred the new stable keys over old minute keys: `48` duplicate attachment jobs and `2` duplicate State Grid scan jobs.
- After cleanup and restart, local Pending BidOps jobs were reduced from `6888` to `191`. Pending attachment-process was cleared to `0`; the remaining backlog was mainly `188` structured-parse jobs and `3` State Grid scan jobs, with `0` duplicate Pending attachment groups.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "Worker_ProcessesConfiguredConcurrencyInParallel|Worker_RespectsJobTypeConcurrencyLimitAndFillsOtherWork|Worker_ProcessesHigherPriorityPendingJobFirst|Worker_CancelsRunningJobWhenTerminationIsRequested|Worker_DefersJobWhenExecutionGateBlocksWithoutConsumingAttempt" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerConcurrencyTests\"` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "Worker_ProcessesConfiguredConcurrencyInParallel|Worker_RespectsJobTypeConcurrencyLimitAndFillsOtherWork|Worker_ProcessesHigherPriorityPendingJobFirst|Worker_CancelsRunningJobWhenTerminationIsRequested|Worker_DefersJobWhenExecutionGateBlocksWithoutConsumingAttempt|Worker_MarksActiveJobDeadWhenItExceedsMaxRunningTime" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerDynamicConcurrencyTests\"` succeeded: 6 passed.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundConcurrencyBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundDynamicConcurrencyBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsDedupeBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 2 transient file-copy retry warnings and 0 errors while another build had briefly held shared outputs.
- After stopping WebApi to release locked output files, `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- After stopping WebApi to release locked output files, `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- After the dynamic slot-filling change, `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- After restarting WebApi to load the latest shared assemblies, `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi returned `HTTP 401` for unauthenticated `GET /api/auth/context`; frontend returned `HTTP 200`; Worker logs showed 4-way attachment processing batches and one structured-parse AI job running under the job-type concurrency cap.

## 2026-06-23 BidOps Task Procurement Number Search

Completed:

- Added explicit `projectCode` filtering to BidOps review-task search and kept keyword search compatible with procurement-number lookups.
- Enriched review-task matching through notice staging, procurement detail staging, and outcome/candidate supplier rows so `采购编号` can find tasks even when it was recognized from attachment/result evidence.
- Added `projectCode` to background-job search DTOs, list/detail DTOs, and operations UI columns.
- Added optional `projectCode` to RawNotice-related BidOps job payloads and propagated it through manual import, attachment processing, structured parsing, outcome supplier reparse, progress heartbeats, and final job results.
- Updated review-task list/detail and background-job list/detail UI labels from generic `项目编码` to product-facing `采购编号` where relevant.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchAsync_FiltersAndMapsBidOpsProjectCode|ReviewTaskSearchQuery_ExposesReviewQualityFilters|BidOpsNoticeListContracts_ExposeNoticeTypeFiltersAndUpdatedAt|ReviewTasksController_DeclaresOutcomeAiReparseContract" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodeTasksTests2\"` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "StructuredParseJobHandler_ReturnsJsonResultSummary|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|BidOpsCodexCliClient_WritesOutputSchemaWithoutUtf8Bom" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodePayloadTests\"` succeeded: 3 passed.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodeBackgroundTasksBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodeBidOpsBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodeWebApiBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ProjectCodeWorkerBuild2\"` succeeded with 0 warnings and 0 errors after rerunning alone; an earlier parallel WebApi/Worker build hit a transient shared `obj` cache file lock.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-23 BidOps Ordinal-Prefixed Outcome Lot Evidence Fix

Completed:

- Fixed outcome supplier persistence so evidence rows like `1 10FM03-9001006-0111 包 1 江苏科能岩土工程有限公司` can fill `LotNo` when the source has an explicit分标编号/包号 result table.
- Added a regression test for ordinal-prefixed public result evidence.
- Extended the local data-quality repair command to support MySQL 5.6-compatible ordinal-prefixed evidence repair and to downgrade stale lifecycle conflict issues when the current row is no longer a multi-package match.
- Repaired local tenant `300001`: filled missing outcome `LotNo` values from public evidence, refreshed review quality, and confirmed review task `327561955894235180` no longer has the `多个采购包件` warning.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_FillsLotNoFromOrdinalPrefixedOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_FillsLotNoFromLeadingOutcomeEvidence|BidOpsReviewQualityEvaluator_MatchesOutcomePackageByLotNoAndPackageNoWhenPackageNoRepeats|BidOpsReviewQualityEvaluator_MatchesOutcomePackageByLotNameAndPackageNo" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 4 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOrdinalLotEvidence\"` succeeded with 0 warnings and 0 errors.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LocalSetupOrdinalLotRepairFinal\"` succeeded with 0 warnings and 0 errors.
- Local dry-run/confirm repair runs completed; review task `327561955894235180` now has `ConflictWarnings=0` for the `多个采购包件` message and quality summary `QualityScore=70`, `QualityIssueCount=1`, `HighRiskIssueCount=1`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` and `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- WebApi and Worker were restarted after temporarily pausing BidOps task execution. WebApi returned `HTTP 401` for unauthenticated `/api/auth/context`; Worker resumed the `bidops` queue after the pause setting was restored.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` completed with only the existing line-ending warning for `src/Atlas.Data.Tenant.Migrations/Migrations/AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-23 BidOps Manual Task Priority

Completed:

- Added BidOps background job priority constants and set operator-triggered jobs to priority `100`.
- Kept automatic/scheduled jobs at the default priority `0`, relying on the existing Worker `Priority DESC` ordering.
- Propagated parent job priority to attachment-processing and structured-parse child jobs so manual chains remain prioritized.
- Promoted BidOps-only manual retry jobs to priority `100`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "RetryAsync_BidOpsOnlyPromotesManualRetryPriority|Worker_ProcessesHigherPriorityPendingJobFirst" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsManualTaskPriority\"` succeeded with 0 warnings and 0 errors.
- After stopping the running local WebApi/Worker processes, `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` and `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Local WebApi and Worker were restarted with `--no-build`; WebApi returned `HTTP 401` for unauthenticated `/api/auth/context`, confirming the listener is responding, and Worker began processing BidOps jobs.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` completed with only the existing line-ending warning for `src\Atlas.Data.Tenant.Migrations\Migrations\AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-23 BidOps Codex Scenario Reasoning Policy

Completed:

- Changed the default Codex CLI reasoning effort from `medium` to `low` for ordinary BidOps notice/result extraction.
- Added scenario-scoped Codex CLI settings for ordinary extraction, complex-source extraction, manual reparse, and reviewer-prompt extraction.
- Defaulted ordinary extraction to `low`, complex-source/manual reparse extraction to `medium`, and reviewer-prompt extraction to `xhigh`.
- Updated Worker selection so structured notice extraction and outcome/candidate supplier extraction choose the correct Codex CLI scenario before each request.
- Updated the operations dashboard copy and docs to describe the low/medium/xhigh scenario policy.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_DefaultsToCodexCliModelAndReasoningEffort|BidOpsStructuredExtractionService_UsesRuntimeCodexCliModelAndReasoningEffort|BidOpsStructuredExtractionService_UsesReviewerPromptScenarioForReviewerPrompt|BidOpsStructuredExtractionService_UsesManualReparseScenarioWithoutPrompt|BidOpsStructuredExtractionService_UsesComplexScenarioForLongSource|BidOpsOutcomeSupplierAiExtractionService_UsesReviewerPromptScenarioForReviewerPrompt|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|OperationsControllers_DeclareP0Routes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 8 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsCodexScenarioSettings\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `ConvertFrom-Json` succeeded for the updated Worker/WebApi BidOps appsettings files.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` completed with only the existing line-ending warning for `src\Atlas.Data.Tenant.Migrations\Migrations\AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-23 BidOps Runtime Codex CLI Settings

Completed:

- Added tenant runtime settings for Codex CLI model and reasoning effort under `ai.codex-cli.model` and `ai.codex-cli.reasoning-effort`.
- Added `PUT /api/bidops/operations/ai-settings/codex-cli` so operations users can update Codex CLI settings from the dashboard.
- Updated the BidOps operations dashboard with Codex model and reasoning controls plus an `应用到 Worker` action.
- Updated structured notice extraction and outcome supplier extraction so Worker reads the effective runtime provider/model/reasoning settings before each Codex CLI request.
- Changed the default Codex CLI reasoning effort from `xhigh` to `medium` in constants and Worker/WebApi BidOps appsettings.
- Documented the runtime Codex CLI behavior in decisions and operations dashboard docs.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_DefaultsToCodexCliModelAndReasoningEffort|BidOpsStructuredExtractionService_UsesRuntimeCodexCliModelAndReasoningEffort|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|OperationsControllers_DeclareP0Routes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 5 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsRuntimeCodexSettings\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` completed with only the existing line-ending warning for `src/Atlas.Data.Tenant.Migrations/Migrations/AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-23 BidOps Global Task Pause Switch

Completed:

- Added a tenant-level BidOps runtime pause setting under `bidops_runtime_setting` key `runtime.task-pause`.
- Added the operations API `PUT /api/bidops/operations/runtime/task-pause` and dashboard runtime status payload.
- Added a `任务总开关` panel to the BidOps operations dashboard, next to the existing `AI 模型` provider switch.
- Added an Atlas background job execution gate so paused BidOps jobs are deferred before handler execution without consuming retry attempts.
- Blocked new operator-triggered BidOps job enqueue requests while paused, and made recurring BidOps tasks skip paused tenants.
- Documented the pause semantics and the existing AI provider/model switch location in operations dashboard docs and decisions.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "Worker_DefersJobWhenExecutionGateBlocksWithoutConsumingAttempt|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsGlobalPause\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin` after rerunning outside the sandbox; the first sandboxed attempt failed with Node `EPERM` reading `C:\Users\Jason`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` completed with only the existing line-ending warning for `src/Atlas.Data.Tenant.Migrations/Migrations/AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-22 BidOps Outcome Lot Identity Quality Fix

Completed:

- Installed MySQL Community Server 8.0.46 ZIP locally for the `mysql` CLI, verified the official MD5, added a user PATH entry, and added a `C:\Users\Jason\.local\bin\mysql.cmd` shim for the current Codex session.
- Investigated review task `327323283588517975` and found the visible conflict warning came from stale `bidops_review_quality_issue` rows that referenced deleted outcome records.
- Updated outcome supplier persistence to fill missing `LotNo` from row-leading public evidence such as `122609-9204013-9999 包 1 ...` when the source text has a lot-number table/header signal.
- Updated review quality writes to use explicit tenant-scoped repository operations so Worker reparses reliably remove old issue rows and write the current evaluation.
- Updated review-detail package fallback display so an ambiguous package number such as `包1` no longer causes the UI to show the first package's lot metadata as if it belonged to the result row.
- Repaired the local affected task data: updated 5 outcome records with evidence-derived `LotNo`, deleted 7 stale quality issues, and reset the review task quality summary to low risk.

Verification:

- `mysql --version` returns MySQL Community Server 8.0.46 and `SELECT 1` against `atlas_global_bidops` succeeds.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReviewQualityEvaluator|BidOpsOutcomeSupplierExtractionService_FillsLotNoFromLeadingOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_KeepsLotNoWhenSourceHasExplicitLotHeader|BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 11 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOutcomeLotFix\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-22 BidOps Review Detail Background Jobs

Completed:

- Added exact review-task background job persistence: raw notice reparse and outcome AI reparse now record the real background `jobId` in the review correction sample evidence JSON at enqueue time.
- Added `GET /api/bidops/review-tasks/{id}/jobs`, authorized with review-read permission, so review detail can fetch only jobs explicitly launched from that review task after refresh.
- Added a background-job operations query that loads a tenant/BidOps-scoped list by known job ids instead of inferring ownership from `rawNoticeId` payload text.
- Added a `本审核发起的后台任务` panel to the review detail page with job type, status, created/completed time, runtime, diagnostic preview, refresh, and detail navigation.
- Refreshed the job panel immediately after raw reparse and outcome AI reparse submissions, and during the existing short polling refresh.
- Replaced remaining user-facing DeepSeek prompt wording in review reparse paths with provider-neutral AI wording.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "SearchByIdsAsync|BackgroundTaskOperationsTests|BidOpsQueryService_MapsReviewQualityDtos" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 12 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReviewJobLinks\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-22 BidOps Review Automation Reduction Plan

Completed:

- Added `docs/BIDOPS/BIDOPS_REVIEW_AUTOMATION_REDUCTION_PLAN.md` with testable R0-R11 tasks for quality scoring, anomaly-first review, low-risk bulk confirmation, batch DeepSeek reparse, correction samples, historical backfill, and review efficiency metrics.
- Documented guardrails that AI/rules can only create staging quality signals and batch-confirm candidates; human approval remains required before Formal import.

Verification:

- Documentation-only change; no build or tests required.

## 2026-06-22 BidOps Review Quality Scoring R1-R2

Completed:

- Added review quality enums, issue type constants, `ReviewTask` quality summary fields, and `ReviewQualityIssue` staging-side entity.
- Added tenant migration `20260622090000_v0.2.15-bidops-review-quality` for `bidops_review_quality_issue` plus quality summary columns on `bidops_review_task`.
- Added `BidOpsReviewQualityEvaluator` and `BidOpsReviewQualityService`.
- Wired structured notice parsing and reparse to refresh quality score, risk level, issue counts, recommendation, and detailed issues before saving the review task.
- Exposed quality summary fields on `ReviewTaskDto` and detailed `QualityIssues` on review task detail DTOs.
- Added frontend BidOps type definitions for review quality fields and issue DTOs.
- Marked R1 and R2 as `Done` in `docs/BIDOPS/BIDOPS_REVIEW_AUTOMATION_REDUCTION_PLAN.md`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 6 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings after rerunning separately; an earlier parallel build attempt hit an expected file lock on the shared BidOps obj output.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-22 BidOps Review Quality Queue R4-R5

Completed:

- Added review-task quality filters to the backend query contract: risk level, quality score range, high-risk issue flag, review recommendation, and issue type.
- Updated review-task search ordering so high-risk, issue-heavy, low-quality tasks are returned first by default.
- Added quality filter controls and quality/recommendation columns to the review task list page.
- Added an `异常复核` panel to review task detail pages before the source/parse split view, showing quality score, risk level, issue counts, recommendation, and active issue rows.
- Updated frontend display helpers and BidOps types for review quality labels, options, and issue DTOs.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|ReviewTaskSearchQuery|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 7 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-20 BidOps Approved Notice Reparse Guard

Completed:

- Investigated local task submission failures after the machine resumed from sleep. WebApi logs showed repeated `POST /api/bidops/raw-notices/{id}/reparse` failures because the selected Raw notice was already approved or already imported into formal notices.
- Fixed the review detail page reparse visibility guard so approved, ignored, and merged review tasks do not expose raw reparse or procurement DeepSeek reparse actions.
- Corrected the frontend review-task status check: numeric `ReviewTaskStatus.Approved` is `2`, not `3`.
- Updated backend reparse guard messages to Chinese so direct API calls clearly explain that approved/imported notices cannot be reparsed.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.
- Local WebApi, Worker, and Vite frontend were restarted. WebApi returned 401 for an unauthenticated `/api/ops/background-jobs/summary` probe, Vite listened on `localhost:5173`, and Worker completed `bidops.recovery`.

## 2026-06-19 BidOps Procurement Review DeepSeek Prompt

Completed:

- Added a procurement-announcement DeepSeek adjustment panel to the review detail page, matching the outcome/candidate announcement workflow.
- Extended raw notice reparse requests and BidOps structured parse job payloads with an optional reviewer prompt.
- Passed reviewer prompts from review UI through WebApi, attachment processing, structured parse jobs, and `BidOpsStructuredExtractionService`.
- Updated the structured extraction prompt so reviewer corrections can override deterministic reference output when supported by the public notice or attachments.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek|StructuredParseJobHandler_ReturnsJsonResultSummary|StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails|RawNoticesController_DeclaresPipelineAndReparseRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 4 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.
- Local WebApi, Worker, and Vite frontend were restarted. WebApi returned 401 for an unauthenticated `/api/ops/background-jobs/summary` probe, Vite listened on `localhost:5173`, and Worker completed `bidops.recovery`.

## 2026-06-18 BidOps Procurement Amount Unit And Attachment Label

Completed:

- Updated structured DeepSeek procurement extraction guidance so `budgetAmount` and `maxPrice` must be returned in CNY yuan, including non-exact headers such as `采购金额（万元）`, `分项估算金额（万元）`, `包估算金额（万元）`, and `行报价最高限价（含税/万元）`.
- Added server-side amount normalization for string AI amount values and fallback reconciliation against deterministic procurement table parsing when DeepSeek returns an unmultiplied numeric value from a `万元` source column.
- Broadened deterministic procurement amount header matching and skipped non-money columns such as percentage limits, tax rates, weights, score columns, calculation methods, and price parameters.
- Changed the raw attachment action label from `来源` to `源文件`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_NormalizesProcurementAmountsFromTenThousandYuanHeaders|BidOpsEcpProcurementTableParser_NormalizesMoneyHeaderAliasesAndSkipsRateColumns|BidOpsEcpProcurementTableParser_ParsesEmbeddedPackageNoAndMaxPrice" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.
- `.\scripts\restart-webapi.ps1` and `.\scripts\restart-worker.ps1` restarted local BidOps services. WebApi returned 401 for an unauthenticated `/api/ops/background-jobs/summary` probe, and Worker completed `bidops.recovery`.

## 2026-06-18 BidOps Procurement Detail And Lifecycle Link Tasking

Completed:

- Added `docs/BIDOPS/BIDOPS_PROCUREMENT_CLOSURE_UPGRADE_TASKS.md` with testable T0-T9 tasks for procurement detail extraction, review, formal import, lifecycle matching, and analysis.
- Added `ProcurementDetailStaging`, `ProcurementDetail`, and `LifecyclePackageLink` entities to represent attachment row facts and lifecycle closure links separately from `TenderPackage` and `OutcomeSupplierRecord`.
- Added EF configurations for the new models, including tenant-scoped indexes, money precision, percentage/weight precision, `text` source text fields, and `longtext` JSON evidence columns.
- Added tenant migration `20260618093000_v0.2.14-bidops-procurement-details` to create `bidops_procurement_detail_staging`, `bidops_procurement_detail`, and `bidops_lifecycle_package_link`.
- Added procurement detail DTOs and lifecycle link DTO. Review task detail now exposes procurement detail staging rows, and package detail exposes formal procurement detail rows.
- Added local setup command `ensure-bidops-procurement-details` to repair local/runtime tenant databases that predate the procurement detail migration.
- Added regression coverage for model configuration and procurement detail DTO mapping.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ProcurementDetailConfiguration_MapsCoreIndexesAndJsonColumns|LifecyclePackageLinkConfiguration_UsesTenantScopedMatchIndex|BidOpsQueryService_MapsProcurementDetailDtosWithRawJsonAndAmounts" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-procurement-details --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"` created the missing local `bidops_procurement_detail_staging`, `bidops_procurement_detail`, and `bidops_lifecycle_package_link` tables.
- `.\scripts\restart-webapi.ps1`, `.\scripts\restart-worker.ps1`, and restarting the local Vite dev server completed successfully. `GET /api/bidops/review-tasks/325911170013859882` through `localhost:5173` returned HTTP 200.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-18 BidOps Outcome Amount Unit Semantics

Completed:

- Updated deterministic outcome/candidate table parsing so amount columns without an explicit unit are interpreted as yuan, while explicit `万元`/`万` headers or cells are still normalized to yuan by multiplying by 10,000.
- Updated the DeepSeek/OpenAI-compatible outcome supplier prompt to state that only explicit `万元`/`万` evidence triggers ten-thousand-yuan conversion; unitless values are yuan.
- Changed outcome review and supplier analysis amount labels/manual editing to use yuan instead of implying all award amounts are entered or shown in `万元`.
- Added regression coverage for unitless outcome table amount parsing.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierTextParser_ExtractsAwardAmountsFromOutcomeTableColumns|BidOpsOutcomeSupplierTextParser_TreatsUnitlessOutcomeTableAmountsAsYuan|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-16 DeepSeek Response Visibility In Background Job Details

Completed:

- Added scoped BidOps AI call diagnostics that capture DeepSeek/OpenAI-compatible raw response bodies and assistant content after each HTTP response is read.
- Included `deepSeekResponses` in BidOps structured parse and outcome supplier extraction job results, with a larger explicit Worker result storage cap for these AI diagnostic jobs.
- Widened `BackgroundJobs.Result` to `mediumtext` through global migration `v0.2.5-background-job-result-mediumtext`, updated the Global model snapshot, and extended the local setup repair command to modify the local column.
- Updated operations detail mapping so BidOps AI job details can return large diagnostic results while list previews stay short.
- Added a `DeepSeek 返回` tab on the background job detail page that formats assistant content and raw response body separately.
- Added regression tests for AI diagnostics capture, job result serialization, large Worker result storage, and large BidOps AI detail results.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsOutcomeSupplierAiExtractionService_LogsUnavailableSettingsWithoutApiKey|BidOpsOutcomeSupplierAiExtractionService_HandlesEmptyDeepSeekContent|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek|StructuredParseJobHandler_ReturnsJsonResultSummary|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 7 passed. The first parallel attempt was blocked by a transient local `obj` cache file lock; the sequential rerun passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "GetAsync_ReturnsFullResultForBidOpsDeepSeekDiagnostics|Worker_StoresExplicitLargeResult|Worker_CancelsRunningJobWhenTerminationIsRequested" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 3 passed.
- `dotnet build src\Atlas.Data.Global.Migrations\Atlas.Data.Global.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings after stopping the existing local Worker that locked normal output DLLs.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- ensure-background-job-cancellation --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"` succeeded and prepared the local Global DB result storage.
- `.\scripts\restart-webapi.ps1` and `.\scripts\restart-worker.ps1` restarted local BidOps WebApi/Worker. WebApi returned 401 for an unauthenticated `/api/ops/background-jobs/summary` probe, confirming it is up and enforcing auth; Worker PID `35744` completed the `bidops.recovery` recurring task in `BidOpsLocal`.
- Browser smoke opened `http://localhost:5173/ops/jobs`, confirmed the list and first job detail loaded without console errors. The inspected historical job did not show `AI 返回` because it predates persisted provider diagnostics, which is expected.

## 2026-06-15 Background Job Cooperative Termination

Completed:

- Added durable cancellation request fields to `BackgroundJobs`: `CancellationRequestedAt`, `CancellationRequestedBy`, and `CancellationReason`, plus global migration `v0.2.4-background-job-cancellation`.
- Changed background job cancel operations so Pending/Failed/Dead jobs still cancel immediately, while Running jobs record a termination request instead of returning a BadRequest.
- Updated `BackgroundJobWorker` to watch the active job for cancellation requests, cancel the handler `CancellationToken`, and mark the job `Canceled` when the handler exits because of that token.
- Exposed `IsCancellationRequested` and cancellation request metadata through operations DTOs. The operations frontend now shows Running jobs with pending termination as `终止中`, enables `终止` for Running jobs, and keeps `取消` for non-running unfinished jobs.
- Added regression coverage for operation-level Running cancellation requests and Worker-level cooperative cancellation of an active handler.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BackgroundTaskOperationsTests --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 8 passed.
- `dotnet build src\Atlas.Data.Global.Migrations\Atlas.Data.Global.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\GlobalMigrationJobCancellation\"` succeeded with 0 warnings.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiJobTermination\"` succeeded. One transient DLL copy retry warning occurred because another process briefly held `Atlas.Data.Abstractions.dll`; the build completed successfully.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerJobTermination\"` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LocalSetupJobTermination\"` succeeded with 0 warnings.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- ensure-background-job-cancellation --global "Server=localhost;Port=3306;Database=atlas_global_bidops;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"` succeeded and prepared the local historical Global DB without replaying pending EF migrations.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `.\scripts\restart-webapi.ps1` and `.\scripts\restart-worker.ps1` restarted local BidOps WebApi/Worker. WebApi PID `22384` served `/api/ops/background-jobs` and `/api/ops/background-jobs/summary` with HTTP 200; Worker PID `15596` resumed BidOps recovery recurring tasks.
- Browser smoke opened `http://localhost:5173/ops/jobs`, logged in with the local seeded BidOps account, confirmed the background job page loaded, and verified completed rows keep the cancel button disabled. No browser console errors were reported.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-15 Background Job Chinese Types And Local Times

Completed:

- Added backend task type display names for common Atlas, export, and BidOps job types. Operations DTOs now expose `JobTypeName` and Worker heartbeat DTOs expose `CurrentJobTypeName` while keeping the original `JobType` code for routing and filtering.
- Changed background job lifecycle writes in `BackgroundJobClient`, `BackgroundJobWorker`, manual retry/cancel operations, recurring task scheduling context, and Worker heartbeat updates to use server local time (`DateTime.Now`).
- Added local-time operation DTO aliases without `Utc` suffix: `AvailableAt`, `StartedAt`, `LockedAt`, `CompletedAt`, `NextAttemptAt`, `OldestPendingAt`, `RecentErrorAt`, `StartedAt`, and `LastSeenAt`. Existing `xxxAtUtc` fields remain for compatibility with old callers.
- Updated the operations frontend job list/detail pages, Worker heartbeat page, BidOps operations dashboard, and crawl channel enqueue success message to display Chinese task types and local-time fields.
- Updated `docs/background_tasks_guide.md` and decisions to document the local-operator semantics and the compatibility reason for keeping legacy `Utc` column/API aliases.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BackgroundTaskOperationsTests --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 6 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiBackgroundJobLocalTimes\"` succeeded. One transient DLL copy retry warning occurred because another process briefly held `Atlas.Data.Abstractions.dll`; the build completed successfully.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerBackgroundJobLocalTimes\"` succeeded with 0 warnings.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `rg "DateTime\.UtcNow|UtcNow" src/Atlas.BackgroundTasks src/Atlas.Modules.BidOps/Queries/BidOpsOperationsQueryService.cs -g "*.cs"` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-15 Local Audit Timestamps

Completed:

- Changed the EF audit interceptor to use `DateTime.Now` for infrastructure-managed `CreatedAt`, `UpdatedAt`, and soft-delete `DeletedAt` fields instead of `DateTime.UtcNow`.
- Changed bulk insert/update audit field population to use the same local-time source so normal and batch persistence paths stay consistent.
- Reverted the shared frontend `formatDateTime` helper to parse timezone-less datetime strings as browser-local time, preventing local backend timestamps from being shifted again in the UI.
- Superseded the earlier background-job exception later the same day: background job operations now expose and write local operator times; legacy `xxxAtUtc` field names remain only as compatibility aliases/columns.

Verification:

- `dotnet test tests\Atlas.Data.Tests\Atlas.Data.Tests.csproj --filter "AuditInterceptor_UsesLocalTimeForAuditFields" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 1 passed. Existing nullable warnings in `tests\Atlas.Data.Tests` remain unrelated.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerLocalAuditTime\"` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-15 BidOps Outcome Supplier Announcement Order

Completed:

- Added `OutcomeSupplierRecord.ExtractionOrder` and exposed it through `OutcomeSupplierRecordDto` so public result/candidate supplier rows keep the announcement order returned by DeepSeek or the deterministic parser.
- Changed reviewer-prompted DeepSeek reparses to persist the DeepSeek array order exactly. Automatic deterministic-plus-AI merging now treats AI rows as the primary ordered source and appends deterministic-only fallback rows after them.
- Changed review-detail outcome supplier queries to order by `ExtractionOrder` instead of package number, lot number, or supplier name. The review frontend no longer re-sorts award/candidate rows, so the visible table follows the backend announcement order.
- Added tenant migration `v0.2.12-bidops-outcome-extraction-order`. Existing `bidops_outcome_supplier_record` rows are deleted during the migration instead of being approximate-backfilled, because their original announcement order was not durable before this change.
- Updated LocalSetup `ensure-bidops-outcomes` so older local databases add `ExtractionOrder`, delete existing outcome supplier lead rows once during that schema upgrade, and create the raw-notice/order index.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptUsesAiRowsAsReplacement|BidOpsOutcomeSupplierExtractionService_ReviewerPromptPreservesAiAnnouncementOrder|BidOpsOutcomeSupplierExtractionService_AutomaticMergePrioritizesAiAnnouncementOrder|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|ReviewTasksController_DeclaresOutcomeAiReparseContract" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 5 passed.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\TenantMigrationOutcomeOrder2\"` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LocalSetupOutcomeOrder2\"` succeeded with 0 warnings.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerOutcomeOrder\"` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Local `ensure-bidops-outcomes` deleted 63 existing outcome supplier rows after adding `ExtractionOrder`; a DB probe confirmed `bidops_outcome_supplier_record: 0`.
- `.\scripts\restart-worker.ps1` restarted the local `BidOpsLocal` Worker after the schema update.

## 2026-06-15 Frontend DateTime Timezone Formatting

Completed:

- Updated the shared frontend `formatDateTime` utility to treat both `T`-separated and space-separated datetime strings without an explicit timezone as UTC before rendering them in the browser's current timezone.
- This fixes created/updated time displays when backend UTC values arrive as strings such as `2026-06-15 06:20:52` instead of `2026-06-15T06:20:52`.
- Confirmed BidOps and operations created/updated time columns use the shared formatter rather than direct `Date`/`dayjs` formatting.
- Superseded later the same day by the local audit timestamp change: timezone-less backend audit timestamps are now treated as local time because the backend stores audit fields in local time.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- A Node smoke check normalized `2026-06-15 06:20:52` to `2026-06-15T06:20:52Z` and rendered it as `2026-06-15 14:20` in the local `Asia/Shanghai` timezone.

## 2026-06-15 BidOps AI HTTP Timeout

Completed:

- Increased BidOps structured notice AI extraction and outcome supplier AI extraction typed `HttpClient.Timeout` from 2 minutes to 30 minutes.
- Kept crawler and attachment HTTP timeouts unchanged; the longer timeout is scoped to DeepSeek/OpenAI-compatible parsing requests that may need more time for large announcement and attachment text bundles.
- Added a registration test that resolves the BidOps AI named clients from `IHttpClientFactory` and verifies both are configured with a 30-minute timeout.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModule_ConfiguresAiHttpClientsWithThirtyMinuteTimeout|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOpsAiTimeout30Min\"` succeeded with 0 warnings.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WorkerAiTimeout30Min\"` succeeded with 0 warnings.
- `.\scripts\restart-worker.ps1` restarted the local `BidOpsLocal` Worker so new AI HTTP timeout settings are active for subsequent manual import/reparse jobs.

## 2026-06-15 BidOps State Grid Local Data Reset

Completed:

- Physically deleted existing local State Grid ECP entry data for tenant `300001`, source `330001`, and the four MVP notice types: `TenderAnnouncement`, `ProcurementAnnouncement`, `CandidateAnnouncement`, and `AwardAnnouncement`.
- Removed source-scoped Raw notices, attachments, staging rows, review tasks, formal notices/packages/requirements, outcome supplier leads, matching runs/results/checks, go/no-go decisions, opportunities, pursuits, crawl run logs, and BidOps background jobs.
- Deleted only the local file-store objects referenced by the deleted Raw/staging/attachment rows.
- Removed 11 supplier master-data shells that were created from the deleted Raw outcome notices; preserved unrelated supplier master data, supplier capability/contact/evidence seed data, buyer master data, and the crawl source/channel configuration.
- Kept the State Grid source and four channels enabled for manual one-by-one import. Automatic scheduled scanning remains disabled in `BidOpsLocal` configuration.
- Disabled local `BidOpsLocal` opportunity and supplier maintenance recurring tasks so the manual-import stabilization phase does not keep generating non-crawl BidOps maintenance jobs while one-time manual import jobs remain enabled.

Verification:

- Pre-delete dry run reported 3,939 tenant rows, 5,461 global BidOps jobs, and 757 referenced local storage files as candidates.
- Confirmed delete removed 3,944 tenant DB rows, including 5 source/channel reset updates, 5,461 global BidOps background jobs, and 757 storage files.
- Post-delete read-only verification showed `bidops_raw_notice`, `bidops_raw_attachment`, staging/review/formal/outcome/matching/opportunity/pursuit tables, `bidops_crawl_run_log`, and global BidOps background jobs at 0 rows for the source-scoped data.
- Post-delete verification showed `bidops_crawl_source` remains 1 row, `bidops_crawl_channel` remains 4 rows, `bidops_buyer` remains 1 row, and unrelated supplier master data remains.
- After a Worker restart generated 5 maintenance jobs, stopped the Worker, deleted those 5 global BidOps jobs, and restarted with opportunity/supplier maintenance disabled.

## 2026-06-15 BidOps Reviewer-Prompted DeepSeek Reparse Gate

Completed:

- Confirmed local Worker processed reviewer-prompted outcome reparse jobs with `reviewerPrompt=True` but no DeepSeek request logs because automatic outcome/candidate notice detection returned false before the AI client was invoked.
- Changed outcome supplier extraction so reviewer-provided correction prompts force an AI extraction attempt even when automatic outcome detection does not match.
- Added a Worker information log before the forced AI path to make this branch observable.
- Added AI HTTP configuration diagnostics when DeepSeek/OpenAI-compatible settings are unavailable, including a missing-key warning without logging secret values.
- Changed DeepSeek request/response diagnostic JSON logging to preserve readable Chinese instead of `\uXXXX` unicode escapes.
- Handled DeepSeek responses where `message.content` is empty, including `finish_reason=length`, without throwing `JsonReaderException`.
- Switched BidOps local DeepSeek model configuration and the DeepSeek default model from the lower-latency flash model to `deepseek-v4-pro`.
- Tightened DeepSeek prompts to require JSON-only output and explicitly forbid reasoning traces, analysis, Markdown, code fences, prefixes, or suffixes.
- Made `BidOps:Ai:MaxOutputTokens` optional; when unset or non-positive, the request omits `max_tokens` instead of imposing an arbitrary output-token cap.
- Updated the local Worker restart script to load `DEEPSEEK_API_KEY` from User/Machine environment variables when the process environment is empty, use encoded PowerShell startup commands, stop `Atlas.Worker.exe` processes even when WMI hides command lines, and fall back to WMI terminate when `Stop-Process` is denied.
- Kept unattended parsing conservative: without a reviewer prompt, non-outcome notices still skip outcome supplier extraction.
- Added regression tests for the reviewer-prompt extraction gate, missing-API-key diagnostics, readable Chinese DeepSeek request/response logs, and empty DeepSeek content handling.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptForcesAiAttempt|BidOpsOutcomeSupplierExtractionService_ReviewerPromptUsesAiRowsAsReplacement|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsOutcomeSupplierAiExtractionService_LogsUnavailableSettingsWithoutApiKey|BidOpsOutcomeSupplierAiExtractionService_HandlesEmptyDeepSeekContent|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 6 passed, including readable Chinese request/response log assertions and empty DeepSeek content handling.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_HandlesEmptyDeepSeekContent|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOpsEmptyDeepSeekContent\"` succeeded with 0 warnings.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WorkerEmptyDeepSeekContent\"` succeeded with 0 warnings.
- `git diff --check` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- `.\scripts\restart-worker.ps1` restarted the local Worker with `DOTNET_ENVIRONMENT=BidOpsLocal`; `var\logs\worker-local.log` confirms Worker resumed recurring tasks.
- `.\scripts\restart-worker.ps1` restarted a new local Worker as `Atlas.Worker.exe` PID 6616 after terminating the stale Worker process tree with WMI; the new `var\logs\worker-local.log` confirms `BidOpsLocal` recurring tasks resumed.

## 2026-06-15 Worker Timestamped Logging

Completed:

- Registered the Atlas Serilog logging pipeline in `Atlas.Worker` and cleared the default .NET logging providers before adding Serilog.
- Added Worker-specific `Logging:Atlas` settings with dedicated application, error, and audit log file paths.
- Changed the Atlas console log output template to include full date, time, milliseconds, and timezone so `var/logs/worker-local.log` includes timestamps.
- Updated Serilog environment enrichment to honor `DOTNET_ENVIRONMENT` when `ASPNETCORE_ENVIRONMENT` is not set.
- Added Worker logging configuration regression tests.

Verification:

- `dotnet restore src\Atlas.Worker\Atlas.Worker.csproj --nologo` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WorkerTimestampLogging\"` succeeded with 0 warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "WorkerLoggingConfigurationTests" --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `git diff --check` succeeded.

## 2026-06-15 BidOps DeepSeek Chinese Prompts And Request Logging

Completed:

- Rewrote the DeepSeek/OpenAI-compatible structured notice prompt in Chinese while keeping JSON schema field names and enum values unchanged.
- Rewrote the DeepSeek/OpenAI-compatible outcome supplier prompt in Chinese, including candidate/result field mapping, reviewer correction handling, package-number preservation, amount-unit handling, and attachment/table guidance.
- Changed attachment metadata labels in AI source bundles from English to Chinese.
- Removed the public notice detail URL from DeepSeek prompt material; prompts now pass announcement body HTML plus attachment URLs, metadata, and extracted text.
- Added AI request start, failure, and completion logs for structured notice extraction and outcome supplier extraction. Logs include provider/model/endpoint host path, prompt and response sizes, status code, elapsed time, attachment counts, parsed result counts, the full JSON request body sent to DeepSeek, and the full raw DeepSeek response body. Authorization/API keys are still excluded.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 2 passed, including assertions that logs contain the full request body and full raw response body while excluding the API key.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOpsDeepSeekFullLogs\"` succeeded with 0 warnings after rerunning outside a transient local `obj` file lock.
- `git diff --check` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- Secret scan for common API key/token patterns returned no matches.

## 2026-06-15 BidOps PR Conflict Resolution

Completed:

- Merged `origin/main` into `codex/bidops-ecp-attachments-outcomes` to resolve PR #35 merge conflicts.
- Kept the BidOps `forceRefresh` manual re-import flow, force parse run ids, SGCC crawl adapter validation, public SGCC host validation, and WCM raw HTML body preservation.
- Removed duplicate auto-merged definitions for raw attachment backfill request, payload, and enqueue service method.
- Preserved both script-based and manual local Worker startup guidance in the BidOps runbook.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMerge\"` succeeded with 0 warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 94 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApiMerge\"` succeeded with 0 warnings.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WorkerMerge\"` succeeded with 0 warnings after rerunning outside a transient local `obj` file lock.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.

## 2026-06-15 BidOps Review Outcome Editing And DeepSeek Replacement

Completed:

- Changed reviewer-prompted DeepSeek outcome reparse so DeepSeek rows replace current outcome/candidate rows instead of being merged with deterministic historical rows.
- Added review-task APIs to add, update, and delete outcome supplier lead rows before approval, guarded by the existing review approval permission and blocked for approved/formal Raw notices.
- Added review-detail manual editing UI for award/candidate/generic outcome rows. Preview rows without a persisted id can be saved as new editable rows; persisted rows can be edited or deleted.
- Kept money storage as CNY yuan. At that point the review edit form accepted the main final quote/award amount in `万元`; that UI assumption is superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`.
- Updated background job result serialization and job detail rendering so Chinese prompt/result text is displayed as Chinese instead of unicode escape sequences, including a fallback decoder for older non-JSON job text.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptUsesAiRowsAsReplacement|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|ReviewTasksController_DeclaresOutcomeAiReparseContract" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOps\"` succeeded; the first parallel run hit a transient local `obj` file lock warning.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded after rerunning outside a parallel local `obj` file lock.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- Browser smoke confirmed approved review tasks hide manual edit actions, pending review tasks show `新增明细` and `编辑`, and the manual detail dialog exposed the then-current amount-in-`万元`, agency service fee, and evidence fields. The amount unit behavior is superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`.
- `.\scripts\restart-webapi.ps1` and `.\scripts\restart-worker.ps1` restarted the local WebApi and Worker. WebApi is reachable on `http://localhost:5260`; Worker resumed processing background jobs.

## 2026-06-14 BidOps Local Worker DeepSeek Configuration

Completed:

- Enabled `BidOps:Ai` in `src/Atlas.Worker/appsettings.BidOpsLocal.json` so local Worker reparse jobs can call the configured DeepSeek/OpenAI-compatible provider.
- Removed the empty local `BidOps:Ai:ApiKey` override from `BidOpsLocal`; a base config value or `DEEPSEEK_API_KEY` can now be seen by the Worker instead of being shadowed by an empty string.
- Added `scripts/restart-worker.ps1` to stop existing local Worker processes and restart `Atlas.Worker` with `DOTNET_ENVIRONMENT=BidOpsLocal` / `ASPNETCORE_ENVIRONMENT=BidOpsLocal`, writing logs to `var\logs\worker-local.log`.

Verification:

- `src\Atlas.Worker\appsettings.BidOpsLocal.json` parsed successfully as JSON.
- `scripts\restart-worker.ps1` passed PowerShell parser syntax validation.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOps\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- `.\scripts\restart-worker.ps1` stopped the old local Worker process and restarted Worker with `Hosting environment: BidOpsLocal`; the new log contains no RabbitMQ connection failure.
- The restarted Worker called `POST https://api.deepseek.com/chat/completions` and received HTTP 200. The outcome supplier extract job saved 5 records for RawNotice `323981944922705920`, with buyer created=1 and supplier created=4.

## 2026-06-14 BidOps DeepSeek Job Result Drilldown

Completed:

- Added a BidOps parsed-result panel to background job detail pages. It derives `RawNoticeId` from the job payload/result/deduplication key, then loads the Raw notice pipeline, review staging detail, package rows, and persisted outcome supplier rows from the database.
- Added `/bidops/operations/jobs/:id` so BidOps task list entries and review-page DeepSeek/reparse job ids can open a BidOps-scoped job detail page.
- Made the review detail DeepSeek reparse job id and Raw reparse job id clickable, leading directly to the job detail and its parsed-result tab.
- Changed structured parse and outcome supplier extract job success results to compact JSON summaries containing RawNoticeId, review task id, saved row counts, and buyer/supplier sync counts.
- Preserved snowflake id precision in the parsed-result panel by extracting RawNoticeId from raw payload/result strings before falling back to JSON parsing.
- Kept persistence behavior unchanged: Worker jobs write Staging/Outcome rows first, and formal business import remains gated by human review approval.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "StructuredParseJobHandler_ReturnsJsonResultSummary|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with one transient file-lock retry warning from the local machine.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- Browser smoke opened `/bidops/operations/jobs`, filtered `bidops.outcome.supplier-extract`, opened a BidOps job detail, switched to `解析结果`, and confirmed exact RawNoticeId display, pipeline data, staging notice fields, and no console errors.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-14 BidOps Review Outcome AI Adjustment

Completed:

- Added a review-task endpoint to enqueue DeepSeek outcome/candidate detail re-extraction with a reviewer-provided correction prompt.
- Extended the outcome supplier extraction job payload and AI prompt so Worker-side extraction can use reviewer corrections while still rebuilding only the current Raw notice's outcome lead rows.
- Extended DeepSeek structured and outcome extraction inputs to include the stored announcement body HTML plus extracted attachment content and attachment metadata, instead of sending only flattened text.
- Updated DeepSeek prompts so required fields are selected by notice type: procurement/tender notices focus on packages and requirements, candidate notices focus on candidate/package identity fields, and award/result notices focus on awarded supplier rows and package identity fields.
- Updated the review detail page to show candidate notices as a flat business list with `采购编号`, `分标编号`, `分标名称`, `包号`, `包名称`, `排名`, `推荐的成交候选人`, the then-current `最终报价（万元）`, and `评审情况`. The amount display label is superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`.
- Updated award/result detail rows to show `采购编号`, `分标编号`, `分标名称`, `包号`, `中标状态`, and `成交供应商`.
- Added a DeepSeek correction textbox above outcome lists so reviewers can iteratively submit a better prompt, refresh the rebuilt rows, and approve only after the staging data looks right.
- Added a review-detail `重新解析` action that enqueues the existing RawNotice reparse pipeline from the audit page when content/body fields need to be extracted again.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|ReviewTasksController_DeclaresOutcomeAiReparseContract|StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek|StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- Browser smoke opened a local candidate review task and confirmed the DeepSeek adjustment panel plus candidate columns including `推荐的成交候选人`, the then-current `最终报价（万元）`, and `评审情况`. The amount display label is superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`.
- Browser smoke opened a local award review task and confirmed the DeepSeek adjustment panel plus award columns `采购编号`, `分标编号`, `分标名称`, `包号`, `中标状态`, and `成交供应商`; no console errors were reported.
- Browser smoke opened a pending award review task and confirmed the review-detail `重新解析` action is visible next to the page title; no console errors were reported.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-14 BidOps Review Detail Organization Linking

Completed:

- Extended review detail APIs and DTOs to return the parsed buyer plus outcome supplier rows for the Raw notice under review.
- Updated the review detail page to show `采购方与厂家`, including whether the buyer/supplier already exists or will be created/linked after approval.
- Added a read-only review-detail fallback that parses outcome supplier preview rows from stored Raw notice text/HTML and extracted attachment text when no persisted outcome rows exist yet.
- Added `BuyerProcurementRecord` and registered it as `bidops_buyer_procurement_record` so approved notices record buyer purchase history by source-backed notice/package snapshots.
- Extended review approval to sync approved notice context into outcome supplier records, upsert buyer/supplier master records, link `BuyerId`/`SupplierId`, and create/update buyer procurement history inside the same transaction.
- Hardened the Worker structured-parse job so outcome supplier extraction still runs when generic notice staging parsing fails on public result/candidate announcements.
- Hardened review approval so it runs outcome supplier extraction before approval when the Raw notice has no persisted outcome rows yet.
- Added `forceRefresh` manual import support so Raw detail can re-fetch a public announcement, refresh stored Raw text/HTML, force attachment text extraction, and enqueue a fresh structured parse.
- State Grid WCM imports now preserve original `CONT` HTML in the Raw HTML snapshot when available, including Word-style `MsoNormal` tables.
- Enhanced award table parsing so tables without project/procurement code or lot columns can use surrounding正文 regex fallback for `采购编号`/`招标编号` and nearby `分标编号`/`分标名称`.
- Added supplier traceability fields for the announcement that created the supplier shell and the latest related public outcome announcement.
- Updated local setup and tenant migrations for the new supplier traceability columns and buyer procurement table.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsAwardEvidenceParser_FillsProjectAndLotFromBodyWhenTableOmitsColumns|BidOpsAwardEvidenceParser_ExtractsStateGridHtmlAwardTable|StateGridEcpWcmParser_PreservesWinHtmlTablesForOutcomeExtraction" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 85 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails|BidOpsAwardEvidenceParser_ExtractsStateGridHtmlAwardTable|StateGridEcpWcmParser_PreservesWinHtmlTablesForOutcomeExtraction" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 58 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded; one local PDB file-lock retry warning cleared.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded after rerun; the first parallel run hit a transient local `obj` file lock.
- A temporary C# smoke against State Grid public WCM `index/getNoticeWin` for `2606128522123684` extracted 93 award rows from the real `CONT` HTML table; the first row included lot `SG2674-9001-13028`, lot name `变电站土建施工`, package `包1`, project unit `国网四川省电力公司`, and winner `中国电建集团江西省水电工程局有限公司`.
- Browser smoke opened `http://localhost:5173/bidops/review/tasks` and was redirected to the local login page. No credentials were submitted during this verification pass.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-14 BidOps Reverse Closure State Grid Award HTML Table Fix

Completed:

- Added deterministic HTML table extraction to `BidOpsEvidenceTableParser` so State Grid WCM `CONT` tables are parsed in addition to Markdown and whitespace tables.
- Extended reverse closure DTO output with award `ProjectUnit` and `LotName`, and closure-level `ProjectUnit` and `LotName`.
- Updated award evidence parsing to read `项目单位`, `分标名称`, `分标编号`, `包号`, and `中标人` from State Grid award-result tables, including nested-span package values such as `包1`.
- Tightened project-code cleanup so HTML text like `招标编号：0711-26OTL04213025）` returns `0711-26OTL04213025`.
- Prevented same-package cross-linking across different lots by requiring `LotNo + PackageNo` to match when both sides have a lot number.
- Updated the BidOps runbook for the State Grid WCM award table fields.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsReverseClosureTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 26 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded.
- A temporary .NET 8 parser smoke against the live State Grid WCM response for `2606128522123684` extracted 93 award rows. The first row included project code `0711-26OTL04213025`, lot `SG2674-9001-13028`, lot name `变电站土建施工`, package `包1`, project unit `国网四川省电力公司`, and winner `中国电建集团江西省水电工程局有限公司`.

## 2026-06-13 BidOps Reverse Lifecycle Closure Debug

Completed:

- Added DTO-only reverse lifecycle closure models: `AwardEvidence`, `CandidateEvidence`, `TenderPackageEvidence`, `LifecyclePackageClosure`, notice match refs, and source evidence refs.
- Added deterministic evidence parsing under `src/Atlas.Modules.BidOps/Ai/Evidence`:
  - package number normalization for `包1` / `包01` / `包一` / `分包1` / `分包编号1` / `标包1`.
  - money normalization for yuan and ten-thousand-yuan forms while excluding percentages, rates, and score cells.
  - award evidence parsing for full award tables, sparse package/supplier tables, and paragraph-style `包1：XXX公司`.
  - candidate evidence parsing for full ranking tables, compact ranking tables, horizontal Top3 tables, and fill-down package/lot cells.
  - tender package evidence parsing for procurement scope, budget/max-price, and qualification tables.
- Added `BidOpsReverseLifecycleClosureService` to read RawNotice and extracted attachment text through repositories and `IBidOpsFileStore`, match candidate/tender notices, and output suggested lifecycle package closures with match reasons, missing fields, confidence, and manual-review flags.
- Added debug APIs:
  - `POST /api/bidops/lifecycle/debug/reverse-close-url`
  - `POST /api/bidops/lifecycle/debug/reverse-close-raw-notice/{rawNoticeId}`
- Kept WebApi enqueue/query-only for external crawling: when the URL RawNotice is missing, the debug API enqueues the existing manual import job and returns a warning instead of fetching SGCC synchronously.
- Added development-only `tools/Atlas.LocalSetup reset-bidops-derived-data`, dry-run by default with `--confirm` required for deletes, protecting RawNotice, RawAttachment, CrawlSource, CrawlChannel, CrawlRunLog, attachment files, and supplier master data.
- Updated BidOps runbook and decisions.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after replacing dynamic delete SQL with parameterized DbCommand execution.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseClosureTests|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 25 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 52 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded with 1 transient copy retry warning from an already-running local process holding `Atlas.Infrastructure.Caching` obj output.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded.
- Default-output WebApi/Worker builds were also attempted and failed only because existing local `Atlas.WebApi` and `Atlas.Worker` processes locked their normal `bin\Debug\net8.0` DLL outputs; the isolated-output builds above verified compilation without stopping those processes.

## 2026-06-13 BidOps Outcome Supplier Award Amount Extraction

Completed:

- Extended `BidOpsOutcomeSupplierTextParser` so public result supplier leads can extract award amounts from explicit labels such as `成交金额`, `中标价`, `投标报价`, `应答报价`, `评审价`, and `金额`.
- Added result-table amount inference for explicit supplier tables whose headers contain amount/price columns and units such as `万元` or `元`.
- Amounts are normalized to CNY yuan in `OutcomeSupplierRecord.AwardAmount`; for example `40.05 万元` is stored as `400500.00`.
- Percentage, discount-rate, fee-rate, and score cells such as `97.50%` and trailing `88.00` scores are ignored and are not treated as award amounts.
- Extended fragment cleanup to discard generic institution/company suffix tails such as `研究院有限公司` while preserving real full names such as `山东黄河勘测设计研究院有限公司`.
- Re-ran local outcome supplier backfill for the BidOps tenant. The local public outcome lead set now has `recordCount=1108`, `supplierCount=558`, and 38 records with non-empty `AwardAmount`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsOutcomeSupplierTextParser --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 10 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 52 passed. A transient copy retry warning occurred because another test process briefly held `Atlas.Services.Tests.dll`; the retry succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- Authenticated API smoke as `bidops_admin` enqueued 85 `bidops.outcome.supplier-extract` jobs through `POST /api/bidops/suppliers/outcome-records/backfill?maxItems=500`.
- Background job summary for tenant `300001` and `bidops.outcome.supplier-extract` reported `pending=0`, `running=0`, `failed=0`, `dead=0`, and `succeeded=813`.
- Post-backfill API samples included `常州天辰电力科技有限公司` with `awardAmount=400500.00` from evidence `40.05 万元`, and exact pagination check returned `0` records for `研究院有限公司`.

## 2026-06-13 BidOps Outcome Supplier Fragment Cleanup

Completed:

- Hardened `BidOpsOutcomeSupplierTextParser` so PDF/Word table fragments like `有限公司`, `技有限公司`, `工程有限公司`, `务有限公司`, `术有限公司`, `程有限公司`, `股份有限公司`, and `科技有限公司` are not accepted as supplier names.
- Added a formal-company-suffix guard that requires enough organization-name content before suffixes such as `有限公司`, `股份有限公司`, `有限责任公司`, `工程设计有限公司`, and `分公司`.
- Added a guard for unbalanced parentheses so fractured names such as `周口龙润电力（集团` are discarded instead of surfacing in supplier analysis.
- Re-ran local outcome supplier backfill for the BidOps tenant. The local public outcome lead set changed from `recordCount=1623`, `supplierCount=616` to `recordCount=1110`, `supplierCount=559` after removing fragment records.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsOutcomeSupplierTextParser --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 8 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 50 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- Authenticated API smoke as `bidops_admin` enqueued 85 `bidops.outcome.supplier-extract` jobs through `POST /api/bidops/suppliers/outcome-records/backfill?maxItems=500`.
- Post-backfill exact pagination checks returned `0` records for the fragment supplier names `有限公司`, `技有限公司`, `工程有限公司`, `务有限公司`, `术有限公司`, `程有限公司`, `股份有限公司`, `科技有限公司`, and `周口龙润电力（集团`.
- Background job summary for tenant `300001` and `bidops.outcome.supplier-extract` reported `pending=0`, `running=0`, `failed=0`, `dead=0`, and `succeeded=643`.

## 2026-06-13 BidOps ZIP And Excel Attachment Extraction

Completed:

- Extended `BidOpsTextExtractor` so Worker attachment processing extracts `.xlsx/.xlsm/.xltx/.xltm` worksheet text through OpenXML worksheet/shared-string XML.
- Added safe in-memory `.zip` recursion for supported inner files, including Excel workbooks and text/HTML/PDF/DOCX attachments, with archive depth, entry count, and entry size limits.
- Added a conservative readable-text fallback for legacy binary `.doc/.xls` files without adding a new conversion service or framework/package upgrade.
- Updated attachment MIME-type inference for Excel and ZIP in both Worker processing and authorized attachment file responses.
- Added regression tests proving XLSX sheet rows and ZIP-contained XLSX/TXT content are included in extracted attachment text.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor" --nologo --verbosity minimal` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 37 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal` succeeded with 0 warnings and 0 errors.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal` succeeded with 0 warnings and 0 errors.
- `dotnet test Atlas.sln --no-build --nologo --verbosity minimal` was run. Non-integration projects passed, including `Atlas.Services.Tests` with 86 passed; existing integration tests still failed outside BidOps because cache integration setup cannot resolve `ICurrentIdentity`, `atlas_global` is missing locally, and the local MySQL instance still hits the known 767-byte key-length limit.
- `git diff --check` succeeded.

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

## 2026-06-11 BidOps Admin Frontend

Completed:

- Added `frontend/atlas-admin` as an independent Vue 3 + Vite + TypeScript admin frontend.
- Added Element Plus, Pinia, Vue Router, Axios, Dayjs, and DOMPurify integration.
- Implemented Vite alias `@`, `/api` development proxy, `.env.example`, and npm scripts for `dev`, `typecheck`, `build`, and `preview`.
- Implemented `BasicLayout`, `BlankLayout`, router guards, auth/app/permission stores, and an Axios HTTP client with Bearer/Cookie support plus `X-Tenant-Id` and `X-Store-Id` headers.
- Added BidOps permission constants, TypeScript DTO/request types, and API wrappers for crawl sources, crawl channels, raw notices, review tasks, notices, and packages.
- Implemented BidOps pages:
  - `/bidops`
  - `/bidops/crawl/sources`
  - `/bidops/crawl/channels`
  - `/bidops/crawl/raw-notices`
  - `/bidops/crawl/raw-notices/:id`
  - `/bidops/review/tasks`
  - `/bidops/review/tasks/:id`
  - `/bidops/notices`
  - `/bidops/packages`
  - `/bidops/packages/:id`
- Added shared components and composables for page containers, search forms, form drawers, tables, empty states, pagination/query loading, permissions, and requests.
- Added BidOps UI components for status tags, risk tags, deadline countdowns, permission buttons, raw notice text preview, manual public URL import, requirement tables, and review decisions.
- Added `frontend/atlas-admin/docs/BIDOPS_API_MAPPING.md` and `frontend/atlas-admin/docs/BIDOPS_FRONTEND_GAPS.md`.

Verification:

- `npm install` succeeded. Runtime dependency audit with `npm audit --omit=dev` reported 0 vulnerabilities.
- `npm run typecheck` succeeded.
- `npm run build` succeeded. Vite reported non-fatal warnings about Rollup comment annotations in a dependency and chunk size after minification.
- Started the Vite dev server at `http://localhost:5173/`; `Invoke-WebRequest http://localhost:5173` returned HTTP 200.
- Browser smoke check confirmed `/bidops` renders the Atlas Admin title, left menu, and 6 BidOps entry cards.
- Browser smoke check confirmed `/bidops/crawl/sources` renders search, compliance warning, create action, table headers, and empty state even when the backend proxy returns an error.
- Browser smoke check confirmed `/bidops/packages/1` renders the documented missing package detail/timeline placeholders and requirements table.

## 2026-06-12 BidOps Frontend Proxy Fix

Completed:

- Updated the Vite development proxy default from `https://localhost:5001` to `http://localhost:5260`, matching the local `src/Atlas.WebApi` launch profile.
- Updated `frontend/atlas-admin/.env.example` and README proxy instructions.
- Confirmed the previous menu errors were Vite proxy failures to `::1:5001`, while `http://localhost:5260/swagger/index.html` returned HTTP 200 and BidOps API endpoints returned authorization responses from the real backend.

## 2026-06-12 State Grid ECP Detail URL Fix

Completed:

- Fixed State Grid WCM list parsing so public detail URLs are built from `noticeId`/`id` plus `firstPageMenuId`, matching the portal's `/doc/{doctype}/{id}_{menuId}` router.
- Stopped using `firstPageDocId` as the route document ID for `doci-*` procurement notices. Public checks showed a sampled `firstPageDocId` returned `SYS001` from `/index/getNoticeWin`, while the matching `noticeId` returned the correct notice.
- Added a regression test for the `doci-win` failure mode reported from the local review pool.
- Repaired local `atlas_bidops_runtime` SGCC Raw data: 80 active Raw URLs, URL hashes, text previews, text snapshots, and HTML snapshots now use the correct `noticeId` route. One historical duplicate pending review row was marked ignored because its corrected URL collides with an already-approved Raw notice.
- Added the `atlas_global_bidops` connection string to Worker `BidOpsLocal` configuration so local Worker restarts use the same seeded BidOps tenant as WebApi.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 12 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 59 passed.
- Local WebApi was restarted on `http://localhost:5260`, local Worker was restarted in `BidOpsLocal`, and Worker recovery completed against tenant `300001`.
- Authenticated API verification confirmed review task `323460760351150083` returns `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606108456217237_2018060501171111` in both `RawNotice.DetailUrl` and Raw full-text `SourceUrl`.

## 2026-06-12 BidOps Project Code Extraction Fix

Completed:

- Fixed deterministic project-code extraction so a blank `ProjectCode:` line cannot consume the next metadata field name such as `ListPublishTime`.
- Added HTML-aware project-code extraction for State Grid WCM detail content, including labels split by HTML tags such as `采购编</span><span>号`.
- Repaired local `atlas_bidops_runtime` staging data: 39 `ProjectCode = ListPublishTime` rows were corrected, with 9 real codes restored and 30 rows cleared because no reliable public project code was present.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 14 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 61 passed.
- Local WebApi and Worker were restarted in `BidOpsLocal`.
- Authenticated review-list API verification returned 79 pending rows and `ListPublishTimeItems = 0`; sample project codes included `17FH05`, `932668`, `552635`, and `19FJAC`.

## 2026-06-12 BidOps Admin Login Integration

Completed:

- Added `AtlasUserAuthController` under `src/Atlas.Extensions.DependencyInjection` to expose Atlas user login, refresh-token, logout, switch-store, and accessible-store endpoints from the WebApi.
- Added `src/Atlas.WebApi/appsettings.BidOpsLocal.json` and a `bidops-local` launch profile so the local WebApi uses the seeded `atlas_global_bidops` global database.
- Added frontend auth API wrappers, a `/login` route/page, real session storage, route guard permission-context loading, 401 redirect handling, and top-bar logout.
- Removed the previous development permission seeding path from the frontend auth store. The UI now depends on the token and `/api/auth/context` permissions.
- Prefilled the local seeded BidOps account on the login page for development convenience: domain `bidops`, username `bidops_admin`, password `Pass1234!`.

Verification:

- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 58 passed.
- Started WebApi with `--launch-profile bidops-local` at `http://localhost:5260` and Vite at `http://localhost:5173`.
- Sequential proxy smoke test through `http://localhost:5173/api/user/login` succeeded for `bidops_admin`, `/api/auth/context` returned 20 permissions, and BidOps list APIs returned successfully.
- Browser smoke test confirmed `/login` renders the seeded account, login redirects to `/bidops`, and `/bidops/crawl/sources` plus `/bidops/crawl/raw-notices` render without 500 errors.

## 2026-06-12 BidOps Review Usability

Completed:

- Expanded `ReviewTaskDto` with project, buyer, region, key date, package/requirement/risk counts, and confidence summary fields.
- Updated `GetReviewTaskDetailAsync` to return Raw notice text content from `IBidOpsFileStore` for the review detail page.
- Added local file-store relative-path resolution and a BidOpsLocal fallback root so WebApi can read Raw text created by the local Worker.
- Reworked the review task list to show business review columns instead of only task title and internal IDs.
- Reworked the review detail page to show review summary, Raw evidence, full notice text, parsed notice fields, packages, requirements, risk flags, and the decision panel.
- Changed frontend BidOps ID handling to string-safe route/API values to avoid JavaScript precision loss on Atlas snowflake IDs.
- Added contextual BidOps status labels so review and Raw statuses render as names instead of bare numbers.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore` succeeded with 0 warnings and 0 errors after stopping the running WebApi process that locked DLLs.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 58 passed.
- Proxy API smoke test confirmed review list returns project/buyer/package/requirement summary fields, and review detail returns Raw text content longer than the 200-character preview.
- Browser smoke test confirmed `/bidops/review/tasks` shows business columns and `/bidops/review/tasks/323460760699277314` shows full text, parsed result, requirements, readable statuses, and review actions without the previous empty detail state.

## 2026-06-12 BidOps Chinese Display Labels

Completed:

- Added BidOps frontend display mappings for notice types, source types, package categories, requirement types, evidence types, risk levels, and common statuses.
- Updated review list/detail, Raw notice list/detail, formal notices, packages, crawl sources/channels, manual import, status tags, risk tags, and requirement tables to prefer Chinese labels while keeping backend enum values unchanged.
- Added Chinese defaults for common parser explanations shown in the review requirement table.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- Browser smoke test confirmed review detail displays Chinese notice type, status, requirement type, evidence type, and parser explanation, with no visible `AwardAnnouncement` or default English qualification explanation.

## 2026-06-12 BidOps State Grid Attachments

Completed:

- Added State Grid ECP `doci-win` attachment discovery through the public WCM `index/getWinFile` endpoint.
- Built public `downLoadWinFile` attachment URLs from `FILE_PATH` and `FILE_NAME`, and kept attachment download/text extraction in the existing Worker pipeline.
- Added stable hashing for State Grid `downLoadWinFile` URLs so volatile encrypted `filePath` tokens do not create duplicate attachments across scans.
- Updated Raw attachment ingestion to upsert by stable `FileHash` and refresh the latest public download URL when a known attachment is seen again.
- Added `RawAttachmentDto`, `GET /api/bidops/raw-notices/{id}/attachments`, and `ReviewTaskDetailDto.Attachments`.
- Added the shared frontend `RawAttachmentTable` and rendered public attachments in Raw notice detail and review detail pages.
- Repaired local `atlas_bidops_runtime` data: widened the local attachment URL/name columns to match entity configuration, added the missing tenant-scoped attachment unique index, and backfilled 39 public SGCC attachments with Chinese file names.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 16 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 63 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- Authenticated API smoke test confirmed `GET /api/bidops/raw-notices/323456792795418624/attachments` returns 1 attachment and review detail `323460627374936064` includes 1 attachment.
- Browser smoke test confirmed Raw notice detail and review detail show the `公开附件` section, Chinese attachment name `10成交候选人公示-补充17FG05.pdf`, download/extract statuses, and an `打开附件` public link.

## 2026-06-12 BidOps Review Approval Transaction Fix

Completed:

- Fixed review approval failing under MySQL retry-on-failure with `MySqlRetryingExecutionStrategy does not support user-initiated transactions`.
- Added `IUnitOfWork.ExecuteInTransactionAsync` overloads so tenant business services can execute manual transactions inside EF Core's configured execution strategy without injecting DbContext directly.
- Implemented the execution-strategy transaction wrapper in `TenantUnitOfWork`.
- Updated `BidOpsReviewService.ApproveAsync` to run the formal Notice/Package/Requirement import transaction through the UnitOfWork execution strategy.
- Updated `ServiceBase` transaction helpers to reuse the same UnitOfWork execution-strategy path.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 16 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 63 passed.
- Local WebApi and Worker were restarted in `BidOpsLocal`.
- Authenticated API smoke test approved review task `323460760699277314` successfully and returned formal notice `323637502403547136`; the review task status became `Approved`.

## 2026-06-12 BidOps Attachment Text Viewer

Completed:

- Added `RawAttachmentTextDto` and `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/text` so extracted attachment text can be read without exposing file-store keys.
- Updated BidOps query services to load attachment extracted text from `IBidOpsFileStore` through `RawAttachment.TextContentStorageKey`, capped at 120,000 characters for UI response safety.
- Added `rawNoticesApi.attachmentText` and a `查看文本` action in `RawAttachmentTable`; Raw notice detail and review detail pass the Raw notice ID so the shared attachment table can open the extracted text drawer.
- Fixed `BidOpsLocal` file-store path resolution to prefer the repository root for relative paths and added `src/Atlas.Worker/var/bidops-storage` as a fallback so WebApi can read existing local attachment text produced by Worker.

Verification:

- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --verbosity minimal` succeeded: 63 passed. A parallel build/test run emitted one transient DLL copy retry warning, then completed successfully.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- Local WebApi and Worker were restarted in `BidOpsLocal`.
- Authenticated API smoke test confirmed `GET /api/bidops/raw-notices/323456792795418624/attachments/323800000000000116/text` returns file name `10成交候选人公示-补充17FG05.pdf` and extracted text length `44640`.
- Browser smoke test confirmed Raw notice detail shows `公开附件` with `查看文本`, and clicking it opens a drawer titled `10成交候选人公示-补充17FG05.pdf` with extracted text content.

## 2026-06-12 BidOps PDF Text Extraction Fix

Completed:

- Replaced the Raw PDF byte/regex extractor with `PdfPig` page text extraction for PDF attachments.
- Added `PdfPig` to central package management and the BidOps module.
- Added a PDF extractor regression test that generates a PDF, extracts text through `BidOpsTextExtractor`, and verifies raw PDF object markers such as `endstream`/`endobj` are not returned.
- Rebuilt local Worker with restored dependencies so PdfPig runtime assemblies are copied to the Worker output.
- Re-extracted all 39 local downloaded PDF attachments in `atlas_bidops_runtime`; all returned to `TextExtractStatus = Succeeded` with populated `TextContentStorageKey`.

Verification:

- Direct extractor probe on `10成交候选人公示-补充17FG05.pdf` returned readable Chinese text length `1703`, beginning with `国网河南电力新乡供电公司 2026 年第三次服务授权批次竞争性谈判采购 成交候选人公示-补充`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor" --verbosity minimal` succeeded: 2 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 64 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded with 0 warnings and 0 errors.
- `tools\check-architecture-governance.ps1` succeeded.
- Authenticated API smoke test confirmed `GET /api/bidops/raw-notices/323456792795418624/attachments/323800000000000116/text` returns Chinese title/content, length `1703`, and no raw PDF `endstream`/`endobj` markers.

## 2026-06-12 BidOps PDF Text Layout Preservation

Completed:

- Updated `BidOpsTextExtractor` so PDF text uses PdfPig's content-order extraction with PDF-specific whitespace normalization.
- Preserved PDF page line breaks instead of applying the general `\s+` collapse that made all extracted PDF text appear pasted together in the attachment text drawer.
- Kept fallback word extraction for pages where content-order extraction does not produce human-readable text.
- Updated the PDF extractor regression test to assert that separate PDF text lines remain separated by a newline and are not collapsed into one sentence.
- Re-extracted all 39 local downloaded PDF attachments in `atlas_bidops_runtime` so existing `.extracted.txt` files use the new readable line-preserving format.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor" --verbosity minimal` succeeded: 2 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 64 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded with 0 warnings and 0 errors.
- `tools\check-architecture-governance.ps1` succeeded.
- Local DB check confirmed all 39 PDF attachments returned to `TextExtractStatus = Succeeded`.
- Authenticated API smoke test confirmed `GET /api/bidops/raw-notices/323456792795418624/attachments/323800000000000116/text` returns `10成交候选人公示-补充17FG05.pdf` with readable Chinese text, 88 lines, 3 blank lines, and no raw PDF `endstream`/`endobj` markers.

## 2026-06-12 BidOps Raw Notice Chinese Display Text

Completed:

- Added `BidOpsRawNoticeTextFormatter` to convert source-oriented Raw notice text into review-friendly Chinese display text at query time.
- Mapped common State Grid fields such as `SourceUrl`, `Doctype`, `PublishOrgName`, `ListPublishTime`, `resultValue.notice.CONT`, `bidagtName`, and `bidOrgName` to Chinese labels.
- Hidden internal source IDs and volatile file-token fields that are not useful for manual review, while keeping the original Raw text files unchanged in `IBidOpsFileStore`.
- Stripped HTML tags from embedded public notice content before returning it to the UI.
- Updated `RawNoticePreview` so announcement text renders as a document-style article instead of a monospace code block; attachment extracted text keeps the monospace variant for table-like PDF text.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsRawNoticeTextFormatter|FullyQualifiedName~BidOpsTextExtractor" --verbosity minimal` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 65 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `tools\check-architecture-governance.ps1` succeeded.
- Authenticated API smoke test confirmed review task `323460760699277314` returns Chinese Raw text labels, including `公告类型：中标/成交结果公告` and `公告内容：详见附件`, with no `resultValue`, `Doctype:`, `ProjectCode:`, or HTML tag leakage.
- Browser smoke test confirmed the review detail `公告全文` region renders as an `ARTICLE` with system document font and `pre-wrap` line preservation, not as a monospace code block.

## 2026-06-12 BidOps Package Detail APIs

Completed:

- Added `GET /api/bidops/packages/{id}` for package base information, linked notice summary, requirement counts, reject-risk counts, and package timestamps.
- Added `GET /api/bidops/packages/{id}/timeline` as an MVP read model synthesized from existing notice/package/requirement timestamps.
- Expanded `TenderPackageDto` and added `PackageTimelineItemDto`.
- Updated the package detail frontend to load package detail, timeline, and requirements concurrently, replacing the previous backend-gap alerts.
- Added Chinese display for the `New` package status.
- Updated frontend API gap and API mapping docs so package detail/timeline are no longer listed as missing.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~PackagesController|FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 19 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 66 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `tools\check-architecture-governance.ps1` succeeded.
- Authenticated API smoke test confirmed package `323637502609068032` returns package detail and a 4-event timeline: `公告发布`, `正式公告入库`, `包件创建`, `要求项生成`.
- Browser smoke test confirmed `/bidops/packages/323637502609068032` no longer shows `后端接口待补充`, displays `包件基础信息`, `关联公告`, `包件时间线`, and `要求项`, and renders package status as `新建`.

## 2026-06-12 Background Task Observability P0

Completed:

- Added shared background job operations DTOs, masked payload output, tenant-scoped list/detail/summary queries, manual retry, and safe cancel in `Atlas.BackgroundTasks`.
- Added `/api/ops/background-jobs`, `/api/ops/background-jobs/summary`, `/api/ops/background-jobs/{id}`, retry, and cancel endpoints.
- Added `/api/bidops/operations/dashboard`, `/api/bidops/operations/jobs`, `/api/bidops/operations/config-check`, `/api/bidops/operations/channels/health`, and BidOps retry/cancel endpoints.
- Added frontend pages `/ops/jobs`, `/ops/jobs/:id`, `/bidops/operations`, `/bidops/operations/jobs`, and `/bidops/operations/channels`.
- Added config warning checks for disabled workers, missing `bidops` queue, disabled recurring runner, missing ScheduledScan/Recovery tenant IDs, no enabled sources/channels, and NeedLogin sources.
- Added documentation: `docs/background_tasks_observability.md`, `docs/bidops_operations_dashboard.md`, and `docs/log_query.md`.

Verification:

- `dotnet restore Atlas.sln` succeeded.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --verbosity minimal` succeeded with 0 warnings and 0 errors.
- `dotnet build Atlas.sln --no-restore --verbosity minimal` succeeded; only existing test-project nullable/EF warnings were reported.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BackgroundTaskOperationsTests|FullyQualifiedName~RuntimeModeRegistrationTests|FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 41 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --verbosity minimal` succeeded: 69 passed.
- `dotnet test Atlas.sln --no-build --verbosity minimal` still fails in existing integration-test setup: `Atlas.Integration.Tests` has 36 failures from missing `ICurrentIdentity` in cache integration tests, missing `atlas_global.databasemasterservers`, and old MySQL key-length limits. Core, Services, and Infrastructure.Http tests passed in that run.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `powershell -ExecutionPolicy Bypass -File tools\check-architecture-governance.ps1` succeeded.
- `docker compose config` succeeded.
- Authenticated API smoke test as `bidops_admin` confirmed `GET /api/ops/background-jobs/summary`, `GET /api/ops/background-jobs`, `GET /api/bidops/operations/config-check`, `GET /api/bidops/operations/dashboard`, and `GET /api/bidops/operations/channels/health` all return data.
- Browser smoke test confirmed `/bidops/operations`, `/bidops/operations/jobs`, `/bidops/operations/channels`, `/ops/jobs`, and a job detail page render without 500s, without `后端接口待补充`, and without console errors.

## 2026-06-12 BidOps Product Module Blueprint

Completed:

- Added `docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md` as the BidOps product/module blueprint.
- Defined 13 first-level modules, their goals, routes, APIs, core objects, permissions, background tasks, current status, and Atlas integration boundaries.
- Mapped existing Crawl, Review, Business, and Operations capabilities into the expanded module system while preserving current APIs and permissions.

Verification:

- `git diff --check -- docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps Module Gaps

Completed:

- Added `docs/BIDOPS_MODULE_GAPS.md` to compare the 13-module blueprint with current backend entities, APIs, jobs, permissions, and frontend routes.
- Classified each module as implemented MVP, partially implemented, or not implemented, with concrete API, permission, data model, frontend, and background-task gaps.
- Documented recommended execution order and reiterated prohibited non-compliant capabilities.

Verification:

- `git diff --check -- docs/BIDOPS_MODULE_GAPS.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps 13-Module Contract Detail

Completed:

- Expanded `docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md` section 5 into a fixed implementation contract for all 13 modules.
- Each module now explicitly lists its goal, routes, APIs, permissions, background tasks, and core objects, separating current implementation from planned capabilities.
- Replaced shorthand API references with full route paths for planned dashboard, intelligence, processing, opportunity, supplier, matching, pursuit, response, outcome, compliance, operations, and settings APIs.

Verification:

- `git diff --check -- docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps Source-Based Module Status

Completed:

- Updated `docs/BIDOPS_MODULE_GAPS.md` to mark all 13 modules as implemented, partially implemented, or not implemented based on the current `src/Atlas.Modules.BidOps` source tree.
- Added source evidence for implemented and partially implemented modules, including controllers, entities, services, queries, documents, background jobs, and constants.
- Added explicit source-based absence notes for modules that do not yet have directories, entities, controllers, services, or jobs.

Verification:

- `git diff --check -- docs/BIDOPS_MODULE_GAPS.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps Non-Breaking Evolution Guardrail

Completed:

- Added a non-breaking evolution rule to `docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md`.
- Updated `docs/BIDOPS_MODULE_GAPS.md` to state that current controllers, API routes, permissions, frontend routes, and request/response semantics must remain compatible while the 13 modules are added.
- Recorded the compatibility decision in `docs/DECISIONS.md`.

Verification:

- `git diff --check -- docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md docs/BIDOPS_MODULE_GAPS.md docs/DECISIONS.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps Frontend Module Placeholders

Completed:

- Added a shared `ComingSoonPage` for BidOps routes that are not implemented yet.
- Reworked the Atlas Admin sidebar into the 13 BidOps module groups while keeping existing working routes and pages available.
- Added placeholder routes for planned dashboard, intelligence, processing, opportunity, public organization, supplier, matching, pursuit, response, outcome, compliance, operations, and settings pages.
- Updated the BidOps home page to show the 13 modules and route unimplemented modules to `ComingSoon`.
- Updated `docs/BIDOPS_MODULE_GAPS.md` to record that frontend menu/route placeholders are complete.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- Browser smoke test confirmed `/bidops` shows all 13 modules, `/bidops/suppliers` renders `ComingSoon`, `/bidops/operations/logs` renders `ComingSoon`, `/bidops/crawl/sources` still renders the existing real page, and no console errors were reported.

## 2026-06-12 BidOps Unimplemented API Guardrail

Completed:

- Strengthened `docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md` backend placeholder rules so unimplemented APIs must not return fake successful responses.
- Updated `docs/BIDOPS_MODULE_GAPS.md` to state that planned API gaps can remain unregistered or return `501 NotImplemented`, but must not return `200 OK` with empty/default data.
- Recorded the API semantics decision in `docs/DECISIONS.md`.

Verification:

- Scanned current BidOps controllers, queries, and services for placeholder success patterns. No unimplemented backend placeholder success endpoint was found; existing empty-array returns are tied to implemented query paths.
- `git diff --check -- docs/BIDOPS_PRODUCT_MODULE_BLUEPRINT.md docs/BIDOPS_MODULE_GAPS.md docs/DECISIONS.md docs/IMPLEMENTATION_LOG.md` succeeded.

## 2026-06-12 BidOps P0 Implementation Task Split And RawNotice Pipeline

Completed:

- Added `docs/BIDOPS_IMPLEMENTATION_TASKS.md` to split the 13-module blueprint into executable batches from P0 through Phase G.
- Added `bidops.ops.read` and `bidops.ops.manage` to the BidOps authorization catalog and frontend permission constants while keeping existing operations APIs compatible with old crawl permissions.
- Added the real RawNotice pipeline read model backed by existing RawNotice, RawAttachment, staging, ReviewTask, Notice, TenderPackage, and RequirementItem data.
- Added `GET /api/bidops/raw-notices/{id}/pipeline` and `GET /api/bidops/operations/raw-notices/{id}/pipeline`; missing RawNotice returns 404 instead of fake success data.
- Added a RawNotice detail pipeline panel showing 公告采集、附件处理、结构化解析、人工审核、正式入库.
- Updated module blueprint, gap analysis, implementation task status, and decisions for the completed P0 items.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-restore --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 21 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --filter "FullyQualifiedName~BackgroundTaskOperationsTests|FullyQualifiedName~RuntimeModeRegistrationTests|FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 42 passed.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation/chunk-size warnings.
- `git diff --check` succeeded; Git reported an existing CRLF/LF warning for `Directory.Packages.props`.
- `dotnet build Atlas.sln --no-restore --verbosity minimal` initially hit local file locks from running `Atlas.WebApi`/`Atlas.Worker`; after stopping those local dev processes it succeeded with 0 warnings and 0 errors.
- Authenticated API smoke test as `bidops_admin` confirmed both `GET /api/bidops/raw-notices/{id}/pipeline` and `GET /api/bidops/operations/raw-notices/{id}/pipeline` return 5 pipeline steps for RawNotice `323666828616404992`.
- Browser smoke test confirmed `/bidops/crawl/raw-notices/323666828616404992` renders `处理流水线`, `公告采集`, `正式入库`, and `公开附件`, with no console error logs.

## 2026-06-12 BidOps Crawl Run Logs P0

Completed:

- Added `CrawlRunLog` as a BidOps authorization data resource.
- Added `CrawlRunLogDto`, `CrawlRunLogSearchQuery`, repository-backed query methods, and `CrawlRunLogsController`.
- Added real APIs: `GET /api/bidops/crawl-run-logs` and `GET /api/bidops/crawl-run-logs/{id}`. Missing logs return 404.
- Replaced the `/bidops/intelligence/run-logs` ComingSoon route with a real run-log list page and added `/bidops/intelligence/run-logs/{id}` detail page.
- Updated the product blueprint, gap analysis, and implementation task status for P0-04.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 21 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Authenticated API smoke test as `bidops_admin` confirmed `GET /api/bidops/crawl-run-logs` returned 389 total logs and `GET /api/bidops/crawl-run-logs/323667005276295168` returned operation `StateGridEcpCrawl`.
- Browser smoke test confirmed `/bidops/intelligence/run-logs` renders a real list instead of `ComingSoon`, and `/bidops/intelligence/run-logs/323667005276295168` renders the log detail and full message with no console error logs.

## 2026-06-12 BidOps Processing Failure Queue P0

Completed:

- Added `ProcessingFailureDto`, `ProcessingFailureSearchQuery`, repository-backed query method, and `ProcessingFailuresController`.
- Added real API `GET /api/bidops/processing/failures`, currently backed by RawNotice records with `Status = Failed`.
- Replaced `/bidops/processing/failed` ComingSoon route with a real failure queue page. Empty state means no failed RawNotice data exists for the tenant.
- Updated product blueprint, gap analysis, and implementation task status for P0-05.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --filter "FullyQualifiedName~BidOpsModuleTests" --verbosity minimal` succeeded: 21 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Authenticated API smoke test as `bidops_admin` confirmed `GET /api/bidops/processing/failures` returns a valid empty page when there are no failed RawNotice records.
- Browser smoke test confirmed `/bidops/processing/failed` renders a real searchable failure queue with empty-state text and no `ComingSoon` marker or console error logs.

## 2026-06-12 BidOps RawNotice Reparse P0

Completed:

- Added `POST /api/bidops/raw-notices/{id}/reparse` under the existing `bidops.review.approve` permission.
- Added `ReparseRawNoticeRequest` and review-service support that reopens RawNotice, NoticeStaging, and ReviewTask state, then enqueues `bidops.document.attachment-process` for Worker execution.
- Manual reparse now carries a force-run id so the following structured parse job uses a fresh deduplication key even when the RawNotice content hash is unchanged.
- Approved or already imported RawNotice rows are rejected for MVP reparse to avoid mutating formal Notice/TenderPackage business data.
- Added frontend reparse actions on RawNotice detail and the processing failure queue.
- Hardened attachment processing so duplicate downloaded file content does not violate the RawAttachment unique index during recovery or reparse runs.
- Updated blueprint, gap analysis, task split, and decisions for P0-06.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests` succeeded: 23 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- Authenticated API smoke test as `bidops_admin` confirmed reparse of RawNotice `323666828616404992` enqueued job `323699653726048256`, Worker completed it, and the RawNotice returned to `ReviewPending` with ReviewTask/NoticeStaging `Pending`.
- Authenticated API smoke test confirmed an approved RawNotice `323459050413101056` returns HTTP 400 on reparse.
- Browser smoke test confirmed `/bidops/processing/failed` renders the real failure queue with no console errors, and `/bidops/crawl/raw-notices/323666828616404992` renders `重解析`, `处理流水线`, `公开附件`, and the public `详情 URL` with no console errors.

## 2026-06-12 BidOps Worker Heartbeat P0

Completed:

- Added Global `BackgroundWorkerHeartbeat` entity, EF configuration, DbSet, and migration `20260612060040_v0.2.3-background-worker-heartbeat`.
- Added best-effort `BackgroundWorkerHeartbeatService` and shared `BackgroundWorkerHeartbeatState`; Worker records process identity, queues, enabled capabilities, current job, start time, and last heartbeat.
- Wired `BackgroundJobWorker` to use the heartbeat worker id for claims and expose current running job state.
- Added shared worker operations query service and real API `GET /api/ops/workers`.
- Added frontend Worker pages at `/ops/workers` and `/bidops/operations/worker-heartbeats`; the BidOps page defaults to `queue=bidops` and warns when no online bidops Worker exists.
- Updated blueprint, gap analysis, operations docs, and task split for P0-07.

Verification:

- `dotnet build src\Atlas.Data.Global.Migrations\Atlas.Data.Global.Migrations.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 23 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet ef database update` against the existing local `atlas_global_bidops` database could not run because the local DB has historical tables but missing EF migration history, so EF tried to apply the initial migration and hit existing `DatabaseInstances`. For local smoke only, the new heartbeat table/indexes were created idempotently with the same schema; formal migration remains in `src/Atlas.Data.Global.Migrations`.
- Authenticated API smoke test as `bidops_admin` confirmed `GET /api/ops/workers?queue=bidops&pageIndex=1&pageSize=20` returned one online Worker with queues `default`, `tenant`, `export`, and `bidops`.
- Browser smoke test confirmed `/bidops/operations/worker-heartbeats` and `/ops/workers` render real Worker lists, not `ComingSoon`, and report no console errors.

## 2026-06-12 BidOps Controlled Attachment File Access P0

Completed:

- Added `RawAttachmentFileResult` and `IBidOpsQueryService.OpenRawAttachmentFileAsync` to open original downloaded attachment binaries through `IBidOpsFileStore`.
- Added authorized API `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/file`; default response is inline preview with range support, and `?download=true` returns an attachment download.
- Missing RawNotice, missing attachment, empty storage key, or missing local file returns 404 instead of fake success.
- Updated `RawAttachmentTable` with `预览` and `下载` actions for local files. The actions fetch blobs through Axios so Authorization/Tenant/Store headers are preserved; the public source link remains labeled `来源`.
- Updated blueprint, gap analysis, task split, and decisions for P0-08.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 23 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Authenticated API smoke test as `bidops_admin` confirmed `GET /api/bidops/raw-notices/323666421458538496/attachments/323666421462732800/file` returns `200 OK`, `Content-Type: application/pdf`, `Content-Disposition: inline`, range support, and 105250 bytes.
- Authenticated API smoke test confirmed `?download=true` returns `Content-Disposition: attachment` and the same 105250 bytes.
- Browser smoke test confirmed Raw notice detail `323666421458538496` shows `预览`, `下载`, `来源`, and `查看文本` actions with no page console errors. Inspecting the opened blob/about preview tab was blocked by the Browser tool URL policy, so preview content was verified through the API file-stream smoke instead.

## 2026-06-12 BidOps Opportunity MVP Phase B

Completed:

- Added tenant-owned opportunity entities and configuration: `Opportunity`, `OpportunityStageHistory`, and `OpportunityWatch`, with `bidops_` table names and tenant-scoped uniqueness for one active opportunity per package.
- Added tenant migration `20260612064052_v0.2.4-bidops-opportunities`.
- Added `IBidOpsOpportunityService`, `BidOpsOpportunityService`, and `OpportunitiesController` for list/detail/create/update/watch/assess/stage flows.
- Added BidOps opportunity capability, permissions, data resource, and menu entry while keeping existing business read compatibility for list/detail APIs.
- Added frontend opportunity API client, Chinese status/type labels, opportunity list page, detail page, watchlist page, and package-detail create-opportunity action.
- Kept `/bidops/opportunities/calendar` as `ComingSoon`; no fake calendar API or success data was introduced.
- Updated product blueprint, module gap analysis, task split, and decisions for Phase B.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --nologo --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 24 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-opportunities ...` succeeded against the local BidOps databases. Formal `MigrationJob apply` was not run locally because the existing tenant DB lacks migration history and would try to replay historical migrations into existing tables.
- Authenticated API smoke as `bidops_admin` confirmed opportunity list/detail/create/watch/assess/stage work, duplicate active opportunity creation returns HTTP 400, and missing data does not return fake success.
- Browser smoke confirmed `/bidops/opportunities` and `/bidops/opportunities/{id}` render real pages with Chinese labels and no console errors.

## 2026-06-12 BidOps Dashboard And Opportunity Maintenance Phase B

Completed:

- Added `GET /api/bidops/dashboard/summary` through `BidOpsDashboardController`.
- Extended `BidOpsOperationsQueryService` with a real business summary based on tenant RawNotice, ReviewTask, Notice, TenderPackage, RequirementItem, and Opportunity data.
- Added `bidops.dashboard.read` to the authorization catalog, frontend constants, and local seed data; runtime dashboard API keeps `bidops.business.read` compatibility for existing roles.
- Replaced `/bidops/dashboard` `ComingSoon` with a real dashboard page showing today counts, pending review, active opportunities, deadline risk, opportunity funnel, value distribution, todos, and high-value opportunities.
- Added `BidOpsOpportunityMaintenanceService`, `BidOpsOpportunityMaintenanceTask`, and four background job handlers: `bidops.opportunity.value-assessment`, `deadline-reminder`, `watch-reminder`, and `stale-state-scan`.
- Updated BidOpsLocal Worker config so OpportunityMaintenance runs on startup and every 10 minutes for tenant `300001`.
- Updated blueprint, gap analysis, runbook, task split, and decisions for B-04/B-05.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal` succeeded after stopping the old local WebApi process that locked `Atlas.Modules.BidOps.dll`.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --nologo --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 25 passed.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `tools\Atlas.LocalSetup ensure-bidops-opportunities` refreshed local permissions; authenticated context for `bidops_admin` showed `bidops.dashboard.read` and 27 permissions.
- Authenticated API smoke confirmed `GET /api/bidops/dashboard/summary` returned real local counts: 1 active opportunity, 17 pending review tasks, 7 stage buckets, and 6 todos.
- Browser smoke confirmed `/bidops/dashboard` renders real dashboard content, not `ComingSoon`, and had no page console errors.
- Started BidOpsLocal Worker and confirmed all four opportunity maintenance jobs succeeded with real job results in `GET /api/ops/background-jobs/{id}`.

## 2026-06-12 BidOps Supplier Capability MVP Phase C

Completed:

- Added tenant-owned supplier entities and configuration: `Supplier`, `SupplierContact`, `SupplierCapability`, and `SupplierEvidenceDocument`.
- Added tenant migration `20260612084739_v0.2.5-bidops-suppliers`.
- Added supplier capability, permissions, data resources, service registration, controller routes, and local setup seeding while preserving existing BidOps controllers and permissions.
- Added real APIs for supplier list/detail/create/update, contact add, capability add, and evidence-document metadata add. Missing suppliers return 404; unimplemented child list/evidence-expiry APIs are not faked.
- Added `BidOpsSupplierMaintenanceService`, `BidOpsSupplierMaintenanceTask`, and `SupplierEvidenceExpiryScanJobHandler` for `bidops.supplier.evidence-expiry-scan`.
- Updated the frontend with supplier API clients, DTOs, Chinese status/type labels, `/bidops/suppliers`, and `/bidops/suppliers/:id`; supplier capability map, evidence-expiry list, and performance routes remain `ComingSoon`.
- Updated implementation task split, product blueprint, module gaps, runbook, and decisions for Phase C.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests` succeeded: 26 passed.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `tools\Atlas.LocalSetup ensure-bidops-suppliers` succeeded against the local BidOps databases after removing local-only long composite indexes that exceed older MySQL key limits.
- Authenticated API smoke as `bidops_admin` created supplier `323746978456539136`, added contact, capability, and evidence metadata, and confirmed detail returns all three child collections with `ExpiringSoon` evidence status.
- BidOpsLocal Worker processed `bidops.supplier.evidence-expiry-scan` successfully with real job results.
- Browser smoke confirmed `/bidops/suppliers` and `/bidops/suppliers/323746978456539136` render real pages with Chinese labels and no console errors.

## 2026-06-12 BidOps Matching And Go-No-Go MVP Phase D

Completed:

- Added tenant-owned matching entities and configuration: `SupplierMatchRun`, `SupplierMatchResult`, `MissingEvidenceCheck`, and `GoNoGoDecision`.
- Added tenant migration `20260612091842_v0.2.6-bidops-matching`.
- Added matching capability, permissions, data resources, service registration, controller routes, Worker handler, and LocalSetup seeding while preserving existing BidOps controllers and routes.
- Added real APIs for starting package supplier matching, listing matching runs, reading run details/results, and recording/listing Go/No-Go decisions. Missing packages/runs return errors instead of fake success.
- Implemented `bidops.matching.supplier-match-run` as a Worker job. The WebApi only enqueues and queries matching work.
- Added frontend matching API clients, DTOs, Chinese status/type labels, `/bidops/matching/runs`, `/bidops/matching/runs/:id`, package-detail match action, and Go/No-Go decision display/entry.
- Updated the frontend auth guard so authenticated sessions refresh `/api/auth/context` once per app startup, preventing stale cached permissions from hiding newly granted BidOps menus.
- Updated implementation task split, product blueprint, module gaps, runbook, and decisions for Phase D.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests` succeeded: 27 passed.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded after stopping the local WebApi process that locked `Atlas.Modules.BidOps.dll`.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded after stopping the local Worker process that locked `Atlas.Modules.BidOps.dll`.
- `tools\Atlas.LocalSetup ensure-bidops-matching` succeeded against the local BidOps databases.
- Authenticated API smoke as `bidops_admin` confirmed `bidops.matching.read/run/decide` permissions, started matching for package `323637502609068032`, Worker completed run `323758574222315520` with status `Succeeded`, 3 results, and 3 missing-evidence checks.
- Authenticated API smoke recorded decision `323758680367566848` with decision `Hold` for supplier `323746978456539136`, and package decisions returned the new record.
- Browser smoke confirmed `/bidops/matching/runs` and `/bidops/matching/runs/323758574222315520` render real pages with matching menu entries, run data, decision data, Chinese labels, and no console errors.

## 2026-06-12 BidOps Pursuit MVP Phase E

Completed:

- Added tenant-owned pursuit entities and configuration: `Pursuit`, `PursuitTask`, and `PursuitFollowRecord`.
- Added tenant migration `20260612100420_v0.2.7-bidops-pursuits`.
- Added pursuit capability, permissions, data resources, service registration, controller routes, and LocalSetup seeding while preserving existing BidOps controllers and routes.
- Added real APIs for pursuit list/detail/create/update, status transitions, task list/create/update, and follow-record list/create. Missing pursuits return 404; calendar and response-matrix APIs remain unimplemented.
- Enforced one active pursuit per package through `ActiveMarker` and tenant-scoped uniqueness. Creating from a Go/No-Go decision requires a `Go` decision.
- Added frontend pursuit API clients, DTOs, Chinese stage/task/follow labels, `/bidops/pursuits`, `/bidops/pursuits/:id`, `/bidops/pursuits/my-tasks`, and package-detail create-pursuit action. `/bidops/pursuits/calendar` remains `ComingSoon`.
- Updated implementation task split, product blueprint, module gaps, runbook, and decisions for Phase E.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --nologo --verbosity minimal` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 28 passed.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj /nodeReuse:false --nologo --verbosity minimal` succeeded after stopping local WebApi/Worker processes that held old output files.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj /nodeReuse:false --nologo --verbosity minimal` succeeded.
- `tools\Atlas.LocalSetup ensure-bidops-pursuits --tenant atlas_bidops_runtime` succeeded against the local BidOps runtime database. An earlier run without `--tenant` initialized the default `atlas` database, which was not used by BidOpsLocal.
- Authenticated API smoke as `bidops_admin` confirmed `bidops.pursuit.read/manage/task.manage/follow-record.manage`, created pursuit `323771539663228928`, task `323771540338511872`, follow record `323771540804079616`, updated task status to `InProgress`, and updated pursuit stage to `Preparing`.
- Browser smoke confirmed `/bidops/pursuits`, `/bidops/pursuits/323771539663228928`, and `/bidops/pursuits/my-tasks` render real pages with pursuit menu entries, task/follow data, Chinese labels, and no console errors.

## 2026-06-12 BidOps Package And Supplier Placeholder Cleanup

Completed:

- Added shared BidOps text-quality normalization for unreadable placeholders such as `?`, `？？`, replacement characters, and question-mark-plus-timestamp values.
- Removed persisted `UNSPECIFIED` package/lot defaults from deterministic and external-AI parsing. Unknown package numbers now stay empty and are shown as `待补录` in the frontend.
- Added deterministic extraction for Chinese package and lot labels in public text/HTML, including `包件号`, `包号`, `标包号`, `分包编号`, `标段号`, and `分标编号`.
- Hardened supplier create/update/contact/capability/evidence inputs so required fields cannot be all unreadable placeholders, and optional placeholder fields normalize to empty.
- Added frontend display guards for package numbers and supplier names across package, review, matching, pursuit, and supplier pages.
- Added `tools/Atlas.LocalSetup repair-bidops-data-quality` and ran it against `atlas_bidops_runtime`; local historical `UNSPECIFIED` and question-mark supplier values now display as `待补录` / `待补录厂家`.
- Normalized paged result numeric fields in `useTableQuery` so Element Plus pagination no longer receives backend BigInt string totals.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 30 passed.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- Browser smoke confirmed package and supplier pages no longer expose `UNSPECIFIED` or consecutive question-mark placeholders; supplier rows show `待补录厂家` for repaired historical data.

## 2026-06-12 BidOps Supplier Analysis

Completed:

- Clarified the supplier data source: current厂家 records come from the BidOps supplier master library through manual/API creation or local setup data; public notice crawling does not automatically create supplier master records.
- Added `GET /api/bidops/suppliers/analysis/summary` under existing `bidops.supplier.read` authorization. The read model aggregates real Supplier, capability, evidence, matching result, Go/No-Go decision, and pursuit data.
- Added supplier analysis DTOs and service logic without creating fake success data. At that point outcome-announcement extraction was still planned; it has since been implemented as `OutcomeSupplierRecord` public result leads, while full `SupplierPerformance` remains planned.
- Replaced the frontend supplier capability placeholder with `/bidops/suppliers/analysis`, keeping `/bidops/suppliers/capabilities` as a compatible alias.
- Updated product blueprint, module gaps, implementation tasks, and decisions to mark current supplier analysis as implemented and outcome-based supplier performance as still planned.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` succeeded: 30 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- Browser smoke confirmed `/bidops/suppliers/analysis` renders the real厂家分析 page, displays supplier source and outcome-extraction status, does not show `ComingSoon`, and has no console errors.
- Browser smoke confirmed legacy `/bidops/suppliers/capabilities` reaches the same analysis page.

## 2026-06-12 BidOps Public Outcome Supplier Leads

Completed:

- Added tenant-owned `OutcomeSupplierRecord` for public中标/成交/候选公示厂家线索, including source announcement, notice/package snapshots, supplier name, optional supplier/package links, outcome type, rank, amount, evidence text, confidence, and source hash.
- Added strict deterministic extraction for explicit supplier/candidate/winner fields and attached extracted text. Announcement intros, buyer/agency/publisher fields, supplier instructions, fee/payment/postage text, and generic company-name mentions are filtered out.
- Added `bidops.outcome.supplier-extract` Worker handler. Structured parsing now triggers outcome extraction after parsing, and `POST /api/bidops/suppliers/outcome-records/backfill` can enqueue repeatable backfill batches.
- Added real read APIs: `GET /api/bidops/suppliers/outcome-records`, `GET /api/bidops/suppliers/outcome-summary`, and `GET /api/bidops/packages/{id}/historical-suppliers`.
- Extended `/api/bidops/suppliers/analysis/summary`, `/bidops/suppliers/analysis`, and package detail to surface public result supplier leads without auto-creating supplier master records or automatic contact workflows.
- Added tenant migration `20260612131223_v0.2.8-bidops-outcome-suppliers` and LocalSetup `ensure-bidops-outcomes`.
- Fixed outcome extraction persistence to call `SaveChangesAsync(raw.TenantId)` after using explicit tenant repository writes; otherwise background jobs could report extracted counts without committing rows.
- Updated product blueprint, module gaps, implementation tasks, and decisions to mark public result supplier leads as implemented while keeping full `SupplierPerformance`, win-rate analytics, and result review planned.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --nologo` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --nologo` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --nologo` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsOutcomeSupplierTextParser --nologo --verbosity minimal` succeeded: 5 passed.
- Full BidOps route/service/parser test pass succeeded: `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --nologo --verbosity minimal` with 35 passed.
- Authenticated API smoke as `bidops_admin` confirmed login, outcome summary/list, and repeatable backfill enqueue. Local full backfill over 54 outcome-like raw notices completed with `OutcomeRecordCount=0` after strict filtering, which is expected for the current local sample because it lacks explicit supplier-detail lines and previous template/buyer false positives were removed.
- Browser smoke confirmed `/bidops/suppliers/analysis` renders the public result supplier lead metrics and empty state without fresh console errors, and `/bidops/packages/323637502609068032` renders the historical supplier lead section and package requirements without fresh console errors.

## 2026-06-13 BidOps ZIP/Excel Attachments And State Grid Manual Detail Import

Completed:

- Added Worker text extraction for ZIP archives and OpenXML Excel workbooks so tender package details in nested `.docx`/`.xlsx` files become searchable extracted attachment text.
- Added conservative readable-text fallback for legacy `.doc` and `.xls` files without introducing a local Office conversion dependency.
- Updated State Grid WCM detail parsing to add `downLoadBid?noticeId=...` as a ZIP attachment when a `doci-bid` detail response exposes `fileFlag = 1`.
- Hardened State Grid `doci-bid` attachment discovery so missing/unknown `fileFlag` values still keep the public `downLoadBid?noticeId=...` ZIP candidate when no explicit attachment list was discovered, while explicit `fileFlag = 0/false` still suppresses the candidate.
- Added the same `doci-bid` ZIP candidate to fallback State Grid detail documents so transient detail API parsing failures do not silently drop public procurement announcement ZIP attachments.
- Updated manual URL import jobs to detect public State Grid portal detail URLs and fetch the public WCM detail/attachment metadata in Atlas.Worker before falling back to the old manual URL-only import path.
- Added tests for XLSX worksheet text extraction, recursive ZIP extraction, State Grid bid ZIP attachment discovery, and State Grid portal detail URL parsing.
- Updated the runbook and decisions for ZIP/Excel extraction and State Grid manual detail imports.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor"` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests` succeeded earlier in this phase: 37 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~StateGridEcpWcmParser|FullyQualifiedName~BidOpsTextExtractor" --no-restore --nologo --verbosity minimal` succeeded after the manual-import change: 11 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~StateGridEcpWcmParser|FullyQualifiedName~BidOpsTextExtractor" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after hardening missing-fileFlag attachment discovery: 15 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after the attachment discovery hardening and parser/frontend preview changes: 47 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after stopping old local processes that locked copied DLLs.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after stopping old local processes that locked copied DLLs.
- Local BidOps smoke started WebApi on `http://127.0.0.1:5260`, Worker `Atlas.Worker.exe`, and the existing Vite frontend on `http://localhost:5173`.
- Authenticated API smoke enqueued `bidops.raw.manual-url-import` job `323954000376500224` for `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128544990232_2018032900295987`; Worker created RawNotice `323954027123576832`.
- The State Grid detail response added one ZIP attachment, `北京电力交易中心有限公司2026年第一次服务公开谈判采购-公告文件.zip`, downloaded `52,078` bytes from `downLoadBid?noticeId=2606128544990232`, and extracted `11,582` characters of text.
- Authenticated API smoke for `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128525313769_2018032900295987` re-imported existing RawNotice `323791819534110720`; the parser registered `国网新源集团有限公司2026年临一批服务公开谈判采购-公告文件.zip`, downloaded `82,025` bytes from `downLoadBid?noticeId=2606128525313769`, and extracted attachment text successfully.
- Extracted attachment text includes the procurement scope table headers `分标编号`, `分标名称`, `包号`, `包名称`, `采购范围`, `服务期 /框架协议有效期`, and `实施地点`.
- The downstream structured parse completed with `packageCount=1`, `requirementCount=23`, and review task `323954072279453717` pending human review; no formal business import occurred.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- bidops-status ...` confirmed `RawNotices=112`, `RawAttachments=74`, `NoticeStaging=112`, and `ReviewTasks=112` after the smoke run.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal` succeeded earlier after ZIP/Excel extraction.
- `dotnet test Atlas.sln --no-build` still has existing non-BidOps local integration failures: cache integration missing `ICurrentIdentity`, missing local `atlas_global`, and old MySQL 767-byte key length limits.

## 2026-06-13 BidOps State Grid Attachment Table Recognition

Completed:

- Read `CODEX_BIDOPS_SGCC_ECP_ATTACHMENT_TABLE_EXTRACTION_SPEC.md` and implemented the P0/P1-compatible table recognition path without changing formal business schemas.
- Updated DOCX extraction to parse `word/document.xml` as WordprocessingML, preserve paragraphs, and render Word tables as Markdown tables with the nearest preceding heading.
- Added deterministic `BidOpsEcpProcurementTableParser` for State Grid ECP procurement attachment Markdown tables.
- `项目概况与采购范围` tables now produce multiple `BidOpsPackageExtract` candidates with lot code/name, package number/name, service period, and implementation place.
- `响应供应商须满足如下专用资格要求` tables now match rows back to packages and produce `Performance`, `Qualification`, `Personnel`, and `JointVenture` requirement candidates while skipping `/` / empty cells.
- The generic deterministic parser now prefers the SGCC table parser when standard table headers are present and otherwise falls back to the existing single-package text heuristic.
- Added tests for DOCX table Markdown output, 44-package/54-requirement SGCC table parsing, and table-header aliases such as `项目内容`, `服务期限`, and `服务地点`.
- Hardened SGCC qualification table handling for merged Word headers: DOCX extraction now fills blank child-header cells from parent header rows, and the Markdown parser can infer `分标` / `包号` / `包名称` when a previously extracted qualification table has those first three header cells blank.
- Manual reparse now forces attachment text extraction when carrying a force-run id so parser improvements can refresh already downloaded attachments before structured parsing.
- Updated the frontend raw/attachment text preview to render Markdown headings and tables as structured HTML instead of showing extracted table Markdown as plain text. The renderer stays intentionally small and safe: it supports headings, paragraphs, and tables without `v-html` or a new dependency.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor|FullyQualifiedName~BidOpsDeterministicNoticeParser|FullyQualifiedName~BidOpsEcpProcurementTableParser" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 14 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 44 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only the existing non-fatal Rollup annotation and chunk-size warnings.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded. An earlier parallel run failed only because a just-finished testhost temporarily locked `obj\Debug\net8.0\Atlas.Modules.BidOps.dll`; rerun passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- Local BidOps smoke re-ran `POST /api/bidops/raw-notices/323954027123576832/reparse` for the public SGCC URL `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128544990232_2018032900295987`.
- Pipeline verification returned `packageCount=44`, `requirementCount=54`, one downloaded/extracted ZIP attachment, and a pending human review task; no formal business import occurred.
- Review detail verification returned requirement distribution `Performance=44`, `JointVenture=7`, and `Qualification=3`.
- During local smoke, Worker had to be started with `BidOpsLocal` environment; the default Worker appsettings attempted RabbitMQ on `localhost:5672`, leaving database jobs pending.

## 2026-06-13 BidOps Manual Procurement Announcement Import Page

Completed:

- Replaced the frontend `手动导入` ComingSoon route with a real `/bidops/intelligence/manual-import` page.
- The page defaults to a single procurement announcement URL input and keeps source/channel/title/text as folded advanced fields for exceptional fallback imports.
- Submitting the form calls the existing enqueue-only `POST /api/bidops/raw-notices/import-url` API; WebApi still does not crawl synchronously.
- The page polls `GET /api/ops/background-jobs/{id}` for the manual import job, extracts `rawNoticeId=...` from the Worker result, then loads the RawNotice and `GET /api/bidops/raw-notices/{id}/pipeline`.
- The result panel shows job status, Raw status, announcement metadata, attachment/download/text-extraction counts, structured parse counts, and direct actions to Raw detail or the generated review task.
- Updated the Raw notice list action so `手动导入` opens the full import page instead of a small internal-ID dialog.
- Tightened the frontend route/menu permission to `bidops.crawl.import`, matching the backend import endpoint.
- Added a controller route assertion for `POST /api/bidops/raw-notices/import-url`.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only the existing Rollup annotation and chunk-size warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 47 passed.
- `git diff --check` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- Browser smoke logged in as local `bidops_admin`, opened `http://localhost:5173/bidops/intelligence/manual-import`, submitted `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128525313769_2018032900295987`, and confirmed the page displayed the existing manual import job, RawNotice title `国网新源集团有限公司2026年临一批服务公开谈判采购`, one downloaded/extracted ZIP attachment, and a pending review pipeline without console errors.

## 2026-06-13 BidOps Historical Attachment Backfill And Timezone Display

Completed:

- Added Worker job type `bidops.raw.attachment-backfill` and `RawAttachmentBackfillJobHandler`.
- Added `POST /api/bidops/raw-notices/backfill-attachments` to enqueue historical attachment repair without synchronous crawling in WebApi.
- The backfill scans historical public SGCC `doci-bid` 招标/采购公告 Raw notices, re-imports public detail metadata through the StateGridEcp crawler, records missing ZIP attachments, and enqueues attachment download/text extraction plus forced structured reparse.
- Approved, ignored, and already-formal Raw notices are skipped so the job does not silently mutate approved business data.
- Structured parsing now trims staging text fields to the actual tenant table limits, including the original migration's `varchar(256)` `RequirementStaging.OriginalText`, while keeping full extracted attachment text in `IBidOpsFileStore`.
- Added a Raw notice list action `补齐历史附件` that starts the backfill job with a conservative batch size.
- Hardened frontend datetime formatting so backend UTC timestamps, including `采集时间`, render in the browser's current timezone even when an ISO datetime arrives without an explicit zone suffix.
- Updated runbook and decisions for the historical backfill endpoint and timezone display behavior.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 47 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing non-fatal Rollup annotation and chunk-size warnings.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- Local backfill job `323976155956908032` processed 20 historical public SGCC `doci-bid` Raw notices with `candidates=20;refreshed=20;attachmentJobs=20;skipped=0;failed=0`. Its 20 attachment jobs succeeded. The first structured parse attempt exposed the local historical `RequirementStaging.OriginalText` `varchar(256)` limit; after truncation hardening, the 20 retry parse jobs succeeded while the old dead jobs remain as audit history.
- Local backfill job `323979300363702272` processed the next 20 missing historical attachments with `candidates=20;refreshed=20;attachmentJobs=20;skipped=0;failed=0`. Its 20 attachment jobs and 20 structured parse jobs succeeded.
- A final missing-only backfill job `323979869820162048` returned `candidates=0;refreshed=0;attachmentJobs=0;skipped=0;failed=0`.
- Local status after historical backfill: `RawNotices=123`, `RawAttachments=127`, `NoticeStaging=123`, `ReviewTasks=123`.
- Database spot-check for the current backfill scope found 53 eligible SGCC `doci-bid` 招标/采购 Raw notices, `MissingAttachments=0`, `FailedAttachmentOrExtraction=0`, and `WithDownloadedAndExtractedAttachment=53`.
- Browser smoke on `http://localhost:5173/bidops/crawl/raw-notices` confirmed the `补齐历史附件` action is visible, list `采集时间` renders the API UTC value in local `Asia/Shanghai` time, and a `doci-bid` detail page shows a ZIP attachment with `下载成功` and `提取成功`; no console errors were recorded.

## 2026-06-13 Background Job Runtime Duration Precision

Completed:

- Added `WaitMilliseconds` and `RunMilliseconds` to background job operation DTOs while keeping existing `WaitSeconds` and `RunSeconds` for compatibility.
- Changed background job operation mapping to calculate wait/run durations in milliseconds first, then derive whole-second compatibility values.
- Updated the frontend background job list and detail pages to display millisecond precision for sub-second jobs, so successfully completed short jobs no longer show as `0s`.
- Added a service test covering a completed 450ms background job.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BackgroundTaskOperationsTests --nologo --verbosity minimal /nodeReuse:false` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BackgroundTaskOperationsTests|BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 50 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded, and the local WebApi was restarted on `http://localhost:5260`.
- Authenticated API smoke confirmed succeeded jobs now return non-zero `runMilliseconds` values such as `34`, `27`, `21`, `10`, and `115` while the legacy `runSeconds` compatibility value can remain `0` for sub-second jobs.
- Browser smoke on `http://localhost:5173/ops/jobs?status=Succeeded` confirmed the `运行` column displays `34ms`, `27ms`, `21ms`, `10ms`, `115ms`, and `776ms` instead of `0s`.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only the existing non-fatal Rollup annotation and chunk-size warnings.
- `git diff --check` succeeded.

## 2026-06-13 Public Outcome Supplier Table Extraction

Completed:

- Confirmed public result supplier lead extraction is already wired through `OutcomeSupplierRecord` and Worker job type `bidops.outcome.supplier-extract`; it does not auto-create `Supplier` master records.
- Found local backfill still produced `outcomeSupplierRecords=0` because result PDF text often exposes award data as tables such as `分标编号 包号 成交人`, while the parser only handled same-line labels like `成交供应商：...`.
- Enhanced `BidOpsOutcomeSupplierTextParser` to keep explicit outcome-table context, parse package/lot rows, and extract supplier names from awarded/candidate result tables.
- Expanded supplier-name suffix recognition for public result rows to include institutional winners such as `测绘院` and `大学`, while still stopping table parsing at buyer/agency/contact/date boundary lines.
- Added PDF table cleanup so service/package-name fragments before the supplier are removed, fractured short internal company-name fragments are discarded, and buyer/agency names are not captured as suppliers.
- Re-ran local public outcome supplier backfill after the parser fix. Outcome leads changed from `0` to `1623`, with `616` distinct public outcome suppliers and `1` weak link to an existing supplier master record. The known bad `临颍公司` fragment no longer appears in the 漯河 sample.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsOutcomeSupplierTextParser --nologo --verbosity minimal` succeeded: 7 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal` succeeded: 49 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal` succeeded.

## 2026-06-13 Public Outcome Supplier Award Amount Display

Completed:

- Added `hasAwardAmount` and `sortBy` filters to public outcome supplier lead queries so callers can request only records with positive parsed award amounts.
- Added amount-based sorting for `OutcomeSupplierRecord` search, with default publish-time sorting kept for existing callers.
- Changed the supplier analysis page's right-side public-result lead table from recent leads to `带金额结果线索`, requesting `hasAwardAmount=true` and `sortBy=AwardAmountDesc`.
- Changed the public outcome supplier summary ordering so suppliers with positive cumulative award amount appear before count-only suppliers, then sort by cumulative amount and lead counts.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter BidOpsModuleTests --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 52 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only the existing non-fatal Rollup annotation and chunk-size warnings.
- Authenticated API smoke for `GET /api/bidops/suppliers/outcome-records?hasAwardAmount=true&sortBy=AwardAmountDesc&pageIndex=1&pageSize=5` returned `total=64` and `nullAmountOnPage=0`; sample rows included `国锋建筑设计有限公司` at `1536447.00` yuan and `山西蓝血科技有限公司` at `1462800.00` yuan.
- Browser smoke on `http://localhost:5173/bidops/suppliers/analysis` confirmed the page displays `带金额结果线索`, the amount column, and formatted values such as `¥13,637,000.00` and `¥1,536,447.00`.

## 2026-06-14 BidOps Crawl Adapter And Organization Master Data

Completed:

- Added `IBidOpsCrawlAdapter` and `StateGridEcpCrawlAdapter` so source-specific public crawl capabilities can be declared outside the crawler implementation. The SGCC adapter now participates in source/detail URL validation and declares inline HTML table support plus public attachment types including PDF, Office, Excel, and ZIP/RAR metadata.
- Added `bidops_buyer` tenant entity/configuration, `BuyerId` on `OutcomeSupplierRecord`, DTO exposure, and tenant migration `v0.2.9-bidops-buyer-master-data`.
- Added `IBidOpsOrganizationMasterDataService` to upsert buyer and supplier master records by normalized organization name from public outcome/candidate records. Existing records are linked and lightly refreshed instead of duplicated.
- Enhanced outcome supplier extraction to combine existing text parsing with the award evidence/table parser, so SGCC中标公告 HTML tables can populate project unit, lot number/name, package number/name, awarded supplier, and amount evidence.
- Updated local setup to create/protect buyer master data and to add `BuyerId` to local outcome-lead tables idempotently.
- Updated decisions and runbook to document adapter boundaries, Worker-side attachment processing, and the conservative master-data upsert policy.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 82 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-14 BidOps DeepSeek Re-Extraction

Completed:

- Added shared BidOps AI HTTP settings that support `Provider=DeepSeek`, default `BaseUrl=https://api.deepseek.com`, default model `deepseek-v4-pro`, optional `MaxOutputTokens`, per-use switches, and `DEEPSEEK_API_KEY` fallback without hardcoding credentials.
- Updated structured notice extraction to use the shared OpenAI-compatible settings, preserving deterministic fallback when AI is disabled or fails.
- Added `IBidOpsOutcomeSupplierAiExtractionService` and a DeepSeek/OpenAI-compatible implementation for public result/candidate notices. It sends strict JSON-mode prompts over Raw text plus extracted attachment text, asks DeepSeek to correct table/PDF/common-body-field cases, and returns supplier lead candidates only.
- Real DeepSeek smoke caught that the model may normalize `包1` to `1`; the outcome AI prompt and post-processing now preserve or restore package prefixes from evidence text before persistence.
- Kept outcome persistence Worker-owned: each RawNotice extraction still deletes/rebuilds only that RawNotice's `OutcomeSupplierRecord` rows, then syncs buyer/supplier master data through the existing conservative organization service.
- Added `ProcurementAgencyServiceFeeAmount` to `OutcomeSupplierRecord`, DTOs, EF configuration, tenant migration `v0.2.11-bidops-outcome-service-fee`, LocalSetup create/repair SQL, review detail, supplier analysis, and package historical supplier lead UI.
- Confirmed existing RawNotice `重解析` remains the re-extraction entry: WebApi enqueues, Worker force-runs attachment text extraction, structured parse, and outcome supplier extraction; approved/formal Raw notices remain protected.

Verification:

- `dotnet ef migrations add v0.2.11-bidops-outcome-service-fee --project src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --startup-project src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --context AtlasTenantDbContext --output-dir Migrations` succeeded after setting `ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES=Atlas.Modules.BidOps`; EF printed only existing model warnings.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 86 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-14 BidOps Review Detail Structured Modules

Completed:

- Updated the BidOps review detail page to detect the display notice kind for award-result, candidate-result, procurement, and fallback notices from staged notice metadata plus Raw notice text.
- `中标/成交结果公告` now shows a dedicated `中标/成交明细` table over outcome supplier lead rows, including supplier, result type, lot/package context, award amount, agency service fee, supplier link state, and evidence snippet.
- `推荐中标候选人公示` now shows `候选人明细` grouped by package context and a separate `对应包件明细` module with parsed package fields and requirement rows, even when candidate lead extraction is empty.
- `采购公告` now shows `采购公告明细` as a package summary table plus per-package detail modules with requirement tables, instead of leaving package/requirement data only as generic parsed text.
- Kept the generic package list and generic outcome lead table for unknown notice kinds only, so known notice types have clearer review surfaces while fallback behavior remains available.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only the existing non-fatal Rollup annotation notices and chunk-size warning.
- Browser smoke on `http://localhost:5173/bidops/review/tasks/323982173575188502` confirmed a real candidate notice displays `候选人明细`, `对应包件明细`, requirement rows, and `采购方` with no browser console errors.

## 2026-06-14 BidOps Wrapped PDF Outcome Table Extraction

Completed:

- Added a deterministic wrapped PDF/plain-text outcome table parser for SGCC result and candidate notices where PDF text extraction splits headers, lot numbers, lot names, suppliers, package numbers, and amount cells across many lines.
- Updated outcome supplier extraction routing so candidate notices use candidate-oriented evidence parsing and wrapped-table parsing, while award/result notices use award-oriented parsing. Candidate announcements no longer get persisted as `Awarded` only because the table header contains `推荐中标人`.
- Extracted wrapped-table fields into `OutcomeSupplierRecord` leads: supplier name, outcome type, candidate rank, lot number, lot name, package number, evidence text, and award/final-quote amount. Header units such as `万元` are normalized to yuan; percentages and discount rates are ignored as money.
- The review detail `候选人明细` and `中标/成交明细` modules now have deterministic backend records to display after RawNotice re-extraction. Existing historical Raw notices still need the `重解析` path to rebuild their outcome supplier records.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplier" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 13 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests|BidOpsReverseClosureTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 88 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- Default WebApi and Worker output builds were blocked by running local processes locking `bin\Debug` files: `Atlas.WebApi` PID 19652 and `Atlas.Worker` PID 12812. The code was verified with temp output directories; the local services must be restarted before the new extraction code is active.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.

## 2026-06-14 BidOps Notice List Filters And Updated Time

Completed:

- Added `NoticeType` to the review-task search query and wired the待审核池 frontend to filter by公告类型 using the shared notice type options.
- Added a dedicated formal notice search query with `NoticeType`, and updated the正式公告库 page to support公告类型 filtering.
- Exposed `CreatedAt` and `UpdatedAt` on Raw notice, review task, and formal notice DTOs, then displayed `最后更新时间` on原始公告、待审核池、正式公告库列表 and Raw/审核详情 metadata sections.
- Kept storage unchanged because BidOps entities already inherit Atlas `BaseEntity.CreatedAt/UpdatedAt`; this step only exposes and displays the existing audit fields.
- Added a lightweight contract test covering the new notice search query, review notice type filter, and updated-time DTO fields.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsNoticeListContracts|BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded: 62 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded after rerunning outside the parallel test/build lock.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-17 BidOps SGCC Procurement Attachment Preview

Completed:

- Added structured Excel extraction for BidOps attachments. `.xlsx` and `.xls` worksheets now produce Markdown tables with column positions preserved, including nested ZIP attachments and legacy SGCC `.xls` files.
- Added ZIP filename fallback decoding for older GB18030/GBK SGCC archives while keeping normal UTF-8 Chinese ZIP names intact.
- Extended the SGCC ECP procurement table parser to parse all procurement scope tables in source order, derive `包1/包2` from package names when needed, ignore continuation rows without package numbers, parse explicit `最高限价/预算金额` money values, and read in-table qualification/performance/personnel requirements.
- Added `tools/Atlas.LocalSetup inspect-bidops-sgcc-notices` for no-write public SGCC notice inspection. The command prints unescaped Chinese JSON with detail metadata, attachment trees, package counts, amount-reference counts, requirement counts, and package samples.
- Ran the diagnostic command against the three supplied procurement notices:
  - `2606028335014767` 四川: 1 nested ZIP attachment, 28 packages, 28 packages with explicit amount reference, 49 parsed requirements.
  - `2605288211018925` 重庆: 1 ZIP attachment with demand list and goods list, 27 packages, 0 explicit public amount references, 34 parsed requirements.
  - `2605268192348756` 河南: 1 ZIP attachment with goods list and DOCX notice, 22 packages, 0 explicit public amount references, 0 parsed requirements from the public package.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsTextExtractor|BidOpsEcpProcurementTableParser|BidOpsDeterministicNoticeParser" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 18 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- inspect-bidops-sgcc-notices --package-take 8` succeeded and returned Chinese JSON without Unicode escaping.

## 2026-06-17 BidOps Raw Notice Business Identity

Completed:

- Changed Raw notice ingestion dedupe from source-local URL only to business identity first: `NoticeType + SourceNoticeId`, where `SourceNoticeId` is now `code:<采购编号/项目编号>` when available and `url:<DetailUrlHash>` as fallback.
- Raw notice lookup now crosses `SourceId`, so the same public URL imported manually and crawled automatically returns/updates the same RawNotice instead of creating a second row under a different source.
- Added tenant unique index `TenantId + NoticeType + SourceNoticeId` through migration `v0.2.13-bidops-raw-notice-business-identity`. The migration preserves historical duplicates by marking non-canonical old rows with `:legacy:<Id>` rather than deleting data.
- Updated LocalSetup `seed-bidops-state-grid` and `repair-bidops-data-quality` to normalize existing Raw notice identities and create the same unique index in local databases.
- Tightened project-code extraction so `ProjectCode:` blank lines do not consume the next field as a code.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 79 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.

## 2026-06-18 BidOps Outcome Project Name Display

Completed:

- Added a distinct `项目名称` column to review task outcome/candidate detail tables so it is no longer conflated with `分标名称`.
- Added `项目名称` to the background job parsed result outcome table and added both `项目名称` and `分标名称` to the supplier analysis amount-ranked outcome list.
- Reused the existing `OutcomeSupplierRecordDto.ProjectName` backend field; no API or database schema change was required.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-18 BidOps Supplier Linked Outcomes

Completed:

- Added a paged `关联成交公告` section to supplier detail pages.
- The section loads `outcome-records` by `supplierId` and shows linked public outcome/candidate announcements with project, procurement code, lot, package, result type, rank, award amount, service fee, publish time, source notice link, and Raw notice detail link.
- Reused the existing supplier outcome record API; no backend or database schema change was required.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-18 BidOps Outcome Lot Number Guard

Completed:

- Tightened the DeepSeek outcome-supplier prompt so `lotNo` is only returned when the public source explicitly shows a `分标编号/标段编号/分标号/标段号` value.
- Tightened the DeepSeek outcome-supplier prompt so `projectName` is only returned when the public source explicitly shows a `项目名称/工程名称/采购项目名称/招标项目名称/子项目名称` value.
- Added backend persistence sanitization that clears AI-provided `lotNo` values when neither evidence text nor source text supports them with an explicit lot-number label or labeled table header.
- Added backend persistence sanitization that clears AI-provided `projectName` values when the value is only the公告标题/公告名称/采购批次名称/分标名称/包名称/附件文件名 rather than an explicit project-name field.
- Added backend correction for DeepSeek rows that place an explicit source `项目名称` column value into `packageName`; persistence now moves that value back to `projectName` and clears `packageName`.
- Fixed PDF line-wrap project-name support: when the source table has an explicit `项目名称` header and DeepSeek row evidence contains the reconstructed project value, persistence keeps `projectName` even if the raw extracted text split that value across lines.
- Tightened package-name fallback so matched package metadata cannot fill `packageName` with the same value as `projectName`; query preview also no longer uses `lotName` as a package-name fallback.
- Changed review-task outcome row display and edit defaults so `项目名称` no longer falls back to the notice/task announcement title.
- Removed outcome/candidate deterministic parser title fallbacks so result detail `项目名称` stays empty when the PDF/table does not expose a real project-name column or label.
- Changed outcome package matching to avoid using a repeated package number alone to pull in an unrelated package's lot number.
- Added a persistence quality gate for outcome supplier extracts so obvious PDF/table misalignment values such as units, bare numbers, header cells, buyer-side State Grid units, and explicit流标/废标 rows are not saved when deterministic fallback output is noisy.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplier" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 21 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 81 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded after adding fallback quality-gate tests with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 83 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded after adding project-name explicit-evidence tests with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 87 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService|BidOpsOutcomeSupplierAiExtractionService" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded after tightening project-name persistence and package-name correction with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 16 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService|BidOpsOutcomeSupplierAiExtractionService" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded after adding PDF line-wrap project-name coverage with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 17 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService|BidOpsOutcomeSupplierAiExtractionService" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded after package-name/project-name distinctness checks with `DEEPSEEK_API_KEY` temporarily cleared for the test process: 19 passed.
- `.\scripts\restart-webapi.ps1` and `.\scripts\restart-worker.ps1` restarted local BidOps services. Review outcome AI reparse job `325853052928135168` for task `325461581435637801` succeeded: 39 outcome rows, 39 non-empty `projectName`, 0 non-empty `packageName`, and 0 rows where `packageName == projectName`.

## 2026-06-18 BidOps Outcome Detail Empty Columns

Completed:

- Added frontend-only column visibility checks for review-task 中标/成交明细 and 候选人明细.
- Optional detail columns now hide automatically when every current row would display an empty placeholder, while core supplier and edit/action columns remain visible.
- The edit dialog still exposes all fields for manual correction; no API or database schema change was required.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-18 BidOps Supplier Analysis Amount Unit

Completed:

- Superseded by `2026-06-18 BidOps Outcome Amount Unit Semantics`: supplier analysis public outcome amount columns now display stored CNY-yuan values instead of assuming already-collected `万元` units.
- Renamed amount headers to `累计金额（元）` and `金额（元）`; `代理服务费` remains yuan-based and is labeled `代理服务费（元）`.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-22 BidOps Review Automation Completion

Completed:

- Implemented outcome/candidate review quality scoring for supplier name, package identity, amount-unit scaling, rate/discount contamination, candidate rank, and procurement package lifecycle matching.
- Connected outcome supplier extraction to review quality refresh, including the no-result path for outcome/candidate notices.
- Added low-risk bulk approval, batch DeepSeek reviewer-prompt reparse, and review-quality backfill API endpoints under `api/bidops/review-tasks`.
- Added `ReviewCorrectionSample` persistence with tenant-scoped indexes and migration `20260622100000_v0.2.16-bidops-review-automation-completion`.
- Captured correction samples from manual outcome edits/deletes, bulk approvals, and reviewer prompt reparse requests.
- Added correction-sample analysis and review-efficiency metrics query endpoints.
- Added `ReviewQualityBackfillJobHandler` for dry-run or write-mode historical quality refresh with source pause awareness.
- Added frontend review-list batch controls, quality backfill enqueue, and a review quality analysis page.
- Added `docs/BIDOPS/BIDOPS_REVIEW_AUTOMATION_RUNBOOK.md`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ReviewQuality|ReviewAutomation|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 10 passed.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 108 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-22 BidOps Scheduled Crawl Progress

Completed:

- Enabled local BidOps scheduled scanning in `src/Atlas.Worker/appsettings.BidOpsLocal.json` while keeping the default Worker config disabled for safe deployments.
- Added tenant tables and module entities for `bidops_crawl_checkpoint` and `bidops_crawl_run`.
- Added StateGrid ECP page-based crawl payloads and Worker execution support for incremental scans and historical backfill.
- Added per-channel schedule settings so each notice category can run by minute interval or at a daily `HH:mm` scan time.
- Added channel-level APIs to open/close scanning, start backfill, continue, pause, resume, and reset a checkpoint.
- Updated ScheduledScan so unfinished backfill checkpoints are continued automatically before normal incremental scans.
- Added operations read models for crawl progress, backfill counters, remaining estimate, cursor state, and failure alerts.
- Updated frontend crawl channel and health pages with per-channel open/close, interval/daily schedule editing, backfill start, continue, pause, resume, reset, and visible progress counters.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 108 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

Local restart note:

- `MigrationJob plan` against local `atlas_global_bidops` showed the historical local tenant database still has no EF migration history and would replay old migrations, so formal local `apply` was not run.
- Added `tools/Atlas.LocalSetup ensure-bidops-crawl-progress` for local/runtime repair of `bidops_crawl_checkpoint`, `bidops_crawl_run`, and `bidops_crawl_channel` schedule columns.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-crawl-progress --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"` succeeded.
- Restarted WebApi, Worker, and atlas-admin dev server. Worker `bidops.scheduled-scan` completed and enqueued channels `330101`, `330102`, `330103`, and `330104` for tenant `300001`.

## 2026-06-22 BidOps Codex CLI Provider

Completed:

- Added `IBidOpsCodexCliClient` and a `codex exec` runner for JSON-schema-constrained, non-interactive extraction.
- Added Codex CLI as the default AI provider for notice staging extraction and outcome/candidate supplier extraction while preserving the existing DeepSeek/OpenAI-compatible HTTP provider path.
- Added `BidOps:CodexCli` configuration for binary path, model, reasoning effort, working directory, timeout, sandbox, git check, user-config/rules handling, ephemeral sessions, and optional `CODEX_API_KEY`.
- Added tests proving Codex CLI provider routes do not call HTTP and preserve diagnostics.

Verification:

- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 110 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `ConvertFrom-Json` succeeded for the updated Worker/WebApi BidOps appsettings files.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `git diff --check` succeeded; Git reported only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

## 2026-06-22 BidOps Runtime AI Model Switch

Completed:

- Added tenant table/entity/configuration `bidops_runtime_setting` for runtime BidOps settings, with unique key `TenantId + SettingKey`.
- Added `IBidOpsAiSettingsService` and operations APIs to read/update the AI provider setting.
- Added operations dashboard controls for switching between DeepSeek and Codex CLI, with OpsManage permission required for mutation.
- Wired notice staging extraction and outcome/candidate supplier extraction to read the runtime provider before each AI call.
- Defaulted Codex CLI extraction to `gpt-5.5` and reasoning effort `xhigh`, while allowing appsettings overrides through `BidOps:CodexCli:Model` and `BidOps:CodexCli:ReasoningEffort`.
- Added `tools/Atlas.LocalSetup ensure-bidops-ai-settings` and ran it against local `atlas_bidops_runtime`.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 111 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /p:OutDir=D:\code\Personal\Atlas\artifacts\verify\webapi\ /nodeReuse:false /m:1` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /p:OutDir=D:\code\Personal\Atlas\artifacts\verify\worker\ /nodeReuse:false /m:1` succeeded.
- `npm run typecheck` and `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing chunk-size / Rollup comment warnings.
- `ConvertFrom-Json` succeeded for the updated Worker/WebApi BidOps appsettings files.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps` returned no matches.
- `dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-build -- ensure-bidops-ai-settings --tenant "Server=localhost;Port=3306;Database=atlas_bidops_runtime;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;"` succeeded.

Local restart note:

- Restarted WebApi, Worker, and atlas-admin dev server.
- Frontend probe returned `HTTP 200` at `http://localhost:5173/`.
- WebApi is listening on `http://localhost:5260`; unauthenticated `GET /api/bidops/operations/dashboard` returned `HTTP 401`, confirming the API is alive and auth is enforced.
- Worker started with `DOTNET_ENVIRONMENT=BidOpsLocal`; `bidops.scheduled-scan` completed and enqueued channels `330101`, `330102`, `330103`, and `330104` for tenant `300001`.

## 2026-06-22 BidOps Codex CLI Configurable Default

Completed:

- Changed the default BidOps AI provider from DeepSeek/OpenAI-compatible HTTP to Codex CLI when no runtime/config provider is set.
- Updated Worker and WebApi BidOps appsettings to default `BidOps:Ai:Provider` to `CodexCli`.
- Restored configurable Codex CLI model and reasoning effort through `BidOps:CodexCli:Model` and `BidOps:CodexCli:ReasoningEffort`.
- Kept Codex CLI defaults at `gpt-5.5` and `xhigh`; typo-like values such as `xhight` and `extrahight` are normalized to `xhigh`.
- Updated the operations dashboard to display the current Codex CLI model/reasoning config instead of saying the values are fixed.
- Updated AI provider docs and background job diagnostics wording from DeepSeek-specific to provider-neutral wording.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsStructuredExtractionService_UsesCodexCliProvider|BidOpsStructuredExtractionService_DefaultsToCodexCliModelAndReasoningEffort|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 112 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /p:OutDir=D:\code\Personal\Atlas\artifacts\verify\webapi\ /nodeReuse:false /m:1` succeeded with one transient file-lock retry warning.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /p:OutDir=D:\code\Personal\Atlas\artifacts\verify\worker\ /nodeReuse:false /m:1` succeeded.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing chunk-size / Rollup comment warnings.
- `ConvertFrom-Json` succeeded for the updated Worker/WebApi BidOps appsettings files.

Local restart note:

- Restarted WebApi, Worker, and atlas-admin dev server after changing the default provider.
- Frontend probe returned `HTTP 200` at `http://localhost:5173/`.
- WebApi is listening on `http://localhost:5260`; unauthenticated `GET /api/bidops/operations/dashboard` returned `HTTP 401`.
- Worker started with `DOTNET_ENVIRONMENT=BidOpsLocal`; `bidops.scheduled-scan` completed and enqueued channels `330101`, `330102`, `330103`, and `330104` for tenant `300001`.

## 2026-06-22 BidOps Outcome Package Identity

Completed:

- Changed outcome/candidate package matching to use the available parts of `LotNo + LotName + PackageNo`, so matching fields must agree and missing lot-number/name values are treated as incomplete evidence.
- Updated lifecycle conflict evidence and user-facing wording to make clear that `PackageNo + SupplierName` is not unique enough.
- Included lot name in outcome supplier merge grouping, package identity checks, package context lookup, manual-edit source hash, reverse lifecycle matching, and extraction source hash generation so same-supplier/same-package-number rows from different lot names are preserved.
- Added tests covering lot-name package matching and same-supplier rows across different lot names.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReviewQualityEvaluator|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 8 passed.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 115 passed.

## 2026-06-22 BidOps Codex CLI Reparse Launch Fix

Completed:

- Fixed Codex CLI startup on Windows by resolving `codex` to an executable wrapper such as `codex.cmd` before an extensionless npm shim, preventing `Process.Start` from failing with `Access is denied`.
- Fixed Codex CLI `--output-schema` temp file writing to use UTF-8 without BOM and validate the schema JSON before launching the CLI. This prevents Codex from rejecting the schema with `expected value at line 1 column 1`.
- Updated BidOps local WebApi/Worker settings to use `BidOps:CodexCli:BinaryPath=codex.cmd`.
- Added provider-neutral background job diagnostics under `aiResponses` while retaining `deepSeekResponses` as a compatibility alias.
- Changed the reviewer-prompt no-result message from a DeepSeek-specific string to `AI Provider 未返回可保存的中标/候选厂家线索，请查看后台任务的 AI 返回诊断。`.
- Recorded Codex CLI startup exceptions into AI diagnostics so the job detail page shows the provider, model, and exception text instead of only a zero-record result.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsCodexCliClient_ResolvesWindowsCmdShimBeforeExtensionlessFile|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierAiExtractionService_RecordsCodexCliFailureDiagnostics|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|StructuredParseJobHandler_ReturnsJsonResultSummary" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 5 passed.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsCodexCliClient_ResolvesWindowsCmdShimBeforeExtensionlessFile|BidOpsCodexCliClient_WritesOutputSchemaWithoutUtf8Bom|BidOpsOutcomeSupplierAiExtractionService_RecordsCodexCliFailureDiagnostics" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `$env:DEEPSEEK_API_KEY=''; dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModuleTests" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 118 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `npm run build` succeeded in `frontend/atlas-admin`; Vite reported only existing Rollup pure-comment and chunk-size warnings.
- `ConvertFrom-Json` succeeded for the updated Worker/WebApi BidOps local appsettings files.
- `cmd.exe /d /c ""C:\Users\Jason\AppData\Roaming\npm\codex.cmd" --version"` succeeded and returned `codex-cli 0.141.0`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

Local restart note:

- Restarted WebApi, Worker, and atlas-admin dev server.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/bidops/operations/dashboard`, confirming the API is alive and auth is enforced.
- Frontend probe returned `HTTP 200` at `http://localhost:5173/`.
- Worker started with `DOTNET_ENVIRONMENT=BidOpsLocal`; `bidops.scheduled-scan` and `bidops.recovery` completed on startup.

## 2026-06-22 BidOps Historical Outcome Quality Repair

Completed:

- Extended `tools/Atlas.LocalSetup repair-bidops-data-quality` with `--dry-run`, `--confirm`, and `--tenant-id` support.
- Added batch repair for outcome supplier rows whose evidence line starts with a clear分标编号 followed by包号, filling missing `LotNo` and defaulting empty `LotName` to `未分标段`.
- Added derived quality cleanup for stale outcome issue rows, now-satisfied `MissingLotOrPackage` issues, and lifecycle package-match issues that are now uniquely resolvable by `LotNo/LotName + PackageNo`.
- Added review task summary recalculation from remaining unresolved quality issues after cleanup.

Verification:

- `dotnet build tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded with 0 warnings and 0 errors.
- Dry-run against local `atlas_bidops_runtime` succeeded. Candidate counts: 37 outcome rows can fill missing `LotNo`, 24372 stale quality issues point to deleted outcome rows, 72 lifecycle package-match issues are now uniquely resolvable, 0 missing-lot/package issues are already satisfied, and 604 review task summaries would be recalculated.
- Executed confirmed repair against local `atlas_bidops_runtime`; it completed with `affectedRows=7279`. The post-repair dry-run showed 0 outcome rows missing evidence-derived `LotNo`, 0 stale outcome quality issues, 0 uniquely-resolvable lifecycle package-match issues, and 0 now-satisfied missing-lot/package issues.
- The local raw-notice business identity unique index was skipped during repair because the current local MySQL key length limit rejects the existing utf8mb4 composite key. This does not block the outcome quality repair; the local schema still needs a dedicated migration/index-width fix before that unique index can be applied.

## 2026-06-22 BidOps Supplier Number Collision Fix

Completed:

- Fixed `Duplicate entry '300001-SUP-20260622-640512' for key 'IX_bidops_supplier_TenantId_SupplierNo'` by replacing the old low-six-digit supplier/buyer number suffix with a base36 encoding of the full Snowflake ID.
- Updated both automatic public outcome organization sync and manual supplier creation to use the same business-number builder.
- Confirmed the local tenant database has no actual duplicate `SupplierNo` or `BuyerNo` rows; the failure was a new insert colliding with an existing historical six-digit number.
- Restarted local Worker and WebApi so new organization sync and manual supplier creation use the new numbering rule.
- Requeued 10 pending/running/failed `bidops.outcome.supplier-extract` jobs that had failed or been interrupted with the old duplicate `SupplierNo` error, and cleared their stale `LastError` text so new retries report fresh results.
- Observed Worker successfully create a new supplier after restart with full-ID business number format, e.g. `SUP-20260622-2HKHBKBJFJLS`, with no repeat of the duplicate six-digit `SupplierNo` error.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsBusinessNumberBuilder|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 2 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsSupplierNoFix\"` succeeded with 0 warnings and 0 errors.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/bidops/operations/dashboard`, confirming the API is alive and auth is enforced.

## 2026-06-23 Background Job Timeout And AI Progress Visibility

Completed:

- Investigated the apparently stuck BidOps background queue. The 8-hour `bidops.outcome.supplier-extract` job `327466358289862656` was stale and was manually marked `Dead` with a timeout watchdog result.
- Confirmed current Codex CLI work was not dead: Worker logs showed large structured-parse calls completing, including a 65k prompt taking about 344 seconds and a 91k prompt taking about 188 seconds.
- Added `BackgroundTasks:OneTimeJobs:MaxRunningSeconds` with a 7200-second default and local Worker config value.
- Updated `BackgroundJobWorker` to force-terminate already-running jobs older than the max runtime and to cancel active job execution when it exceeds the same max runtime.
- Added a generic `IBackgroundJobProgressReporter` that writes a short running heartbeat into `BackgroundJobs.Result` and refreshes `UpdatedAt` without sharing the executing handler's DbContext.
- Wired BidOps structured parsing and outcome supplier extraction handlers to report stages while Codex CLI is running.
- Capped deterministic/reference AI prompt JSON at 12,000 characters to prevent large fallback results from inflating Codex prompts far beyond the configured source-text budget.
- Updated Codex CLI cancellation handling so upstream Worker cancellation kills the full Codex process tree instead of relying only on the CLI's own timeout.
- Adjusted hard-timeout sweep logic to prefer `LockedAtUtc` over historical `StartedAtUtc`, so retried jobs are timed from the current execution attempt.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ProgressReporter_UpdatesRunningJobResult|Worker_ForceTerminatesRunningJobOlderThanMaxRunningTime|Worker_MarksActiveJobDeadWhenItExceedsMaxRunningTime|Worker_CancelsRunningJobWhenTerminationIsRequested" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 4 passed.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BackgroundJobProgress\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerProgress\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "ProgressReporter_UpdatesRunningJobResult|Worker_ForceTerminatesRunningJobOlderThanMaxRunningTime|Worker_MarksActiveJobDeadWhenItExceedsMaxRunningTime|BidOpsCodexCliClient_ResolvesWindowsCmdShimBeforeExtensionlessFile|BidOpsCodexCliClient_WritesOutputSchemaWithoutUtf8Bom" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "Worker_ForceTerminatesRunningJobOlderThanMaxRunningTime|Worker_MarksActiveJobDeadWhenItExceedsMaxRunningTime|ProgressReporter_UpdatesRunningJobResult" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsProgress\"` succeeded with one transient file-lock retry warning and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WorkerTimeoutProgressFinal\"` succeeded with 0 warnings and 0 errors.
- `ConvertFrom-Json` succeeded for the updated Worker BidOps appsettings files.

Local restart note:

- Stopped the old local Worker and the active Codex CLI child process, then marked the interrupted running BidOps job retryable with a restart message.
- Restarted Worker with `DOTNET_ENVIRONMENT=BidOpsLocal`.
- Confirmed new Worker process is running and current structured-parse job `327538620040876032` was reclaimed with `AttemptCount=2`.
- Confirmed the running job now writes visible progress to `BackgroundJobs.Result`, for example `{"message":"Codex CLI 正在结构化解析公告","stage":"notice-structured-parse",...,"elapsedSeconds":15}`.

## 2026-06-23 Outcome Supplier Codex Timeout Mitigation

Completed:

- Investigated `OutcomeSuppliers` Codex CLI failures ending with `TimeoutException: Codex CLI timed out after 600 seconds`.
- Changed outcome supplier AI prompt construction to treat Codex as a focused enrichment pass: deterministic reference results are capped at 40 records / 6,000 characters.
- Replaced raw source concatenation with source compaction around result evidence, prioritizing attachment text and retaining explicit `分标编号/包号/厂家/金额` context while trimming long irrelevant HTML/text.
- Kept reviewer-prompt calls on a larger source ceiling than ordinary automatic extraction, matching the runtime scenario policy where ordinary recognition can use `low` and manual prompts can use `xhigh`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_CompactsCodexSourceAroundOutcomeEvidence|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierAiExtractionService_RecordsCodexCliFailureDiagnostics|BidOpsOutcomeSupplierExtractionService_FillsLotNoFromOrdinalPrefixedOutcomeEvidence|BidOpsReviewQualityEvaluator_MatchesOutcomePackageByLotNoAndPackageNoWhenPackageNoRepeats" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 5 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

Local restart note:

- Restarted WebApi and Worker after building the force-cancel changes.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`, confirming the API is alive and auth is enforced.
- Worker started with process `15992`; current BidOps progress now shows provider-neutral `AI 正在结构化解析公告`.
- Confirmed local runtime AI provider remains `DeepSeek` in `bidops_runtime_setting`.
- `git diff --check` succeeded with only the existing line-ending normalization warning for `AtlasTenantDbContextModelSnapshot.cs`.

Local restart note:

- Stopped the old local WebApi/Worker process tree and the Worker-owned Codex CLI child process that was still running the old prompt.
- Restarted WebApi with the `bidops-local` launch profile and Worker with `DOTNET_ENVIRONMENT=BidOpsLocal`.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`, confirming the API is alive and auth is enforced.
- Worker started with process `19980`; startup logs showed a new structured Codex CLI request using `reasoningEffort=low` and `promptChars=5841`.

## 2026-06-23 Background Job Force Cancellation

Completed:

- Investigated a BidOps task that stayed in `Running` after an operator termination request. The job had `CancellationRequestedAt` set, but a restarted single Worker had picked up newer high-priority work before it could reclaim and mark the stale running job canceled.
- Added a Worker sweep that marks stale running jobs with existing termination requests as `Canceled` before claiming new work.
- Added `BackgroundJobCancelRequest.Force`. When `force=true`, a running job is immediately marked `Canceled`, its lock is cleared, retry scheduling is stopped, and the cancellation signal remains visible for the Worker.
- Added a Worker post-handler cancellation check so a late external AI response cannot overwrite an operator-forced cancellation as `Succeeded`.
- Added `强停` actions to the operations job list and detail pages. Normal `终止` remains cooperative; `强停` is the explicit immediate cancellation path.
- Changed BidOps progress heartbeat messages from `Codex CLI 正在...` to provider-neutral `AI 正在...`; the current runtime provider may be DeepSeek even when older progress text said Codex.
- Locally force-canceled the stuck BidOps running jobs `327643833896669184` and `327582120501448704`; both now have `Status=5` and no database lock.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "CancelAsync_RunningJobForceCancelsImmediately|CancelAsync_RunningJobRequestsCooperativeTermination|Worker_CancelsStaleRunningJobWithTerminationRequestBeforeNewWork|Worker_CancelsRunningJobWhenTerminationIsRequested|Worker_ForceTerminatesRunningJobOlderThanMaxRunningTime" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ForceCancelWorker\"` succeeded: 5 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "CancelAsync_RunningJobForceCancelsImmediately|ProgressReporter_UpdatesRunningJobResult|Worker_CancelsStaleRunningJobWithTerminationRequestBeforeNewWork" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ForceCancelWorker2\"` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.BackgroundTasks\Atlas.BackgroundTasks.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.

## 2026-06-25 BidOps Bulk Approval Timeout Mitigation

Completed:

- Investigated a bulk review timeout and confirmed the primary bottleneck was the WebApi approval path synchronously calling outcome supplier extraction, which can invoke the currently selected AI provider such as Mimo.
- Changed review approval so it commits the approved notice first, then enqueues a `bidops.outcome.supplier-extract` background job only when a result/candidate notice has no outcome supplier records yet.
- Added `ApprovalOutcomeExtract` review correction samples so the review detail background-job view can still show the post-approval extraction job without mixing it into reviewer-prompt reparse metrics.
- Added `Supplier.NameNormalized`, a non-unique `(TenantId, NameNormalized)` index, and a tenant migration to backfill existing supplier rows.
- Updated supplier creation/update and approved-notice organization sync to maintain and query `NameNormalized`, avoiding full tenant supplier materialization during bulk approval.
- Updated the local `atlas_bidops_runtime.bidops_supplier` table with the additive column, backfill, and index because this local database lacks EF migration history even though BidOps tables already exist.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsModule_RegistersServicesAndBackgroundHandlers|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|StructuredParseJobHandler_ReturnsJsonResultSummary" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsBulkApproveOptimizedTests\"` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsBulkApproveOptimizedRetry\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\WebApiBulkApproveOptimized\"` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Data.Tenant.Migrations\Atlas.Data.Tenant.Migrations.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\TenantMigrationsSupplierNormFinal\"` succeeded with 0 warnings and 0 errors.
- Local MySQL verification showed `bidops_supplier` has `NameNormalized`, index `IX_bidops_supplier_TenantId_NameNormalized`, and 24,551 of 24,562 rows backfilled with a non-empty normalized name.
- `EXPLAIN SELECT * FROM bidops_supplier WHERE TenantId=300001 AND NameNormalized IN (...)` uses `IX_bidops_supplier_TenantId_NameNormalized` with index condition.

Local restart note:

- Restarted WebApi and Worker from default `bin/Debug/net8.0` outputs after applying the local supplier-name index patch.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`, confirming the API is alive and auth is enforced.
- Worker started under `DOTNET_ENVIRONMENT=BidOpsLocal`; logs show active Mimo structured AI requests after restart, and runtime setting `ai.provider` remains `Mimo`.

## 2026-06-26 BidOps Runtime Mimo Token UI

Completed:

- Added operations API endpoints for saving and testing the tenant runtime Mimo token.
- Added runtime Mimo token metadata to the AI settings DTO without returning the raw token.
- Updated Mimo HTTP settings resolution so Worker prefers the saved runtime token before configuration or environment variables.
- Updated structured notice extraction and outcome supplier extraction to read the latest runtime Mimo token before each HTTP AI call.
- Added a Mimo token panel to the BidOps operations dashboard, visible when the effective AI provider is `Mimo`, with save and test actions.
- The Mimo token test action verifies token/model/endpoint connectivity without requiring WebApi AI extraction to be enabled; Worker execution still follows Worker-side AI settings.
- Registered BidOps crypto support for Worker hosts when `Security:Crypto:Key` is configured, and added local Worker development crypto configuration.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoTokenModule\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_UsesMimoProviderSettings|OperationsControllers_DeclareP0Routes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsMimoTokenTests\"` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- An initial WebApi build attempt failed because old local WebApi/Worker processes locked `obj` outputs; stopping those processes and rebuilding resolved it.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; Worker process started from `src/Atlas.Worker/bin/Debug/net8.0`; frontend probe returned `HTTP 200` at `http://localhost:5173`.

## 2026-06-26 BidOps Runtime DeepSeek Token UI

Completed:

- Added operations API endpoints for saving and testing the tenant runtime DeepSeek token.
- Added DeepSeek runtime token metadata to the AI settings DTO without returning the raw token.
- Updated DeepSeek HTTP settings resolution so Worker prefers the saved runtime token before configuration or environment variables.
- Updated structured notice extraction and outcome supplier extraction to read the latest provider-specific HTTP token before each DeepSeek or Mimo AI call.
- Added a DeepSeek token panel to the BidOps operations dashboard, visible when the effective AI provider is `DeepSeek`, with save and test actions.
- Kept token test behavior provider-neutral: it verifies token/model/endpoint connectivity without requiring WebApi AI extraction to be enabled; Worker execution still follows Worker-side AI settings.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsDeepSeekTokenModule\"` succeeded with 0 warnings and 0 errors.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsOutcomeSupplierAiExtractionService_UsesMimoProviderSettings|OperationsControllers_DeclareP0Routes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsDeepSeekTokenTests\"` succeeded: 4 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`.

## 2026-06-27 BidOps Lifecycle Retry Dead Jobs

Completed:

- Investigated retried jobs that immediately returned to `Dead`. Worker error logs showed `bidops.lifecycle.reverse-closure` failing with MySQL duplicate-key errors on `IX_bidops_lifecycle_link_Tenant_SourceHash`.
- Confirmed retry scheduling was not the root cause: the same lifecycle reverse-closure job regenerated duplicate link suggestions inside one persistence batch, then attempted to insert two rows with the same tenant-scoped `SourceHash`.
- Added lifecycle-link draft deduplication before persistence. Duplicate suggestions with the same persistence hash are collapsed to one candidate, preferring lower manual-review risk, higher confidence, and richer amount/candidate/tender evidence.
- Added a warning log when duplicate suggestions are collapsed so future retries can be diagnosed from Worker logs without dumping sensitive payloads.
- Added a regression test covering same-hash collapse while preserving distinct supplier/package outcomes.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DeduplicatesLifecycleLinkDraftsByPersistenceHash|BidOpsReverseLifecycleClosureService_DoesNotCrossLinkSamePackageAcrossLots|LifecyclePackageLinkConfiguration_UsesTenantScopedMatchIndex" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleRetryDeadFixTests\"` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker restarted as process `31828`.

## 2026-06-27 BidOps Lifecycle Closure Lot Name Display

Completed:

- Split the lifecycle closure center table from a combined `分标 / 包件` column into separate `分标编号`, `分标名称`, and `包件` columns so lot names are visible without relying on overflow tooltip text.
- Split the lifecycle link detail drawer into separate lot-number and lot-name fields.
- Added a lightweight display fallback from lifecycle evidence JSON (`lotName`, `award.lotName`, `matchedCandidate.lotName`, `tender.lotName`) when the persisted link row itself does not have `LotName`.
- Investigated `rawNoticeId=328339628681728000` versus review task `328547208658030783`. The review task had lot names in `bidops_outcome_supplier_record` and lot numbers/names in `bidops_package_staging`, while existing `bidops_lifecycle_package_link` rows had empty `LotNo/LotName`.
- Updated lifecycle reverse-closure generation to enrich award evidence from existing outcome supplier rows plus review package rows before building lifecycle links.
- Updated lifecycle link list/detail responses to display-enrich missing lot/package fields from the same outcome/package context, so historical link rows become readable without mutating stored audit data.
- Extended lifecycle link DTOs with procurement/candidate/award RawNotice references, original public attachments, and an explicit procurement-missing reason.
- Updated the lifecycle closure center list with a `采购公告` column and renamed the supplier column to `中标商家`.
- Updated the lifecycle detail drawer into a review workbench surface: it now shows procurement notice evidence, procurement attachments, award/result notice attachments, optional candidate-publicity attachments, winning supplier, lot name, package identity, and final award amount.
- Local investigation of project code `282602` found only the award-result RawNotice in `atlas_bidops_runtime`; no corresponding procurement-announcement RawNotice was present locally. The closure center now surfaces that as a missing procurement notice rather than a blank field.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_BuildsOutcomeAwardEvidenceWithReviewPackageLotContext|BidOpsReverseLifecycleClosureService_DeduplicatesLifecycleLinkDraftsByPersistenceHash|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleLotNameContextTests3\"` succeeded: 3 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker restarted as process `3600`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_BuildsOutcomeAwardEvidenceWithReviewPackageLotContext|BidOpsReverseLifecycleClosureService_DeduplicatesLifecycleLinkDraftsByPersistenceHash|LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\LifecycleEvidenceCenterTests\"` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin` after adding procurement/award attachment evidence to the lifecycle center.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after the lifecycle evidence DTO changes.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after the lifecycle evidence DTO changes.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker restarted as process `27636`.

## 2026-06-27 BidOps Review Sorting Defaults

Completed:

- Updated the background-job list so choosing status `成功/Succeeded` applies `CompletedAt` descending when no explicit sort is already selected.
- Kept completed-time table-header sorting manual and sticky, so operator-selected sort is not overwritten by the success-status convenience default.
- Extended the lifecycle closure center sort dropdown with lot number/name, package number, supplier, project code, amount, match score, review flag, status, confirmation time, update time, and creation time options.
- Extended lifecycle link backend sorting to accept the new `SortBy` values used by the closure center.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\ReviewSortingTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `git diff --check` succeeded with the existing EF snapshot line-ending warning.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker restarted as process `34260`.

## 2026-06-28 BidOps 22FK09 Lifecycle Lot Context Repair

Completed:

- Investigated project code `22FK09` in the local BidOps runtime database. The award notice text contains the expected table header `分标编号 分标名称 包号 包名称 成交供应商名称`, but the PDF text extraction splits values such as `22FK09-9012` + `008-T035` and supplier suffixes such as `有限` + `公司`.
- Confirmed existing outcome supplier rows for award RawNotice `327333277306327040` had `ProjectCode=22FK09）`, empty `LotNo`, and generic `LotName=未分标段`, while the procurement RawNotice `327854700416339968` and its package staging rows already contained `LotNo=22FK09-9012008-T035`, `LotName=科技项目-经研院科技科研`, and package names for packages 1-26.
- Checked background job diagnostics for the same RawNotice. The original structured parse job saved outcome rows with `deepSeekResponses=[]`; a later reviewer-prompt `bidops.outcome.supplier-extract` attempt used `CodexCli / gpt-5.5` and failed with status code 1. No local evidence showed Mimo as the source of the bad 22FK09 outcome rows.
- Added a deterministic wrapped State Grid award-table parser for header-driven PDF text with no row sequence number. It reconstructs split lot numbers, lot names, package numbers, package names, and trailing supplier names before creating outcome supplier extracts.
- Updated deterministic outcome extraction to drop less-specific duplicate rows when a more specific row for the same supplier/package/outcome has lot context.
- Normalized project codes during outcome persistence and lifecycle procurement matching so values such as `code:22FK09`, `22FK09）`, and `22FK09` match as the same procurement code.
- Updated lifecycle closure read models to infer procurement notice references before display enrichment, then use both award and procurement package staging rows for read-only lot/package display enrichment. Generic placeholders such as `未分标段` are treated as lower-priority than a unique procurement package match.
- Added a lifecycle closure page `解析/AI任务` shortcut from the top award/procurement notice context to the existing BidOps background-job list filtered by RawNoticeId, so operators can inspect provider/model diagnostics in the background-job detail page.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOps22FK09Build3\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractBuilder_ExtractsStateGridWrappedAwardTableWithoutSequence|BidOpsReverseLifecycleClosureService_EnrichesGenericAwardLotFromProcurementPackage|BidOpsLifecycleFieldEnrichmentAiService_SourceBundleKeepsNeighborRowsAroundRelevantRows" --no-build --no-restore --nologo --verbosity minimal -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureTests2\"` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsReverseClosureTests" --no-build --no-restore --nologo --verbosity minimal -p:OutDir="$env:TEMP\AtlasVerify\BidOpsReverseClosureTests2\"` succeeded: 44 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Outcome Supplier Reparse Button

Completed:

- Added a lifecycle-closure endpoint to enqueue `bidops.outcome.supplier-extract` for an award/result RawNotice from the closure center without using the RawNotice full reparse workflow that rejects approved notices.
- Kept the new action explicitly announcement-scoped: the outcome supplier extraction job re-extracts all supplier rows for the selected RawNotice and can refresh multiple lifecycle rows after the job completes.
- Added a top-context `重抽中标明细` button next to the award notice in `闭环任务与审核中心`, with a confirmation warning that this is not a row-level operation.
- Renamed the row action from `提示词` to `提示词补全` so row-level lifecycle field enrichment is visibly separate from announcement-level supplier re-extraction.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOutcomeReparseBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleDebugController_DeclaresReverseClosureRoutes|BidOpsOutcomeSupplierExtractBuilder_ExtractsStateGridWrappedAwardTableWithoutSequence|BidOpsReverseLifecycleClosureService_EnrichesGenericAwardLotFromProcurementPackage" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsOutcomeReparseTests\"` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Package Amount Display

Completed:

- Split the lifecycle closure table's package display into explicit `包号` and `包名称` columns so operators can verify package identity without parsing combined text.
- Added read-model fields for `ProcurementPackageAmount` and `ProcurementPackageAmountSource` without changing the persisted lifecycle-link table.
- Enriched lifecycle list/detail responses from procurement `ProcurementDetailStaging` rows, prioritizing exact `分标名称 + 包号` matches before falling back to reviewed package staging or persisted tender evidence.
- Kept procurement amount display conservative: when multiple procurement detail rows match a package, the UI shows an amount only when a supported amount field has one unique value across those rows.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsPackageAmountBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_EnrichesProcurementPackageAmountByLotNameAndPackageNo|BidOpsReverseLifecycleClosureService_EnrichesGenericAwardLotFromProcurementPackage|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsPackageAmountTests\"` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Procurement Amount Default

Completed:

- Updated lifecycle pricing inference so when no direct award amount or candidate final quote is available, a unique procurement package amount defaults the final award amount for review display.
- Added a dedicated amount kind `DefaultedFromProcurementPackageAmount` so defaulted procurement amounts are not confused with amounts directly disclosed by the award/result notice.
- Kept defaulted procurement amounts marked for manual review before formal supplier analytics.
- Updated lifecycle list/detail read enrichment so historical rows with missing final award amount also display the matched procurement package amount as the default final amount.
- Translated lifecycle detail `匹配理由` and `缺失字段` tags to Chinese in the closure center while keeping the raw evidence JSON unchanged for audit/debugging.

Verification:

- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsProcurementDefaultBuild\"` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReverseLifecycleClosureService_DefaultsAwardAmountFromProcurementAmountWhenAwardAmountMissing|BidOpsReverseLifecycleClosureService_EnrichesProcurementPackageAmountByLotNameAndPackageNo|BidOpsReverseLifecycleClosureService_ReportsAwardAmountMissing|BidOpsPricingInferenceService_DoesNotInferWhenBaseAmountIsAmbiguous" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsProcurementDefaultTests2\"` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Amount Source Chinese Display

Completed:

- Added lifecycle amount-source Chinese labels and options for internal values such as `DirectAwardAmount`, `CandidateFinalQuote`, and `DefaultedFromProcurementPackageAmount`.
- Updated the lifecycle confirmation dialog to show a Chinese amount-source selector while still submitting the stable internal enum value.
- Updated lifecycle detail and field-enrichment source display to format source enums in Chinese.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Batch Review

Completed:

- Added row selection to the lifecycle closure center table, with selectable rows limited to `Suggested` lifecycle links.
- Added a compact batch review toolbar with `批量确认` and `批量驳回` actions for the current selected rows.
- Kept batch review on the existing per-row confirmation/rejection endpoints so each lifecycle link is updated independently and already confirmed/rejected rows are not mutated by selection.
- Batch confirmation submits each row's currently displayed final award amount and internal amount-source value, while the UI continues to show Chinese amount-source labels.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Lifecycle Reparse Refresh

Completed:

- Investigated 22FK09 after outcome supplier re-analysis. The local outcome supplier table had new extracted rows, but `bidops_lifecycle_package_link` still contained the old 10 links; the page default `Suggested` status filter hid the one confirmed row, so only 9 rows were visible.
- Added a `RefreshLifecycleLinks` flag to `OutcomeSupplierExtractJobPayload`. Lifecycle-center outcome reparse jobs now run `ReverseCloseRawNoticeAndPersistAsync` after supplier extraction succeeds, so the closure link table is refreshed by the same background job.
- Updated the lifecycle closure reparse button label to `重抽并刷新闭环` and clarified the confirmation text.
- Changed the lifecycle closure center default status filter from `待确认` to `全部`, while keeping batch selection limited to `Suggested` rows.
- Added final outcome-supplier save pruning for wrapped award-table fragments, so 22FK09-style parses keep the complete package row and drop weaker duplicate fragments for the same package.
- Refresh persistence now removes stale non-confirmed lifecycle suggestions for the same award notice before writing new suggestions, while preserving equivalent manually confirmed package/supplier links.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PrunesWrappedAwardTableFragmentsWhenFullPackageRowsExist|OutcomeSupplierExtractJobHandler_RefreshesLifecycleLinksWhenRequested|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|BidOpsReverseLifecycleClosureService_TreatsConfirmedSamePackageSupplierAsEquivalentAcrossHashChanges|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsLifecycleReparseRefreshTests2\"` succeeded: 5 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Initial WebApi/Worker builds failed because the running local `Atlas.WebApi` and `Atlas.Worker` processes held DLL locks; after stopping those processes, WebApi built successfully, Worker briefly hit a parallel `obj` cache lock, and then a single Worker rebuild succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker with the new build. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`.

## 2026-06-28 BidOps Lifecycle Amount Unit And Dialog Close

Completed:

- Extended `BidOpsMoneyNormalizer` to accept amount-unit context from column headers or nearby table text. Unitless cells now multiply by 10,000 when the column/header context says `万元`, while explicit `元` headers take precedence.
- Updated award, candidate, and tender evidence table parsers to pass amount column headers and table context into money normalization.
- Added regression coverage for award table cells like `65.88` under `成交金额（万元）`, preventing lifecycle rows from showing tens of yuan when the public notice uses ten-thousand-yuan units.
- Updated the lifecycle confirmation dialog to use an explicit close handler, append the dialog to `body`, and allow cancel, close icon, ESC, and modal-click closing.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsMoneyNormalizer_UsesTenThousandYuanUnitContextForUnitlessCells|BidOpsAwardEvidenceParser_UsesAmountHeaderTenThousandYuanUnit|BidOpsOutcomeSupplierTextParser_ExtractsAwardAmountsFromOutcomeTableColumns|BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfAwardRows" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsAmountUnitDialogTests\"` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Worker built successfully. WebApi initially hit a transient Microsoft Defender lock on an `obj` file, then rebuilt successfully with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`.

## 2026-06-28 BidOps Manual Lifecycle Analysis Reruns

Completed:

- Investigated the "闭环分析任务已存在 but list is empty" case. The enqueue message comes from `BackgroundJobs` deduplication, while the closure center list reads `bidops_lifecycle_package_link`; those can diverge when a historical job failed, completed with no links, or used older extraction behavior.
- Updated manual lifecycle reverse-closure enqueue keys to include a per-run suffix, so an old background job no longer permanently blocks rerunning closure analysis for the same RawNotice.
- Kept global background task deduplication unchanged because the unique index is shared by many task types; the rerun behavior is scoped to BidOps manual lifecycle analysis.
- Added a closure-center empty state for RawNotice-filtered pages with no link rows, with actions to open the filtered background-job list and refresh the closure list.
- Adjusted lifecycle analysis enqueue messages to say the task was submitted or is in the queue rather than implying an old completed task is enough.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "LifecycleReverseClosureDeduplicationKey_IncludesRunIdForManualAnalysis|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsLifecycleManualRerunTests\"` succeeded: 2 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi now listens on `http://localhost:5260` and returned `HTTP 401` for `/api/auth/context`; frontend returned `HTTP 200` at `http://localhost:5173`.

## 2026-06-28 BidOps Procurement Notice Code Search

Completed:

- Updated State Grid public procurement notice lookup to query `index/noteList` with `purOrgCode=<项目编号>` before using the generic `key` keyword field. This matches the official search behavior needed for notices such as `22FK09`.
- Added project-code normalization before official search so values copied from lifecycle rows with prefixes or trailing punctuation still search as the clean procurement code.
- Kept `key` keyword lookup as a fallback when project-code lookup returns fewer candidates.
- Added regression coverage for the generated `noteList` request payload.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "StateGridEcpCrawler_BuildsNoticeListPayloadWithProjectCodeField|StateGridEcpWcmParser_ParsesNoticeListAndDetail|LifecycleDebugController_DeclaresReverseClosureRoutes" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsProcurementCodeSearchTests\"` succeeded: 3 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi now listens on `http://localhost:5260` and returned `HTTP 401` for `/api/auth/context`; frontend returned `HTTP 200` at `http://localhost:5173`.
- `git diff --check` succeeded.

## 2026-06-28 BidOps Formal Notice Default Filter

Completed:

- Updated `正式公告库` to default the notice-type filter to `中标/成交结果公告` (`AwardAnnouncement`) instead of showing all formal notices.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-06-28 BidOps Formal Notice Lifecycle Review Status

Completed:

- Added a lifecycle-review status to formal notice list/detail DTOs, derived from lifecycle package links for the notice's award RawNotice.
- Added formal notice search filtering by lifecycle review state, including a query-only `NotApproved` option for "未通过/未完成".
- Updated `正式公告库` with a `闭环审核` filter and table column.
- Added Chinese labels for the new lifecycle review statuses so product-facing UI does not show raw English enum values.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsQueryService_ResolvesNoticeLifecycleReviewStatus|BidOpsModule_RegistersServicesAndBackgroundHandlers" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsNoticeLifecycleStatusTests\"` succeeded: 2 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Initial parallel Worker build hit a transient `obj` cache file lock; rerunning `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi now listens on `http://localhost:5260` and returned `HTTP 401` for `/api/auth/context`; frontend returned `HTTP 200` at `http://localhost:5173`.
- `git diff --check` succeeded.

## 2026-06-28 BidOps Lifecycle Prompt Parse Placement

Completed:

- Removed row-level `AI补全` and `提示词补全` actions from the lifecycle closure table and detail drawer so lifecycle rows stay focused on review decisions.
- Added source-notice-level `AI提示词辅助解析` actions to the top `中标公告` and `采购公告` context areas.
- Award prompt parsing now submits the existing award RawNotice outcome-supplier reparse task with the reviewer prompt and refreshes the lifecycle list when the background job succeeds.
- Procurement prompt assistance now submits field-enrichment jobs for the procurement notice area's associated non-final lifecycle links and refreshes the lifecycle list after those jobs finish.
- Added background-job polling in the closure page for prompt/reparse tasks so completed AI work updates the visible lifecycle list automatically.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.

## 2026-07-01 BidOps Award Collection Auto Procurement Closure

Completed:

- Added lifecycle closure service support for auto-collecting the corresponding 前置公告 when an award/result notice is parsed. The flow reuses the closure page's State Grid project-code candidate search and only auto-imports when an exact candidate can be selected unambiguously.
- Wired `StructuredParseJobHandler` and `OutcomeSupplierExtractJobHandler` to refresh lifecycle links and then auto-collect/auto-review procurement notices for result notices.
- Updated outcome supplier extraction enqueue sites after review approval, review AI reparse, and supplier backfill to set `RefreshLifecycleLinks=true`.
- Added backend lifecycle batch review and award-level auto-review endpoints. Auto-review skips failed/status-only rows, rows without a linked procurement notice, low-score links, and rows whose final amount source looks like a service fee.
- Updated the lifecycle closure page to call the backend batch-review API and added an `自动补采集/审核` action for the current award notice.

Verification:

- `dotnet build Atlas.sln --no-restore` succeeded with 0 warnings and 0 errors after stopping the running WebApi/Worker that locked BidOps DLLs.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --filter "FullyQualifiedName~BidOpsReverseClosureTests"` succeeded: 78 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --no-build --filter "FullyQualifiedName~StructuredParseJobHandler|FullyQualifiedName~OutcomeSupplierExtractJobHandler"` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- Restarted WebApi with the `bidops-local` launch profile and Worker with `DOTNET_ENVIRONMENT=BidOpsLocal`.
- WebApi probe returned `HTTP 401` at `http://localhost:5260/api/auth/context`; frontend probe returned `HTTP 200` at `http://localhost:5173`; Worker PID `21440` started and completed BidOps scheduled scan/recovery startup tasks.

## 2026-07-01 BidOps Queue Health UTC Clock Fix

Completed:

- Investigated the operations page `超期未成功` status after restarting/continuing a BidOps crawl queue. Local Worker had completed channel `330104` backfill jobs successfully, and `bidops_crawl_channel.LastSuccessTime` had been refreshed in UTC.
- Fixed channel health calculation to compare `LastSuccessTime` with `DateTime.UtcNow` instead of local `DateTime.Now`, preventing freshly successful UTC crawl timestamps from being misread as hours older.
- Added regression coverage for `BidOpsOperationsQueryService.GetChannelHealthAsync` so a recently successful UTC crawl channel remains `Healthy`.

Verification:

- `dotnet build Atlas.sln --no-restore` succeeded with 0 warnings and 0 errors after stopping running WebApi/Worker processes that locked BidOps DLLs.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsOperationsQueryService_ChannelHealthUsesUtcClock"` succeeded: 1 passed.

## 2026-07-02 BidOps Review Pool Bulk Approve Timeout

Completed:

- Investigated review-pool bulk approval timeout: WebApi logs showed `/api/bidops/review-tasks/bulk-approve` eventually returned `200` after about 70 seconds, while the frontend Axios timeout is 30 seconds.
- Confirmed the current review-pool button was submitting all selected review task IDs in one request. The backend endpoint was a synchronous batch wrapper that still approved items one by one and each approved notice synchronously performed formal notice persistence plus organization master-data synchronization, so a 100-item request could exceed the browser request window.
- Added a real background bulk-approval path: `POST /api/bidops/review-tasks/bulk-approve/job` enqueues one `bidops.review.bulk-approve` job carrying the selected review task IDs. The Worker reuses the existing per-item approval rules and records the merged result in the background-job result payload.
- Updated `ReviewTaskListPage.vue` so selections larger than 10 create one background bulk-approval job, while smaller batches still use the synchronous endpoint for immediate feedback.
- Updated the background bulk-approval enqueue path to pre-reserve eligible pending review tasks as `InReview` before the Worker consumes the job. This removes queued items from the `待审核` filter immediately and prevents duplicate selection while the background job is running.

Verification:

- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsReviewService_EnqueueBulkApproveReservesEligiblePendingTasks|ReviewTasksController_DeclaresReviewAutomationContracts" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:OutDir="$env:TEMP\AtlasVerify\BidOpsBulkApproveReserveTests\"` succeeded: 2 passed.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after a transient lock from a .NET host process cleared.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi returned `HTTP 401` at `http://localhost:5260/api/auth/context`, frontend returned `HTTP 200` at `http://localhost:5173`, and Worker logs showed BidOps recurring tasks starting successfully.

## 2026-07-02 BidOps Review Detail Row Numbers And AI/Persistence Diagnosis

Completed:

- Added explicit `序号` columns to the review detail page's amount candidate, award/result supplier, candidate supplier, and procurement package tables so operators can reference problematic rows in large 100+ row notices.
- Investigated review task `328124621658394627`: the latest `bidops.outcome.supplier-extract` job returned 166 AI rows, all with empty `lotNo` but complete `lotName/packageNo/supplierName`. The persisted table contained 169 rows; the extra 3 rows were weaker duplicate fallback rows for the same supplier/package/evidence with empty `lotName/packageName`.
- Verified existing extraction merge tests already cover pruning weak wrapped-table fragments when full package rows exist, so newly re-run outcome extraction should not reintroduce the 3 weak duplicate rows.
- Fixed amount candidate cache invalidation for outcome reparse: candidates derived from deleted/replaced `OutcomeSupplierRecord` rows are now removed before the raw notice or lifecycle candidate pool is rebuilt.
- Filtered low-context `unknown` candidates produced by raw notice/attachment full-text scanning when they have no lot/package/supplier context, and stopped generating those text-scan noise candidates going forward.
- Updated the review detail amount candidate table to display 分标名称 and 包名 first, with 分标编号/包号 as fallbacks.
- Repaired local data for `RawNoticeId=327970754287243264`: removed 128 stale outcome-derived amount candidates, 3 weak duplicate outcome rows, and 61 no-context text-scan amount candidates. The local outcome detail now has 166 rows with 0 missing `LotName`; amount candidates now have 47 rows with 0 missing `LotName`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PrunesWrappedAwardTableFragmentsWhenFullPackageRowsExist|BidOpsOutcomeSupplierExtractionService_PrunesShortLotNameFragmentsWhenFullPackageRowsExist" --no-restore --logger "console;verbosity=minimal"` succeeded: 2 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsAmountCandidateService_DetectsStaleOutcomeRecordCandidate|BidOpsAmountCandidateService_FiltersFailedOutcomeSupplierCandidatesForDisplay|BidOpsAmountCandidateService_FiltersLowContextUnknownTextNoise|BidOpsOutcomeSupplierExtractionService_PrunesWrappedAwardTableFragmentsWhenFullPackageRowsExist" --no-restore --logger "console;verbosity=minimal"` succeeded: 4 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Restarted local WebApi and Worker. WebApi returned `HTTP 401` at `http://localhost:5260/api/auth/context`, frontend returned `HTTP 200` at `http://localhost:5173`, and Worker process is running.

## 2026-07-02 BidOps CI Wrapped Outcome Parser Fix

Completed:

- Fixed outcome notice kind detection so an explicit `CandidateAnnouncement` notice type or candidate-publicity title is not overridden by the State Grid `doci-win` detail URL.
- Prevented wrapped outcome table parsing from using the announcement title as `ProjectName` when the body has no explicit `项目名称/采购项目名称` label.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfCandidateRows|BidOpsWrappedOutcomeTableParser_DoesNotUseTitleAsProjectNameWithoutExplicitProjectName" --no-restore --logger "console;verbosity=minimal"` succeeded: 2 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOps" --no-restore --logger "console;verbosity=minimal"` succeeded: 233 passed.

## 2026-07-04 BidOps PDF Table Structure Extraction

Completed:

- Investigated local pending review data for executable validation samples. The tenant database currently has 1851 pending review tasks with `ReviewRecommendation=NeedsReview` and 655 pending tasks with `ReviewRecommendation=NeedsReparse`.
- Selected `RawNoticeId=327873693386674176` / review task `328034536208338949` as the primary regression sample because its 18FV2F award PDFs show the exact structure-loss symptom: visual table columns such as 分标编号、分标名称、包号、成交人 are split into plain text line fragments.
- Enhanced `BidOpsTextExtractor` PDF extraction to append a layout-aware `PDF 表格结构 Page N` Markdown section built from PdfPig word coordinates. The extractor now detects table headers, assigns following words into header-derived columns, keeps single-digit sequence/package values, and merges wrapped cells such as `18FV2F900300` + `1-14-05`.
- Kept the MVP storage contract unchanged: the structured Markdown is embedded into the existing extracted text so current attachment processing and AI prompt input paths can consume it without a new database column.
- Added regression tests covering plain PDF extraction, simple PDF table extraction, and wrapped PDF table cell merging.

Verification:

- `mysql -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT Status, ReviewRecommendation, COUNT(*) AS Count FROM bidops_review_task WHERE Status IN (0,1) AND ReviewRecommendation IN (1,2) GROUP BY Status, ReviewRecommendation;"` confirmed pending review/reparse validation samples exist.
- `mysql -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT Id, RawNoticeId, FileName, FileType, StorageKey, TextContentStorageKey, TextExtractStatus FROM bidops_raw_attachment WHERE RawNoticeId IN (327873693386674176,331402358141620224,331402348666687488,331341155914616832) ORDER BY RawNoticeId, Id LIMIT 30;"` confirmed PDF attachments and extracted text are available for the selected samples.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings and 0 errors.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsTextExtractor_ExtractsPdfTextWithoutRawPdfObjects|BidOpsTextExtractor_ExtractsPdfTableStructureAsMarkdown|BidOpsTextExtractor_MergesWrappedPdfTableCellsByColumn" --no-restore --nologo --verbosity minimal` succeeded: 3 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsTextExtractor" --no-restore --nologo --verbosity minimal` succeeded: 10 passed.

## 2026-07-04 BidOps Outcome Reparse Row Explosion Fix

Completed:

- Investigated review task `328034536208338949` after manual reparse. The latest `bidops.ai.structured-parse` job `331662123849617408` saved 458 outcome supplier records for `RawNoticeId=327873693386674176`, while the previous focused outcome reparse jobs saved 301 records.
- Confirmed the reparse did remove old outcome rows before inserting new rows; the 458 rows were not old+new duplicates. The new rows came from the latest extraction run itself.
- Identified two sources of row inflation: result-announcement AI rows were persisted as `Candidate` instead of being normalized to `Awarded`, and appended `PDF 表格结构` Markdown sections were consumed by the outcome supplier extraction path as if they were original evidence, producing Markdown/long cross-section evidence rows.
- Kept PDF structured text available for review diagnostics, but changed outcome supplier extraction to strip appended `PDF 表格结构` Markdown before deterministic parsing and before sending attachment text to the outcome-supplier AI prompt.
- Added persistence guards to reject obvious polluted evidence rows that start with Markdown pipes, contain `PDF 表格结构`, or have long evidence that crossed into service-notice/instruction text.
- Normalized non-failed rows from final result announcements to `Awarded`, so AI returning `Candidate` for a 成交/中标结果公告 no longer creates a second outcome set.
- Repaired local data for `RawNoticeId=327873693386674176`: removed 144 polluted outcome rows, 127 derived amount candidates, and 144 derived review quality issues. The outcome table now has 314 rows (`Awarded=307`, `Failed=7`, `Candidate=0`) and no Markdown evidence rows. Amount candidates for the notice are down to 33, and the review task risk is now Medium with 312 active medium issues.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_DropsPdfMarkdownOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_NormalizesFinalAwardCandidateRowsToAwarded|BidOpsOutcomeSupplierExtractionService_StripsPdfMarkdownSectionsFromOutcomeSource|BidOpsOutcomeSupplierExtractionService_EnrichesFragmentedLotNoFromUniqueOutcomeContext|BidOpsOutcomeSupplierExtractionService_FillsSingleHyphenLotNoFromInlineOutcomeEvidence|BidOpsOutcomeSupplierExtractionService_KeepsInlineLotNoEvidenceWithLotName" --no-restore --nologo --verbosity minimal` succeeded: 6 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsOutcomeSupplierExtractionService|FullyQualifiedName~BidOpsOutcomeSupplierExtractBuilder|FullyQualifiedName~BidOpsTextExtractor" --no-restore --nologo --verbosity minimal` succeeded: 47 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 0 warnings and 0 errors.

## 2026-07-04 BidOps PDF Table Outcome Merge

Completed:

- Added a dedicated PDF Markdown-table outcome parser for the `PDF 表格结构 Page N` sections appended by `BidOpsTextExtractor`. It reads table rows as structured evidence instead of treating Markdown pipes as free text or sending them back to AI.
- Merged the dedicated PDF table extracts with the existing deterministic and AI outcome extracts, while still stripping diagnostic Markdown sections from the old text parser and AI prompt input to prevent duplicate row inflation.
- Added recovery heuristics for State Grid award tables where PDF text extraction wraps or drops boundaries: split `LotNo` prefixes embedded in `LotName`, reconstruct wrapped lot numbers such as `18FV2F900300 1-14-05`, accept long unseparated lot numbers such as `18FV2F9011005` only with package evidence, and fill missing lot names from row evidence.
- Added same-notice enrichment for rows where the PDF breaks a supplier name before the company suffix, limited to cases where a unique complete supplier name exists in the same extraction result and the row evidence supports the truncated suffix.
- Repaired local historical data for `RawNoticeId=327873693386674176` / review task `328034536208338949`: the three attached PDFs are represented in the merged outcome detail, stale weak duplicates were removed, missing lot context was backfilled from attachment text/evidence, and two truncated supplier names were expanded.

Verification:

- `mysql --default-character-set=utf8mb4 -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT COUNT(*) AS AttachmentCount FROM bidops_raw_attachment WHERE RawNoticeId=327873693386674176;"` returned `3`.
- `mysql --default-character-set=utf8mb4 -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT COUNT(*) AS TotalRows, SUM(LotNo IS NULL OR LotNo='') AS EmptyLotNo, SUM(LotName IS NULL OR LotName='') AS EmptyLotName, SUM(PackageNo IS NULL OR PackageNo='') AS EmptyPackageNo, SUM(EvidenceText LIKE '%PDF 表格结构%' OR EvidenceText LIKE '|%') AS MarkdownEvidence FROM bidops_outcome_supplier_record WHERE RawNoticeId=327873693386674176;"` returned `TotalRows=310`, `EmptyLotNo=0`, `EmptyLotName=0`, `EmptyPackageNo=0`, and `MarkdownEvidence=0`.
- `mysql --default-character-set=utf8mb4 -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT OutcomeType, COUNT(*) AS Count FROM bidops_outcome_supplier_record WHERE RawNoticeId=327873693386674176 GROUP BY OutcomeType;"` returned `Awarded=303` and `Failed=7`.
- `mysql --default-character-set=utf8mb4 -h localhost -P 3306 -u root -proot atlas_bidops_runtime -e "SELECT COUNT(*) AS DuplicateGroups FROM (SELECT LotNo,LotName,PackageNo,SupplierName,COUNT(*) c FROM bidops_outcome_supplier_record WHERE RawNoticeId=327873693386674176 GROUP BY LotNo,LotName,PackageNo,SupplierName HAVING c>1) d;"` returned `0`.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "FullyQualifiedName~BidOpsOutcomeSupplierExtractionService|FullyQualifiedName~BidOpsOutcomeSupplierExtractBuilder|FullyQualifiedName~BidOpsTextExtractor|BidOpsPdfTableOutcomeParser" --no-restore --nologo --verbosity minimal` succeeded: 54 passed.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 82 existing test-project warnings and 0 errors.

## 2026-07-09 BidOps Local Startup Recovery

Completed:

- Restored `BidOpsOutcomeSupplierExtractBuilder.cs` after the file was found to contain only NUL bytes, preserving the current outcome source diagnostics, raw field, field-evidence, and warning fields.
- Fixed the restored outcome supplier extraction job handler namespace reference so lifecycle refresh diagnostics compile again.
- Updated the local BidOps restart scripts so the combined startup stops backend processes, prebuilds WebApi/Worker, shuts down .NET build servers, and starts both backend services with `--no-build` to avoid shared `bin`/`obj` locks.
- Changed `scripts/restart-bidops-local.ps1` so Worker is opt-in with `-WithWorker`; the default UI startup now keeps only WebApi and Vite running.
- Reduced `BidOpsLocal` Worker startup pressure by disabling scheduled scan/recovery `RunOnStartup`, lowering recovery batch size, and limiting local AI job concurrency.

Verification:

- BidOps module C# NUL-byte scan returned no corrupted files.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfAwardRows|BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfCandidateRows|BidOpsOutcomeSupplierExtracts_CarrySourceDiagnostics|BidOpsOutcomeSupplierAiExtractionService_ParsesOutcomeSupplierSchemaV2Diagnostics" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|OutcomeSupplierRebuildDryRunJobHandler_ReturnsJsonResultSummaryWithoutApplying" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 2 passed.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\"` succeeded with 82 existing warnings and 0 errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -StartupWaitSeconds 8` succeeded locally; WebApi, Worker, and Vite were running afterward. `http://localhost:5173/` returned 200, `http://localhost:5260/api/auth/context` returned expected unauthenticated 401, and the proxied review-task API returned 401 instead of the previous proxy/backend 500.
- After stopping Worker/AI child processes, `http://localhost:5173/bidops/review/tasks/328034536208338949` returned 200 and WebApi auth returned expected unauthenticated 401 with no BidOps Worker or AI CLI child processes running.

## 2026-07-04 BidOps Lot Context Prefix Split Follow-up

Completed:

- Generalized outcome cleanup for rows where `LotName` starts with a complete State Grid lot number, including lot numbers whose final segment is one digit such as `18FV2F9011002-01-1`.
- When the current `LotNo` conflicts with the lot number embedded at the start of `LotName`, persistence now prefers the embedded lot number only if row evidence contains that embedded number and package context. This fixes context bleed without blindly rewriting ambiguous rows.
- Repaired local historical rows for `RawNoticeId=327873693386674176`: removed one weak duplicate failed row, split three joined `LotName` values, updated the review task issue count from 312 to 311, and corrected one supplier row to the existing full supplier master `中国电建集团江西省电力设计院有限公司`.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PrefersLotNameEmbeddedLotNoWhenCurrentContextConflicts|BidOpsOutcomeSupplierExtractionService_SplitsLotNoPrefixFromLotName|FullyQualifiedName~BidOpsOutcomeSupplierExtractionService|BidOpsPdfTableOutcomeParser" --no-restore --nologo --verbosity minimal` succeeded: 40 passed.
- Local query for `RawNoticeId=327873693386674176` returned `TotalRows=309`, `JoinedLotNameRows=0`, `EmptyLotNo=0`, and `EmptyLotName=0`.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false` succeeded with 82 existing test-project warnings and 0 errors.
- `git diff --check` succeeded.

## 2026-07-07 BidOps Local Service Restart Script

Completed:

- Added `scripts/restart-bidops-local.ps1` as a one-command local restart wrapper for BidOps WebApi, Worker, and Atlas Admin Vite frontend.
- The wrapper reuses the existing `restart-webapi.ps1` and `restart-worker.ps1` scripts, starts the frontend from `frontend/atlas-admin`, writes logs to the existing local log files, and prints health-check results for `http://localhost:5173` and `http://localhost:5260/api/auth/context`.

Verification:

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restart-bidops-local.ps1 -StartupWaitSeconds 5` succeeded locally.

## 2026-07-09 BidOps Outcome Supplier Pipeline Phase 2 Diagnostics

Completed:

- Added diagnostic scoring to outcome-supplier extraction candidates: completeness, evidence support, source trust, total strength score, and `Strong`/`Weak`/`Unsupported` classification.
- Added `StrengthCounts` to outcome extraction and rebuild dry-run result DTOs, and included source, lot-number validation, and strength distributions in the background job result JSON.
- Changed merge behavior for covered fallback candidates so a deterministic/weak fallback fills only missing survivor fields such as buyer, lot, package, amount, and evidence, without replacing the stronger survivor or creating a duplicate row.
- Updated the operations background-job detail page to show an outcome-supplier result summary above the raw result JSON, including dry-run state, existing/preview/saved counts, source distribution, lot-number validation distribution, and strength distribution.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PreservesPipeDelimitedLotNoEvidence|BidOpsOutcomeSupplierExtractionService_ScoresWeakLegacyOutcomeRows|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|OutcomeSupplierRebuildDryRunJobHandler_ReturnsJsonResultSummaryWithoutApplying|BidOpsOutcomeSupplierExtractionService_MergesMissingFieldsFromCoveredFallback|BidOpsOutcomeSupplierExtractionService_AutomaticMergeDropsLegacyWeakRowCoveredByAi|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames|BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsDeterministicRowsMissingFromAi" --no-restore --nologo --verbosity minimal` succeeded: 8 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` failed only because the running local `Atlas.WebApi` and `Atlas.Worker` processes locked their normal `bin` output DLLs.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\"` succeeded with 82 existing warnings and 0 errors.
- `git diff --check` succeeded; it emitted the existing line-ending warning for `src/Atlas.Modules.BidOps/BidOpsConstants.cs`.

## 2026-07-09 BidOps OutcomeSuppliers Schema V2 Intake

Completed:

- Upgraded the OutcomeSuppliers AI JSON schema and prompt to request `sourceSequenceNo`, `sourcePageNo`, `sourceTableTitle`, `sourceRowText`, raw field values, `fieldEvidence`, and `warnings`.
- Extended `BidOpsOutcomeSupplierExtract` to carry the new source-row, raw-field, field-evidence, and warning diagnostics while keeping the persisted `OutcomeSupplierRecord` schema unchanged in this phase.
- Updated AI response parsing to remain compatible with the older schema and to use `sourceRowText` as an evidence fallback when `evidenceText` is empty.
- Preserved the new diagnostic fields through non-award normalization, persistence sanitization, and covered-fallback field merge.
- Updated evidence scoring so `sourceRowText`, `fieldEvidence`, source page, and source sequence information improve diagnostic strength scores.
- Extended the background-job detail AI summary with source-row, field-evidence, and warning-row completeness metrics.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierAiExtractionService_ParsesOutcomeSupplierSchemaV2Diagnostics|BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords|BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider|BidOpsOutcomeSupplierExtracts_CarrySourceDiagnostics" --no-restore --nologo --verbosity minimal` succeeded: 4 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_MergesMissingFieldsFromCoveredFallback|BidOpsOutcomeSupplierExtractionService_PreservesPipeDelimitedLotNoEvidence|BidOpsOutcomeSupplierExtractionService_ScoresWeakLegacyOutcomeRows|BidOpsOutcomeSupplierExtractionService_AutomaticMergeDropsLegacyWeakRowCoveredByAi|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames" --no-restore --nologo --verbosity minimal` succeeded: 5 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\"` succeeded with 82 existing warnings and 0 errors.
- `git diff --check` succeeded; it emitted the existing line-ending warning for `src/Atlas.Modules.BidOps/BidOpsConstants.cs`.

## 2026-07-09 BidOps Outcome Pairwise Merge And Survivor Selection

Completed:

- Replaced the outcome-supplier AI/fallback merge path with an in-memory pairwise merge stage: candidates are scored, grouped, and reduced to one survivor per compatible business row.
- Added pairwise scoring signals for supplier compatibility, package number, source sequence/page, normalized evidence row, lot number compatibility, lot name compatibility, amount, and outcome type.
- Added hard conflict guards so different suppliers, package numbers, outcome types, ranks, or incompatible lot names do not merge in automatic mode.
- Kept source identity stable by selecting survivor primarily by source trust, then strength score and completeness. This preserves the previous behavior where AI remains the primary survivor and deterministic/PDF rows only fill missing fields.
- Preserved reviewer-prompt correction semantics: reviewer-prompt runs may merge rows where AI changed the supplier name, but only when package and lot context remain compatible.
- Added merge observability fields to extraction and dry-run results: `CandidateCount`, `MergeGroupCount`, and `MergedCandidateCount`, and surfaced them in background-job result JSON and the operations job detail page.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_PairwiseMergeUsesSourceSequenceForSurvivorFields|BidOpsOutcomeSupplierExtractionService_AutomaticMergePrioritizesAiAnnouncementOrder|BidOpsOutcomeSupplierExtractionService_AutomaticMergeDropsLegacyWeakRowCoveredByAi|BidOpsOutcomeSupplierExtractionService_MergesMissingFieldsFromCoveredFallback|BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|OutcomeSupplierRebuildDryRunJobHandler_ReturnsJsonResultSummaryWithoutApplying" --no-restore --nologo --verbosity minimal` succeeded: 7 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsAiCorrectionForSamePackage|BidOpsOutcomeSupplierExtractionService_ReviewerPromptKeepsDeterministicRowsMissingFromAi|FullyQualifiedName~BidOpsOutcomeSupplierExtractionService|BidOpsOutcomeSupplierAiExtractionService_ParsesOutcomeSupplierSchemaV2Diagnostics|BidOpsOutcomeSupplierExtracts_CarrySourceDiagnostics|BidOpsPdfTableOutcomeParser" --no-restore --nologo --verbosity minimal` succeeded: 46 passed.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\"` succeeded with 82 existing warnings and 0 errors.
- `git diff --check` succeeded; it emitted the existing line-ending warning for `src/Atlas.Modules.BidOps/BidOpsConstants.cs`.

## 2026-07-10 BidOps Review Reparse Outcome Count Fix

Completed:

- Diagnosed review task `328191424086544450`: the top review-page reparse runs the full raw-notice/attachment structured parse path, while the lower outcome prompt reparse runs only `OutcomeSupplierExtractJob` with reviewer prompt text.
- Confirmed both recent jobs had empty AI outcome responses and the observed 258/77 row counts came from deterministic parser and merge behavior, not from valid model extraction.
- Hardened PDF structured-table outcome parsing so shifted Markdown rows without reliable lot-number context are dropped instead of becoming persisted 中标明细.
- Added State Grid sequence/code wrapped-row parsing for result-announcement tables where row number, 分标编号, suffix, 分标名称, 包号, and 成交人 are split across multiple text lines.
- Fixed wrapped-text result parsing to stop at the next row once a row is complete and to normalize `流标`/failed rows as `Failed`.
- Limited reviewer-prompt relaxed merge thresholds to runs that actually have AI extract candidates, preventing empty-AI prompt reparses from collapsing different deterministic suppliers under the same package number.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsPdfTableOutcomeParser|BidOpsWrappedOutcomeTableParser_ExtractsStateGridSequenceSplitAwardRows|BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfAwardRows|BidOpsOutcomeSupplierExtractionService_ReviewerPrompt" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 11 passed.
- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOps" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false` succeeded: 263 passed.
- `dotnet build Atlas.sln --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false -p:BaseOutputPath="$env:TEMP\AtlasCodexBuild\"` succeeded with 82 existing warnings and 0 errors.
- `git diff --check` succeeded; it emitted the existing line-ending warning for `src/Atlas.Modules.BidOps/BidOpsConstants.cs`.
