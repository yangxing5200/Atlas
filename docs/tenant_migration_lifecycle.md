# Tenant Schema Migration Lifecycle

Atlas upgrades tenant databases through an explicit MigrationJob instead of WebApi or Worker startup.

## Status Table

Global database table `TenantSchemaMigrationStates` records one row per tenant:

| Field | Purpose |
| --- | --- |
| `TenantId` | Tenant identifier. Unique. |
| `CurrentVersion` | Last applied tenant EF migration. |
| `TargetVersion` | Target migration for the current run. |
| `Status` | `Pending`, `Running`, `Succeeded`, `Failed`, or `Skipped`. |
| `LastError` | Truncated failure detail for operations. |
| `RetryCount` | Number of failed attempts. |
| `UpdatedAtUtc` | Last status update timestamp. |

## MigrationJob

Plan:

```powershell
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- plan
```

Apply:

```powershell
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- apply --batch-size 100
```

Dry-run through the apply path:

```powershell
dotnet run --project src\Atlas.MigrationJob\Atlas.MigrationJob.csproj -- apply --dry-run
```

## Runtime Behavior

- Scans active/trial tenants in batches.
- Reads tenant pending migrations before applying.
- Updates Global migration status before and after each tenant.
- A failed tenant records `Failed`, increments `RetryCount`, stores `LastError`, and does not stop following tenants.
- Cancellation tokens are checked between tenants and before database operations.

Production release flow:

1. Deploy and run MigrationJob `plan`.
2. Review pending migration list.
3. Run MigrationJob `apply`.
4. Start or roll WebApi and Worker after migration succeeds.

See `docs/release_and_versioning.md` for release notes and migration note requirements.
