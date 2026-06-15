param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Profile = "bidops-local",
    [int]$StartupWaitSeconds = 3
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $ProjectRoot "src\Atlas.WebApi\Atlas.WebApi.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Atlas.WebApi project not found: $projectPath"
}

$logDir = Join-Path $ProjectRoot "var\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logPath = Join-Path $logDir "webapi-local.log"

Write-Host "Stopping existing Atlas.WebApi processes..."
$processes = Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -and
        ($_.CommandLine -like "*Atlas.WebApi*" -or $_.CommandLine -like "*Atlas.WebApi.csproj*") -and
        $_.ProcessId -ne $PID
    }

foreach ($process in $processes) {
    Write-Host "Stopping PID $($process.ProcessId): $($process.Name)"
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 1

Write-Host "Starting Atlas.WebApi with launch profile '$Profile'..."
$command = @"
Set-Location -LiteralPath '$ProjectRoot'
dotnet run --project '$projectPath' --launch-profile '$Profile' *> '$logPath'
"@

Start-Process powershell.exe `
    -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command `
    -WindowStyle Hidden `
    -WorkingDirectory $ProjectRoot

Start-Sleep -Seconds $StartupWaitSeconds

$started = Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -and
        ($_.CommandLine -like "*Atlas.WebApi*" -or $_.CommandLine -like "*Atlas.WebApi.csproj*")
    } |
    Select-Object ProcessId, Name, CommandLine

Write-Host "Atlas.WebApi restart command submitted."
Write-Host "URL: http://localhost:5260"
Write-Host "Log: $logPath"
$started | Format-Table -AutoSize
