# AGENTS.md — Atlas BidOps Project Instructions

## Role

You are the primary coding agent for integrating BidOps 招投标自动采集与人工审核系统 into the existing Atlas repository. Work as a senior .NET modular-monolith engineer and implementation planner.

## Read first

Before coding, read:

1. `CODEX_ATLAS_ADAPTATION_PROMPT.md`
2. `BIDOPS_CODEX_EXECUTION_SPEC.md`
3. `docs/BIDOPS/BIDOPS_ATLAS_DATABASE_INTEGRATION_NOTES.md`
4. `docs/DECISIONS.md` if it exists
5. `docs/IMPLEMENTATION_LOG.md` if it exists
6. Existing Atlas README, solution files, module docs, project files, migrations, Worker/WebApi startup code

## Working agreement

- Do not stop for clarification unless blocked by security, credentials, destructive actions, paid services, production data, or legal/compliance risks.
- When uncertain, make a conservative implementation choice, document it in `docs/DECISIONS.md`, and continue.
- Keep code, identifiers, classes, tables, and APIs in English.
- Keep product-facing Chinese labels where useful in UI, seed data, permissions, or menu labels.
- Prefer simple, maintainable MVP implementation over over-engineered abstractions.
- Do not implement bypasses for login, CAPTCHA, anti-bot controls, or non-public data access.
- Do not add features that help bribery, kickbacks, collusion, evasion, hidden fund flows, or non-public influence.
- Implement crawler throttling, retry limits, audit logs, kill switches, and source-level pause controls.
- AI extraction results must go to staging tables first and require human review before business import.
- One tender package can have at most one active pursuit in MVP.

## Atlas integration rules

- Use Atlas as the base framework. Do not create a new solution.
- Do not rewrite Atlas authentication, authorization, tenant isolation, logging, messaging, background task, repository, or migration infrastructure.
- Keep BidOps as an Atlas business module, preferably `src/Atlas.Modules.BidOps`, following Atlas module conventions.
- Keep the target framework and package policy aligned with Atlas. Do not upgrade Atlas to .NET 10 or any new framework in this task.
- WebApi must expose APIs, enqueue jobs, and query status only. Crawling, document extraction, AI parsing, deduplication, change detection, and long-running work must run through Atlas.Worker / Atlas background job mechanisms.
- Do not inject `AtlasTenantDbContext`, `ITenantDbContextFactory`, EF `DbContext`, or call `DbContext.Set<T>()` in BidOps business/API code. Use Atlas repositories, QueryServices, domain services, and existing data-scope conventions.
- Do not store file binary content in MySQL. Store only metadata and use `IBidOpsFileStore` with local storage for MVP and MinIO/S3-compatible storage later.

## BidOps data ownership rules

- BidOps owns its entities, entity configurations, services, queries, controllers, background jobs, AI services, crawler adapters, and compliance services.
- MVP physical storage is Atlas Tenant DB, not a separate `bidops_db`.
- Do not create a standalone `BidOpsDbContext` or a separate migration pipeline in MVP.
- All BidOps tables must use the `bidops_` prefix.
- All tenant business entities must use Atlas's current tenant isolation interface and `TenantId` rules.
- Unique indexes and deduplication keys must be tenant-scoped.
- Migrations must be generated/executed through Atlas tenant migration infrastructure.
- If Atlas does not yet support scanning module assemblies for EF `IEntityTypeConfiguration<T>`, implement a reusable module configuration scanning extension before adding BidOps tables.

## Definition of done

For each completed phase:

- Code builds, or failure is documented with exact reason.
- Relevant tests are added or updated.
- Run available tests and record results.
- Update `docs/IMPLEMENTATION_LOG.md`.
- Update `docs/DECISIONS.md` for assumptions or tradeoffs.
- Update README or docs when behavior changes.
- Do not proceed silently past a broken migration or tenant isolation issue; record it clearly and fix if possible.

## Commands

Prefer these commands when applicable:

```bash
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build
docker compose config
docker compose up -d
```

If a command is unavailable due to missing SDK, package manager, Docker, database, or network, document it and continue with feasible work.
