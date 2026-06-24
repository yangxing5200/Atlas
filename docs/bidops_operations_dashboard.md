# BidOps Operations Dashboard

Date: 2026-06-12

## Pages

- `/bidops/operations` - BidOps operations dashboard.
- `/bidops/operations/jobs` - BidOps-only background job list.
- `/bidops/operations/channels` - crawl channel health.
- `/bidops/operations/worker-heartbeats` - BidOps worker heartbeat view, default filtered to `bidops` queue.
- `/ops/workers` - system worker node list.

## APIs

- `GET /api/bidops/operations/dashboard`
- `GET /api/bidops/operations/jobs`
- `GET /api/bidops/operations/config-check`
- `GET /api/bidops/operations/ai-settings`
- `PUT /api/bidops/operations/ai-settings/provider`
- `PUT /api/bidops/operations/ai-settings/codex-cli`
- `PUT /api/bidops/operations/ai-settings/codex-cli/scenario`
- `PUT /api/bidops/operations/runtime/task-pause`
- `GET /api/bidops/operations/channels/health`
- `GET /api/bidops/operations/crawl-progress`
- `POST /api/bidops/operations/jobs/{id}/retry`
- `POST /api/bidops/operations/jobs/{id}/cancel`
- `GET /api/ops/workers`

The read APIs currently reuse `bidops.crawl.read` or `bidops.ops.read`.
Retry/cancel reuse `bidops.crawl.manage`; AI provider updates, Codex CLI runtime
settings, and the global task pause switch require `bidops.ops.manage`.

## Config Checks

The dashboard reports warnings or errors for:

- one-time background job worker disabled;
- `BackgroundTasks:OneTimeJobs:Queues` missing `bidops`;
- recurring task runner disabled;
- `BidOps:ScheduledScan:TenantIds` missing;
- `BidOps:Recovery:TenantIds` missing;
- no enabled crawl sources;
- no enabled crawl channels;
- enabled sources marked `NeedLogin`.
- BidOps global task pause enabled.

## Task Pause Switch

The operations dashboard exposes a tenant-level `任务总开关` above the AI provider
panel.

- `bidops_runtime_setting` stores the runtime key `runtime.task-pause`.
- When paused, new operator-triggered BidOps job requests are rejected before
  enqueue.
- Recurring BidOps tasks skip the paused tenant instead of creating new jobs.
- Pending BidOps jobs are deferred before handler execution and retry attempts
  are not consumed while the pause is active.
- Running jobs are not force-killed; use the job detail termination action for
  cooperative cancellation.

## Channel Health

Health status is computed from source/channel flags, scan timestamps, last error,
and a bounded snapshot of recent BidOps jobs.

Statuses:

- `Healthy`
- `Due`
- `Stale`
- `Failed`
- `Disabled`
- `SourceDisabled`
- `SkippedNeedLogin`
- `NeverSucceeded`

The page also shows pending/running job counts and 24-hour success/failure counts
where a job payload contains the channel ID.

## Crawl Progress

Crawler progress is tracked separately from raw run logs:

- `bidops_crawl_checkpoint` stores one cursor per tenant/channel/mode.
- `bidops_crawl_run` stores each Worker execution segment.
- `Incremental` scans return to page 1 after each run.
- `Backfill` scans advance `NextCursor` and can be continued, paused, resumed, or reset.
- Channel scheduling can be set per notice category as either minute interval or
  daily `HH:mm`. Daily times are evaluated by the Worker server's local clock.

Channel enablement is the MVP switch for notice-category scanning. To scan only
成交公告 or only采购公告, keep the corresponding `CrawlChannel.Enabled=true`
and turn the other channels off.

## AI Provider

The operations dashboard shows the effective BidOps AI provider and exposes a
runtime switch between DeepSeek and Codex CLI. When Codex CLI is selected, the
same panel can update the Codex model and reasoning effort used by Worker
extraction jobs.

- `bidops_runtime_setting` stores the tenant-level runtime key `ai.provider`.
- `bidops_runtime_setting` stores ordinary Codex CLI runtime keys
  `ai.codex-cli.model` and `ai.codex-cli.reasoning-effort`.
- `bidops_runtime_setting` stores complex-source Codex CLI runtime keys
  `ai.codex-cli.complex.model` and
  `ai.codex-cli.complex.reasoning-effort`.
- `bidops_runtime_setting` stores manual reparse Codex CLI runtime keys
  `ai.codex-cli.manual-reparse.model` and
  `ai.codex-cli.manual-reparse.reasoning-effort`.
- `bidops_runtime_setting` stores reviewer-prompt Codex CLI runtime keys
  `ai.codex-cli.reviewer-prompt.model` and
  `ai.codex-cli.reviewer-prompt.reasoning-effort`.
- `CodexCli` is the default provider and uses local `codex exec`.
- Codex CLI defaults to model `gpt-5.5` with reasoning effort `low`; override
  them with `BidOps:CodexCli:Model` and `BidOps:CodexCli:ReasoningEffort`.
- Complex-source and manual reparse extraction default to reasoning effort
  `medium`; reviewer-prompt extraction defaults to reasoning effort `xhigh`.
- Worker chooses the Codex CLI scenario before each extraction job, so ordinary
  extraction, complex-source extraction, manual reparse, and reviewer-prompt
  extraction can use different model/effort values without a Worker restart.
- Complex-source extraction is selected when the Worker sees at least 3
  attachments or at least 60,000 characters of notice plus attachment text.
- Runtime Codex CLI changes take effect on the next Worker extraction job without
  a Worker restart.
- `DeepSeek` uses the configured OpenAI-compatible HTTP settings when selected.
- Switching the provider does not move secrets into the database; API keys,
  endpoints, and Codex CLI paths remain in appsettings or environment variables.

## Worker Heartbeat

The Worker heartbeat page reads `GET /api/ops/workers?queue=bidops`. It should
show at least one online Worker with `bidops` in its queue list before crawler,
attachment processing, structured parse, and recovery jobs are expected to drain.

## RawNotice Pipeline Troubleshooting

P0 has a dedicated `GET /api/bidops/operations/raw-notices/{id}/pipeline`
endpoint. To investigate a RawNotice:

1. Open the Raw notice detail page.
2. Check attachment download/text extraction status.
3. Open `/bidops/operations/jobs` and filter by the RawNoticeId or ChannelId.
4. Inspect failed `AttachmentProcess` or `StructuredParse` jobs from the job
   detail page.
5. Retry failed/dead jobs only after confirming handlers are idempotent.
