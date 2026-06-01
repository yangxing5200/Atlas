param(
    [switch]$SkipDocker,
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$ResetDatabase
)

$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

Write-Host "Atlas bootstrap" -ForegroundColor Cyan
Write-Host "Repository: $PSScriptRoot"
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required."
}

if (-not $SkipDocker) {
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        Write-Host "Starting local infrastructure..." -ForegroundColor Yellow
        docker compose up -d mysql redis rabbitmq
    }
    else {
        Write-Warning "Docker was not found. Skipping docker compose startup."
    }
}

if (-not $SkipRestore) {
    Write-Host "Restoring solution..." -ForegroundColor Yellow
    dotnet restore Atlas.sln
}

if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build Atlas.sln --no-restore
}

if ($ResetDatabase) {
    Write-Warning "ResetDatabase deletes and recreates the local atlas_global and atlas databases."
    dotnet run --project tools/Atlas.LocalSetup/Atlas.LocalSetup.csproj --no-build
}
else {
    Write-Host "Database reset skipped. Re-run with -ResetDatabase to seed the local demo databases." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "WebApi:  dotnet run --project src/Atlas.WebApi/Atlas.WebApi.csproj"
Write-Host "Sample:  dotnet run --project samples/Atlas.Sample.WebApi/Atlas.Sample.WebApi.csproj"
Write-Host "Worker:  dotnet run --project src/Atlas.Worker/Atlas.Worker.csproj"
