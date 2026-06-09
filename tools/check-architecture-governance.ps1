param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Get-XmlItems {
    param(
        [xml]$Document,
        [string]$Name
    )

    return $Document.SelectNodes("//*[local-name()='$Name']")
}

function Assert-NoItems {
    param(
        [string]$Message,
        [object[]]$Items
    )

    if ($Items.Count -gt 0) {
        Write-Error "$Message`n$($Items -join "`n")"
    }
}

function Test-AllowedMicrosoftExtensionsException {
    param(
        [string]$Name,
        [string]$Version
    )

    return $Version -eq "10.0.0" -and (
        $Name -eq "Microsoft.Extensions.Logging.Abstractions" -or
        $Name -eq "Microsoft.Extensions.DependencyInjection.Abstractions"
    )
}

$packagesPath = Join-Path $RepoRoot "Directory.Packages.props"
[xml]$packages = Get-Content -LiteralPath $packagesPath

$extensionViolations = @()
foreach ($item in Get-XmlItems -Document $packages -Name "PackageVersion") {
    $name = $item.Include
    $version = $item.Version
    if ($name -like "Microsoft.Extensions.*") {
        $major = [int]($version -split '[.-]')[0]
        if ($major -ne 8 -and -not (Test-AllowedMicrosoftExtensionsException -Name $name -Version $version)) {
            $extensionViolations += "$name $version"
        }
    }
}

Assert-NoItems "Microsoft.Extensions.* packages must stay on major version 8 unless documented and allow-listed." $extensionViolations

$dataAbstractionsPath = Join-Path $RepoRoot "src/Atlas.Data.Abstractions/Atlas.Data.Abstractions.csproj"
[xml]$dataAbstractions = Get-Content -LiteralPath $dataAbstractionsPath
$efReferences = @()
foreach ($itemName in @("PackageReference", "ProjectReference")) {
    foreach ($item in Get-XmlItems -Document $dataAbstractions -Name $itemName) {
        $include = $item.Include
        if ($include -match "EntityFrameworkCore|Atlas\.Data\.EntityFramework") {
            $efReferences += "$itemName $include"
        }
    }
}

$efSourceReferences = Get-ChildItem -LiteralPath (Split-Path $dataAbstractionsPath) -Filter *.cs -Recurse |
    Where-Object { $_.FullName -notmatch "\\obj\\" } |
    Select-String -Pattern "Microsoft.EntityFrameworkCore" |
    ForEach-Object { "$($_.Path):$($_.LineNumber)" }

Assert-NoItems "Atlas.Data.Abstractions must not reference Entity Framework." ($efReferences + $efSourceReferences)

$corePath = Join-Path $RepoRoot "src/Atlas.Core/Atlas.Core.csproj"
[xml]$core = Get-Content -LiteralPath $corePath
$coreViolations = @()
foreach ($item in Get-XmlItems -Document $core -Name "ProjectReference") {
    $include = $item.Include
    if ($include -match "Atlas\.(Infrastructure|Data|Services)") {
        $coreViolations += "ProjectReference $include"
    }
}

Assert-NoItems "Atlas.Core must not reference Infrastructure, Data, or Services projects." $coreViolations

Write-Host "Architecture governance checks passed."
