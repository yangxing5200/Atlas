# Implementation Log

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
- Browser smoke opened `http://localhost:5173/ops/jobs`, confirmed the list and first job detail loaded without console errors. The inspected historical job did not show `DeepSeek 返回` because it predates persisted `deepSeekResponses`, which is expected.

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
- Kept money storage as CNY yuan while the review edit form accepts the main final quote/award amount in 万元 to match the candidate/award review tables.
- Updated background job result serialization and job detail rendering so Chinese prompt/result text is displayed as Chinese instead of unicode escape sequences, including a fallback decoder for older non-JSON job text.

Verification:

- `dotnet test tests\Atlas.Services.Tests\Atlas.Services.Tests.csproj --filter "BidOpsOutcomeSupplierExtractionService_ReviewerPromptUsesAiRowsAsReplacement|OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary|ReviewTasksController_DeclaresOutcomeAiReparseContract" --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1` succeeded: 3 passed.
- `dotnet build src\Atlas.Modules.BidOps\Atlas.Modules.BidOps.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\BidOps\"` succeeded; the first parallel run hit a transient local `obj` file lock warning.
- `dotnet build src\Atlas.WebApi\Atlas.WebApi.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\WebApi\"` succeeded.
- `dotnet build src\Atlas.Worker\Atlas.Worker.csproj --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:OutDir="$env:TEMP\AtlasVerify\Worker\"` succeeded after rerunning outside a parallel local `obj` file lock.
- `npm run typecheck` succeeded in `frontend/atlas-admin`.
- `rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" src\Atlas.Modules.BidOps -n` returned no matches.
- Browser smoke confirmed approved review tasks hide manual edit actions, pending review tasks show `新增明细` and `编辑`, and the manual detail dialog exposes amount-in-万元, agency service fee, and evidence fields.
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
- Updated the review detail page to show candidate notices as a flat business list with `采购编号`, `分标编号`, `分标名称`, `包号`, `包名称`, `排名`, `推荐的成交候选人`, `最终报价（万元）`, and `评审情况`.
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
- Browser smoke opened a local candidate review task and confirmed the DeepSeek adjustment panel plus candidate columns including `推荐的成交候选人`, `最终报价（万元）`, and `评审情况`.
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
