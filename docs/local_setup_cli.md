# Atlas.LocalSetup CLI

`tools/Atlas.LocalSetup` is the local/demo data initializer. It is intentionally separate from production migration flow.

## Commands

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- init-global
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- create-tenant-db
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- seed-demo
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- reset-demo
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- seed-production
```

| Command | Behavior |
| --- | --- |
| `init-global` | Creates the local Global database schema from the current model if needed. |
| `create-tenant-db` | Creates the local tenant database schema from the current model if needed. |
| `seed-demo` / `seed-local` | Idempotently writes demo tenant, stores, users, products, and inventory. |
| `reset-demo` | Drops and recreates local demo databases, then writes demo data. |
| `seed-production` | Schema-only production-safe seed. Demo tenant data is intentionally excluded. |

Connections can be passed by argument or environment variable:

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj -- seed-demo `
  --global "Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;" `
  --tenant "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;"
```

Environment variables:

| Variable | Purpose |
| --- | --- |
| `ATLAS_GLOBAL_CONNECTION` | Global database connection string. |
| `ATLAS_TENANT_CONNECTION` | Tenant database connection string. |

Connection-string passwords are masked in console output.

## Seed Policy

- Demo/local seed may create demo tenant, demo users, stores, products, and inventory.
- Production seed must not create demo tenants, demo users, or demo business data.
- Demo seed is idempotent by stable IDs and can be run repeatedly.
- Local setup creates schema from the current model and removes indexes to stay compatible with older local MySQL/InnoDB limits. It is not a replacement for the production migration flow.
- If local schema drifts after model changes, use `reset-demo` to recreate demo databases from the current model.
