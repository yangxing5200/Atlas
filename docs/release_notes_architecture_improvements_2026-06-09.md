# Architecture Improvement Release Notes

Date: 2026-06-09

## Breaking Changes

- `ICacheService` no longer exposes synchronous cache APIs; callers must use async methods such as `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` and `GetOrSetAsync`.
- `ITokenCacheService` no longer exposes synchronous token version/session cache APIs; security cache checks must use async methods.
- Tenant runtime infrastructure moved from `Atlas.Services.Tenant` to the new `Atlas.Services.Tenant.Runtime` assembly.

## Migration Notes

- Replace direct synchronous `ICacheService.Get/Set/Remove/Exists/GetOrSet` usage with async equivalents.
- Use `ValidateTokenAsync` for authentication/security paths that must honor session blacklist and token version cache. The synchronous `ValidateToken` path performs local token parsing only.
- Runtime workers, consumers and migration hosts should reference `Atlas.Services.Tenant.Runtime` when they need tenant provisioning, schema migration or tenant message runtime services.
- Business services should keep using `IUserService` during the transition; internally it now delegates to `IUserAuthService`, `IUserManagementService`, `IUserAssignmentService` and `IUserSessionService`.
- Data abstractions should depend on `IQueryBuilder<TEntity>` only; EF-specific implementation remains in `Atlas.Data.EntityFramework`.
- Dependency governance keeps runtime `Microsoft.Extensions.*` packages on 8.x and allows only documented Abstractions-only exceptions required by AutoMapper 16.1.1.

## Upgrade Path

1. Restore and build with the centralized package versions in `Directory.Packages.props`.
2. Run `tools/check-architecture-governance.ps1`.
3. Run focused test suites for Data, Core, Services and Analyzers.
4. Pack release artifacts with `dotnet pack Atlas.sln --no-build`.
