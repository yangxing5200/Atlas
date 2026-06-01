param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$centralPackages = Join-Path $Root 'Directory.Packages.props'
if (-not (Test-Path -LiteralPath $centralPackages)) {
    Write-Error "Directory.Packages.props was not found at '$centralPackages'."
}

$supportedExtensions = @('.csproj', '.props', '.targets')
$projectFiles = Get-ChildItem -LiteralPath $Root -Recurse -File |
    Where-Object {
        $supportedExtensions -contains $_.Extension -and
        $_.FullName -notmatch '\\(bin|obj)\\' -and
        $_.Name -ne 'Directory.Packages.props'
    }

$violations = New-Object System.Collections.Generic.List[string]

foreach ($file in $projectFiles) {
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
    $packageReferences = $xml.SelectNodes('//PackageReference')

    foreach ($reference in $packageReferences) {
        $include = $reference.Include
        if ([string]::IsNullOrWhiteSpace($include)) {
            $include = $reference.Update
        }

        if ($reference.Version) {
            $violations.Add("$($file.FullName): PackageReference '$include' must not use a Version attribute.")
        }

        foreach ($child in $reference.ChildNodes) {
            if ($child.Name -eq 'Version') {
                $violations.Add("$($file.FullName): PackageReference '$include' must not use a nested <Version> element.")
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Central package version violations found:`n" + ($violations -join "`n"))
}

Write-Host "Central package version check passed for $($projectFiles.Count) project/build files."
