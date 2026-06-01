# Tenant Boundary PR Review Checklist

Use this checklist for every module PR that touches tenant data, store-scoped data, background jobs, consumers, or raw SQL.

## Hard Blocks

Reject the PR if ordinary business, API, QueryService, or consumer code contains:

1. direct `AtlasTenantDbContext`, `ITenantDbContextFactory`, or EF `DbContext` dependencies;
2. `DbContext.Set<T>()`;
3. `FromSql*`, `ExecuteSql*`, or a new generic raw SQL executor;
4. `IgnoreQueryFilters`;
5. request DTOs that accept caller-controlled `TenantId`.

These blocks should map to Analyzer diagnostics where possible: `ATL001`, `ATL002`, and `ATL003`. Items not covered by Analyzer, such as request shape and `IgnoreQueryFilters`, are template/review gates.

## Data Category Checks

Global data:

1. Tenant-owned Global records include `TenantId`.
2. Queries, deduplication keys, and background task claims include `TenantId` when the record belongs to a tenant.
3. Cross-tenant operations are named as infrastructure/admin flows and are not reachable from ordinary tenant APIs.

Tenant data:

1. Entity implements `ITenantEntity`.
2. Reads use Repository, QueryService, or an approved infrastructure reader.
3. Writes use Repository or domain services so `TenantId` is filled or checked before save.

Shared data:

1. Entity implements `ISharedEntity`.
2. Reads preserve current store/share-group visibility.
3. Any explicit `StoreId` filter narrows the scoped query instead of replacing it.

StoreOnly data:

1. Entity implements `IStoreOnlyEntity`.
2. Reads and writes stay within the current store unless a named cross-store QueryService or background reader is approved.
3. API parameters cannot choose another store without server-side authorization.

Infrastructure tenant data:

1. Outbox/inbox and similar runtime entities are accessed only through runtime store/services.
2. Claim, update, delete, and cleanup operations contain `TenantId` in the final predicate.
3. Raw SQL operations use named `ITenantSqlExecutor` methods with explicit `tenantId`.

## Template Acceptance

Generated or copied module code must satisfy:

1. Service depends on Repository, QueryService, UnitOfWork, and domain abstractions only.
2. QueryService depends on Repository/QueryBuilder or approved readers only.
3. Controller depends on service abstractions only and does not accept `TenantId` from request bodies.
4. Tests include at least one Analyzer or source check when a new template is introduced.
