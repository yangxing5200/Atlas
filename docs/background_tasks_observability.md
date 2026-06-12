# Background Tasks Observability

Date: 2026-06-12

## Scope

P0 observability is implemented on top of the existing global `BackgroundJobs`
table plus a lightweight global `BackgroundWorkerHeartbeat` table. It adds
tenant-scoped job query/basic management APIs and worker heartbeat visibility.

Implemented APIs:

- `GET /api/ops/background-jobs`
- `GET /api/ops/background-jobs/summary`
- `GET /api/ops/background-jobs/{id}`
- `POST /api/ops/background-jobs/{id}/retry`
- `POST /api/ops/background-jobs/{id}/cancel`
- `GET /api/ops/workers`

Frontend pages:

- `/ops/jobs`
- `/ops/jobs/:id`
- `/ops/workers`
- `/bidops/operations/worker-heartbeats`

## Enable Workers

WebApi can enqueue and query jobs, but long-running work must be consumed by
Worker nodes. Enable one-time jobs on the Worker host:

```json
{
  "BackgroundTasks": {
    "OneTimeJobs": {
      "Enabled": true,
      "Queues": [ "default", "tenant", "export", "bidops" ]
    }
  }
}
```

Recurring tasks are controlled separately:

```json
{
  "BackgroundTasks": {
    "Recurring": {
      "Enabled": true
    }
  }
}
```

BidOps uses queue `bidops`. If the Worker queue list does not include `bidops`,
BidOps crawl, attachment processing, structured parse, and recovery jobs will
remain pending.

## Worker Heartbeat

Worker hosts upsert `BackgroundWorkerHeartbeat` every 15 seconds when one-time
jobs or recurring tasks are enabled. The heartbeat records worker id, host,
process id, runtime mode, configured queues, enabled worker capabilities, current
job when one is running, start time, and last seen time.

The heartbeat writer is best-effort: a missing table or transient database error
is logged with throttling and does not stop the Worker. This keeps older local or
staging databases from failing before the global migration has been applied.

The query API computes online status from `LastSeenAtUtc` using a threshold of
`max(60 seconds, PollIntervalSeconds * 3)`.

## Tenant Boundary

The query service lives in `Atlas.BackgroundTasks`, where access to the global
`BackgroundJobs` table is allowed. It applies current `TenantId` filtering before
returning jobs. BidOps module code consumes this service instead of injecting
`AtlasGlobalDbContext`.

P0 `/api/ops/background-jobs` and `/api/ops/workers` use existing BidOps crawl
permissions so the current local BidOps admin account can access the page. A
dedicated system operations permission catalog remains a P1 hardening item.

## Payload Masking

All job payloads shown by the APIs are masked. The masker replaces sensitive
keys with `***`, including:

```text
password, pwd, secret, token, accessToken, refreshToken, apiKey, authorization,
cookie, phone, mobile, email, idCard, bankCard
```

There is no API that returns the unmasked payload.

## Retry

Retry creates a new `BackgroundJob` row. It does not mutate the original job
history. The new job inherits:

- `JobType`
- `Queue`
- `TenantId`
- `StoreId`
- `Payload`
- priority and max-attempt settings

The deduplication key receives a `manual-retry` suffix.

## Cancel

P0 cancel supports `Pending`, `Failed`, and `Dead` jobs. These are marked
`Canceled` and receive `CompletedAtUtc`.

`Running` jobs are not force-killed. The API returns a 400 response explaining
that the current framework does not have a safe interruption mechanism.

## P1 Deferred Tables

The following tables are not added in P0:

- `BackgroundJobEvent`
- `RecurringTaskRun`
- `OperationLogEntry`

They should be added through the global migration pipeline in a follow-up phase.
