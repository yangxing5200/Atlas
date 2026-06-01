# Warning Policy

Atlas treats production-code warnings differently from tests and samples.

## Production Code

Projects outside tests and samples escalate high-risk compiler warnings to errors:

```text
CS0108, CS0168, CS0618, CS8601, CS8602, CS8603, CS8604, CS8618, CS8625, CS8766
```

These categories cover hidden members, unused exception variables, obsolete APIs, and nullable issues that can become runtime defects in shared tenant infrastructure.

## Tests and Samples

Test projects and `Atlas.Sample.*` projects do not treat warnings as errors yet. They remain visible in build output and should be reduced incrementally without blocking production-code hardening.

The current baseline after the first cleanup is:

1. `src`, `tools`, and framework projects build without warnings in local Debug builds.
2. Release solution builds may still report warnings from `tests/*`, mainly nullable initialization in test models and integration-test fixtures.
3. CI keeps tests warning-tolerant until those fixtures are cleaned in a later dedicated task.

## Local Verification

Use:

```powershell
dotnet build Atlas.sln --no-restore
dotnet build Atlas.sln --no-restore --configuration Release
```

Any new high-risk warning in production code is expected to fail the build.
