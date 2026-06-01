param(
    [string]$ModuleName = "Demo.Module"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$tmpRoot = Join-Path $repoRoot ".tmp"
$outputPath = Join-Path $tmpRoot $ModuleName
$templatePath = Join-Path $repoRoot "templates\atlas-module"

if (-not (Test-Path -LiteralPath $tmpRoot)) {
    New-Item -ItemType Directory -Path $tmpRoot | Out-Null
}

$resolvedTmpRoot = (Resolve-Path -LiteralPath $tmpRoot).Path
if (-not $resolvedTmpRoot.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use temp directory outside repository: $resolvedTmpRoot"
}

if (Test-Path -LiteralPath $outputPath) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputPath).Path
    if (-not $resolvedOutput.StartsWith($resolvedTmpRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete path outside repository temp directory: $resolvedOutput"
    }

    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

dotnet new install $templatePath --force
dotnet new atlas-module -n $ModuleName -o $outputPath

Push-Location $outputPath
try {
    $testProject = Join-Path $outputPath "Tests\$ModuleName.Tests\$ModuleName.Tests.csproj"

    dotnet restore "$ModuleName.csproj"
    dotnet build "$ModuleName.csproj" --no-restore
    dotnet restore $testProject
    dotnet test $testProject --no-restore

    $forbidden = & rg "AtlasTenantDbContext|ITenantDbContextFactory|DbContext|\.Set<|FromSql|ExecuteSql|IgnoreQueryFilters" . -g "*.cs" -g "*.csproj" --glob "!bin/**" --glob "!obj/**"
    if ($LASTEXITCODE -eq 0) {
        throw "Generated template output contains forbidden data-access APIs:`n$($forbidden -join [Environment]::NewLine)"
    }

    $dbSetMatches = & rg "DbSet<" . -g "*.cs" -g "*.csproj" --glob "!bin/**" --glob "!obj/**"
    if ($LASTEXITCODE -eq 0) {
        throw "Generated template output contains DbSet declarations:`n$($dbSetMatches -join [Environment]::NewLine)"
    }

    $projectPath = Join-Path $outputPath "$ModuleName.csproj"
    $originalProjectText = Get-Content -LiteralPath $projectPath -Raw
    $probeProjectText = $originalProjectText -replace "<Nullable>enable</Nullable>", "<Nullable>enable</Nullable>`n    <AssemblyName>Atlas.TemplateAnalyzerProbe</AssemblyName>"
    Set-Content -LiteralPath $projectPath -Value $probeProjectText -Encoding UTF8

    $probePath = Join-Path $outputPath "AnalyzerViolationProbe.cs"
    @"
using Microsoft.EntityFrameworkCore;

namespace $ModuleName;

public sealed class AnalyzerViolationProbe
{
    public object Probe(DbContext db) => db.Set<Entities.TenantRecord>();
}
"@ | Set-Content -LiteralPath $probePath -Encoding UTF8

    $probeOutput = & dotnet build "$ModuleName.csproj" --no-restore 2>&1
    $probeText = $probeOutput | Out-String
    Remove-Item -LiteralPath $probePath -Force
    Set-Content -LiteralPath $projectPath -Value $originalProjectText -Encoding UTF8

    if ($LASTEXITCODE -eq 0) {
        throw "Analyzer violation probe unexpectedly built successfully."
    }

    if ($probeText -notmatch "ATL001" -or $probeText -notmatch "ATL002") {
        throw "Analyzer violation probe did not report ATL001 and ATL002 as expected:`n$probeText"
    }
}
finally {
    Pop-Location
}

Write-Host "Atlas module template verification passed for $ModuleName."
