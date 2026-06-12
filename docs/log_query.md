# Log Query

Date: 2026-06-12

## Current P0 Behavior

P0 does not add an `OperationLogEntry` table or a Serilog database sink. The
available operational query surface is:

- job list: `/ops/jobs` or `GET /api/ops/background-jobs`;
- job detail: `/ops/jobs/{id}` or `GET /api/ops/background-jobs/{id}`;
- job fields: `LastError`, `Result`, status, attempts, lock time, and masked
  payload.

This is enough to answer common P0 questions such as:

- which jobs are pending/running/failed/dead;
- why a job failed;
- whether a job is waiting for retry;
- whether a job lock is stale;
- whether a BidOps queue is being consumed.

## Query By JobId

Use:

```http
GET /api/ops/background-jobs/{id}
```

The response contains masked payload, last error, result, retry count, and lock
metadata. The frontend page is `/ops/jobs/{id}`.

## Masking

Payload and text fields returned by the job APIs are masked for token, password,
cookie, authorization, phone, email, ID card, and bank card style keys. Unmasked
payloads are not exposed.

## Retention

P0 retention follows the existing `BackgroundJobs` table retention policy because
no separate operation-log table exists yet.

## P1 Plan

Add `OperationLogEntry` in the global database and write structured warnings and
errors from:

- `BackgroundJobWorker`;
- `RecurringTaskRunner`;
- BidOps crawl handlers;
- attachment processing;
- structured parsing;
- review task generation;
- recovery requeue operations.

Planned APIs:

- `GET /api/ops/logs`
- filter by `JobId`, `TenantId`, `Level`, `Module`, `SourceContext`,
  `CorrelationId`, and time range;
- default time range of 1 hour;
- maximum query range of 7 days;
- export gated by a dedicated logs export permission.
