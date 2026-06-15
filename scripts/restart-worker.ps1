param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Environment = "BidOpsLocal",
    [int]$StartupWaitSeconds = 3
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $ProjectRoot "src\Atlas.Worker\Atlas.Worker.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Atlas.Worker project not found: $projectPath"
}

$logDir = Join-Path $ProjectRoot "var\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logPath = Join-Path $logDir "worker-local.log"

Write-Host "Stopping existing Atlas.Worker processes..."
$processes = Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -and
        (
            $_.Name -eq "Atlas.Worker.exe" -or
            ($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*")
        ) -and
        $_.ProcessId -ne $PID
    }

foreach ($process in $processes) {
    Write-Host "Stopping PID $($process.ProcessId): $($process.Name)"
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 1

$escapedProjectRoot = $ProjectRoot.Replace("'", "''")
$escapedProjectPath = $projectPath.Replace("'", "''")
$escapedLogPath = $logPath.Replace("'", "''")
$escapedEnvironment = $Environment.Replace("'", "''")

Write-Host "Starting Atlas.Worker with DOTNET_ENVIRONMENT='$Environment'..."
$command = @"
Set-Location -LiteralPath '$escapedProjectRoot'
`$env:DOTNET_ENVIRONMENT = '$escapedEnvironment'
`$env:ASPNETCORE_ENVIRONMENT = '$escapedEnvironment'
dotnet run --project '$escapedProjectPath' *> '$escapedLogPath'
"@

Start-Process powershell.exe `
    -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command `
    -WindowStyle Hidden `
    -WorkingDirectory $ProjectRoot

Start-Sleep -Seconds $StartupWaitSeconds

$started = Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -and
        (
            $_.Name -eq "Atlas.Worker.exe" -or
            ($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*")
        )
    } |
    Select-Object ProcessId, Name, CommandLine

Write-Host "Atlas.Worker restart command submitted."
Write-Host "Environment: $Environment"
Write-Host "Log: $logPath"
$started | Format-Table -AutoSize
