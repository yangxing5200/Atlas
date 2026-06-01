# Atlas Module Template

The `atlas-module` template creates a tenant-safe business module skeleton inside this repository.

## Install

```bash
dotnet new install templates/atlas-module --force
```

## Create

Run the template from the repository root so generated project references resolve to the local Atlas source projects:

```bash
dotnet new atlas-module -n Demo.Module -o .tmp/Demo.Module
dotnet restore .tmp/Demo.Module/Demo.Module.csproj
dotnet build .tmp/Demo.Module/Demo.Module.csproj --no-restore
dotnet test .tmp/Demo.Module/Tests/Demo.Module.Tests/Demo.Module.Tests.csproj --no-restore
```

## Generated Structure

1. `ModuleEntry`: registers services through the Atlas module system.
2. `Entities`: includes tenant-only, shared, and store-only entity examples.
3. `EntityConfigurations`: EF configurations loaded by assembly scanning, without adding `DbSet` properties.
4. `Services`: write service using Repository and UnitOfWork.
5. `Queries`: read service using Repository/QueryBuilder.
6. `Controllers`: API controller using ProblemDetails-compatible flow and tenant admin policy.
7. `Tests`: request-shape test skeleton.

## Verification

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-atlas-module-template.ps1
```

The verification script generates `Demo.Module`, restores, builds, runs the test project, checks the output for forbidden data-access APIs, and confirms a deliberate `DbContext.Set<T>()` violation fails Analyzer checks.
