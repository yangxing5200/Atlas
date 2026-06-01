# CI Baseline

Atlas CI is the minimum quality gate for every pull request and branch push.

## Trigger

The workflow runs on:

1. `pull_request`
2. `push`

It does not rely on a developer machine, local NuGet sources, or absolute paths.

## Required Gates

The baseline job runs on `windows-latest` with .NET 8 and performs:

1. `scripts/verify-line-endings.ps1`
2. `scripts/verify-central-package-versions.ps1`
3. `dotnet restore Atlas.sln`
4. `dotnet build Atlas.sln --no-restore --configuration Release`
5. Core, Data, and Services test projects with TRX output

Test results are uploaded from `TestResults/**` as a workflow artifact.

## Integration Tests

Integration tests are opt-in because they may require MySQL, Redis, RabbitMQ, or other external services. Set the repository variable `RUN_INTEGRATION_TESTS` to `true` when the CI environment has those dependencies available.

## Local Equivalent

Run the same baseline locally before opening a PR:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-line-endings.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-central-package-versions.ps1
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj --no-build
dotnet test tests/Atlas.Data.Tests/Atlas.Data.Tests.csproj --no-build
dotnet test tests/Atlas.Services.Tests/Atlas.Services.Tests.csproj --no-build
```
