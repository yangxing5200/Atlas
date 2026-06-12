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
- `GET /api/bidops/operations/channels/health`
- `POST /api/bidops/operations/jobs/{id}/retry`
- `POST /api/bidops/operations/jobs/{id}/cancel`
- `GET /api/ops/workers`

The read APIs currently reuse `bidops.crawl.read`; retry/cancel reuse
`bidops.crawl.manage`.

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
