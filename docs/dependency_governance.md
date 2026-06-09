# Dependency Governance

Date: 2026-06-09

## Version Policy

- Runtime target is .NET 8 LTS.
- `Microsoft.Extensions.*` packages must stay on major version 8 unless a project owner records an exception in this file and the governance test/script allow-list.
- Entity Framework Core packages must stay on major version 8 while the runtime target is .NET 8.
- MassTransit, Serilog, OpenTelemetry, Redis, BCrypt, AutoMapper and test packages follow their own product semver lines.
- Package versions are centrally managed in `Directory.Packages.props`; project files should not add inline `Version` attributes.

## Current Inventory

- Microsoft.Extensions: runtime packages stay on 8.x; approved Abstractions-only exceptions are listed below.
- Entity Framework Core: 8.0.0.
- Pomelo.EntityFrameworkCore.MySql: 8.0.0.
- MassTransit: 8.4.1.
- Serilog core/sinks/settings: product line versions 4.x to 9.x.
- OpenTelemetry: 1.15.x.
- Testcontainers: 3.6.0.

## Guardrails

- Run `tools/check-architecture-governance.ps1` before introducing package or project-reference changes.
- `ArchitectureGovernanceTests` blocks these regressions in tests:
  - `Atlas.Data.Abstractions` referencing EF Core packages, EF project adapters, or EF namespaces.
  - `Atlas.Core` referencing Infrastructure, Data, or Services projects.
  - `Microsoft.Extensions.*` packages drifting away from major version 8 without an explicit allow-list entry.

## Exceptions

Approved exceptions:

- `Microsoft.Extensions.Logging.Abstractions` 10.0.0.
  Reason: `AutoMapper` 16.1.1 removes the known high severity advisory affecting older AutoMapper versions and depends on this contract package. This is an abstractions-only package and does not move the hosting/runtime stack away from .NET 8.
- `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0.
  Reason: transitive contract dependency of `Microsoft.Extensions.Logging.Abstractions` 10.0.0. Runtime DI package remains on major version 8.
