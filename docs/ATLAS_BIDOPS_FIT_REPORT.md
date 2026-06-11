# Atlas BidOps Fit Report

Date: 2026-06-11

## Atlas Architecture Overview

Atlas is a .NET 8 modular multi-tenant framework with a Global DB for platform/control data and Tenant DBs for tenant business data. WebApi uses `AddAtlasWebApi`, Worker uses `AddAtlasWorker`, and tenant schema upgrades are handled by `Atlas.MigrationJob` instead of WebApi/Worker startup.

Reusable foundations for BidOps:

- Authentication, authorization, RBAC permissions, menu catalog, and tenant authorization context.
- Tenant data isolation through `ITenantEntity`, `IRepository<T>`, `IUnitOfWork`, `QueryService`, `DataScope`, and the tenant boundary analyzer.
- Tenant migrations through `src/Atlas.Data.Tenant.Migrations` and `Atlas.MigrationJob`.
- Global persistent background jobs through `IBackgroundJobClient` and `IBackgroundJobHandler`.
- Structured logging, cache, Snowflake IDs, auditing, and health checks.

## Fit Gaps And Additions

Atlas already supports business modules for services, controllers, authorization, consumers, and AutoMapper assemblies. Before BidOps entities were added, it did not expose a module-owned EF `IEntityTypeConfiguration<T>` assembly list to `AtlasTenantDbContext`.

Implemented Phase 0.5 addition:

- `IAtlasModule.EntityConfigurationAssemblies`
- Runtime provider `IAtlasTenantEntityConfigurationAssemblyProvider`
- Tenant DbContext scanning of module configuration assemblies
- Tenant migration service creation path uses the same assembly list
- Design-time migration factory can load module EF assemblies from `ATLAS_TENANT_ENTITY_CONFIGURATION_ASSEMBLIES` or `Atlas:TenantEntityConfigurationAssemblies`

## Recommended BidOps Directory

BidOps is implemented as:

```text
src/Atlas.Modules.BidOps
├── Ai
├── BackgroundJobs
├── Controllers
├── Crawling
├── Documents
├── Entities
├── EntityConfigurations
├── Models
├── Queries
└── Services
```

## Database Ownership

MVP physical storage is Atlas Tenant DB. BidOps owns the module entities and EF configurations, but tenant migrations remain centralized in `Atlas.Data.Tenant.Migrations`.

Rules:

- All BidOps tables use `bidops_` prefix.
- All business entities implement Atlas tenant isolation through `ITenantEntity`.
- Unique indexes include `TenantId`.
- No `BidOpsDbContext`, `bidops_db`, or separate migration pipeline is introduced.

## Worker Boundary

WebApi exposes configuration, enqueue, query, and review endpoints. Crawling, raw ingestion, mock AI parsing, and review task generation are implemented as Atlas BackgroundJobs and run in Worker.

Not placed in WebApi request threads:

- Public page crawling
- Attachment download
- Text extraction
- AI/rule parsing
- Dedup/change detection
- Result backfill

## File Storage

BidOps stores file metadata, storage provider, storage key, hash, and previews in MySQL. HTML snapshots, attachment binaries, and large extracted text are stored through `IBidOpsFileStore`. MVP uses `LocalBidOpsFileStore`; MinIO/S3-compatible storage remains a later adapter.

## First Implementation Plan

1. Add the generic module EF configuration scanning hook.
2. Add `Atlas.Modules.BidOps` using Atlas module conventions.
3. Register BidOps in WebApi, Worker, and MigrationJob.
4. Add Raw, Staging, Review, and Formal tenant entities with `bidops_` tables.
5. Implement manual URL/mock crawler enqueue paths and background handlers.
6. Implement mock AI/rule parsing into Staging and ReviewTask.
7. Implement human approval from Staging into Formal Notice/Package/Requirement.
