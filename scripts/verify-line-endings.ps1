param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$binaryExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.xlsx', '.zip', '.gz', '.dll', '.exe', '.pdb'
)

$violations = New-Object System.Collections.Generic.List[string]

Push-Location $Root
try {
    $files = @(git ls-files --cached --others --exclude-standard | Sort-Object -Unique)
}
finally {
    Pop-Location
}

foreach ($relativePath in $files) {
    $extension = [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()
    if ($binaryExtensions -contains $extension) {
        continue
    }

    $fullPath = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    if ($bytes.Length -eq 0) {
        continue
    }

    $lf = 0
    $crlf = 0
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10) {
            $lf++
            if ($i -gt 0 -and $bytes[$i - 1] -eq 13) {
                $crlf++
            }
        }
    }

    if ($lf -eq 0) {
        continue
    }

    if ($crlf -gt 0 -and $crlf -lt $lf) {
        $violations.Add("$relativePath has mixed line endings.")
        continue
    }

    if ($crlf -gt 0) {
        $violations.Add("$relativePath must use LF line endings.")
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Line ending violations found:`n" + ($violations -join "`n"))
}

Write-Host "Line ending check passed for $($files.Count) repository files."
