# Tenant Runtime Boundaries

Atlas separates ordinary tenant business code from tenant runtime infrastructure.

## Ordinary Business Code

Business services live outside `Runtime` namespaces and must use repository, query service, unit of work, and domain-event abstractions. They must not depend on:

1. `AtlasTenantDbContext`
2. `ITenantDbContextFactory`
3. `DbContext.Set<T>()`
4. EF raw SQL APIs such as `FromSql*` and `ExecuteSql*`

Example: `OrderCommandService` depends on `IRepository<Order>`, `IUnitOfWork`, and `ITenantDomainEventOutbox`; it does not open a tenant DbContext directly.

## Approved Runtime Code

Runtime implementations are isolated under `src/Atlas.Services.Tenant/Runtime`.

Approved runtime areas:

1. `Runtime/Messaging`: tenant outbox dispatcher, inbox idempotency runtime, and outbox writer implementation.
2. `Runtime/BackgroundJobs`: tenant background job handlers and recurring maintenance tasks.
3. `Runtime/Provisioning`: tenant provisioning flows that must initialize tenant data immediately after global tenant creation.

These types may use tenant DbContext or `DbContext.Set<T>()` only when the code explicitly constrains the operation by `TenantId`. Tenant raw SQL must go through `ITenantSqlExecutor`; callers choose a named infrastructure operation such as claiming an outbox message or deleting processed tenant messages, and the executor owns the final SQL text.

`ITenantSqlExecutor` intentionally does not expose a generic "execute this SQL string" method. New tenant SQL operations must add a narrow method that:

1. accepts `tenantId` explicitly;
2. rejects `tenantId <= 0`;
3. keeps `TenantId` in the final update/delete predicate;
4. has a unit test for missing tenant, wrong tenant, and normal execution.

## Dependency Direction

Business code may depend on abstractions such as:

1. `ITenantDomainEventOutbox`
2. `ITenantProvisioningService`
3. repository and unit-of-work interfaces

DI composition binds those abstractions to runtime implementations. This keeps ordinary services testable and lets the Analyzer enforce stricter rules against non-runtime assemblies and namespaces.
