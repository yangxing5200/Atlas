param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/packages",
    [string]$PackageVersion,
    [switch]$NoBuild,
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$solution = Join-Path $Root 'Atlas.sln'
$outputPath = Join-Path $Root $Output
New-Item -ItemType Directory -Force $outputPath | Out-Null

$excludedSegments = @(
    '\samples\',
    '\tests\',
    '\tools\',
    '\src\Atlas.Analyzers\',
    '\src\Atlas.WebApi\',
    '\src\Atlas.Worker\',
    '\src\Atlas.MigrationJob\'
)

$projects = dotnet sln $solution list |
    Where-Object { $_ -match '\.csproj$' } |
    ForEach-Object { $_.Trim() } |
    Where-Object {
        $fullPath = Join-Path $Root $_
        $normalized = $fullPath.Replace('/', '\')
        -not ($excludedSegments | Where-Object { $normalized.Contains($_) })
    }

foreach ($project in $projects) {
    $projectPath = Join-Path $Root $project
    $arguments = @(
        'pack',
        $projectPath,
        '--configuration',
        $Configuration,
        '--output',
        $outputPath
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $arguments += "/p:PackageVersion=$PackageVersion"
    }

    & dotnet @arguments
}

Write-Host "Packed $($projects.Count) Atlas framework projects to $outputPath."
