# Atlas {{version}} Release Notes

## Summary

- Multi-tenant scaffold release for Atlas framework libraries, WebApi, Worker, and MigrationJob.

## Breaking Changes

- None declared for this release.

## Migration Notes

- Run `Atlas.MigrationJob plan` before applying tenant schema changes.
- Run `Atlas.MigrationJob apply --dry-run` in pre-production before production apply.
- Review `docs/tenant_migration_lifecycle.md` for tenant migration status and retry handling.

## Operational Notes

- WebApi and Worker should be deployed as separate processes.
- Enable OpenTelemetry with `Observability:OpenTelemetry:Enabled=true` and choose `Console` or `Otlp` exporter per environment.

## Artifacts

- NuGet packages: `artifacts/packages`
- Docker images: `atlas-webapi`, `atlas-worker`, `atlas-migration-job`
