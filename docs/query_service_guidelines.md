# QueryService Guidelines

QueryService is the standard entry point for complex read models. It exists so business developers do not fall back to `AtlasTenantDbContext`, `DbContext.Set<T>()`, or raw SQL when a query grows beyond simple CRUD.

## Naming

1. Put query contracts in `Atlas.Services.Abstractions.Queries`.
2. Name contracts as `I{Feature}QueryService`.
3. Put implementations in `Atlas.Services.Queries`.
4. Name implementations as `{Feature}QueryService`.
5. QueryService methods return DTOs, read models, or `PagedResult<T>`.

## Data Access Rules

1. QueryService uses `IRepository<TEntity>.QueryAsync()` or a dedicated approved repository/query reader.
2. QueryService does not inject `AtlasTenantDbContext`, `ITenantDbContextFactory`, EF `DbContext`, or `ITenantSqlExecutor`.
3. QueryService does not call `SaveChanges`, start transactions, or mutate entities.
4. Store-scoped filters are added after Repository creates the scoped query. Repository creation applies `AtlasTenantDbContext.ScopedSet(...)`, so tenant, store, share-group, and soft-delete rules remain central.
5. If a query cannot be expressed with Repository/QueryBuilder, add a named infrastructure reader with explicit tenant/store scope and analyzer coverage.

## Current Example

`IProductQueryService` and `ProductQueryService` implement a product search read model with keyword, price, store, customization, sort, and paging. The implementation uses `IRepository<Product>.QueryAsync()` and projects directly to `ProductDto`; it does not reference `AtlasTenantDbContext`.
