param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Environment = "BidOpsLocal",
    [int]$StartupWaitSeconds = 3,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $ProjectRoot "src\Atlas.Worker\Atlas.Worker.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Atlas.Worker project not found: $projectPath"
}

$logDir = Join-Path $ProjectRoot "var\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logPath = Join-Path $logDir "worker-local.log"

function Stop-AtlasWorkerProcess {
    param(
        [Parameter(Mandatory = $true)]
        $Process
    )

    Write-Host "Stopping PID $($Process.ProcessId): $($Process.Name)"
    try {
        Stop-Process -Id $Process.ProcessId -Force -ErrorAction Stop
        return
    }
    catch {
        Write-Warning "Stop-Process failed for PID $($Process.ProcessId): $($_.Exception.Message). Trying CIM terminate."
    }

    try {
        $result = Invoke-CimMethod -InputObject $Process -MethodName Terminate -ErrorAction Stop
        if ($result.ReturnValue -ne 0) {
            Write-Warning "CIM terminate returned $($result.ReturnValue) for PID $($Process.ProcessId)."
        }
    }
    catch {
        Write-Warning "CIM terminate failed for PID $($Process.ProcessId): $($_.Exception.Message)"
    }
}

Write-Host "Stopping existing Atlas.Worker processes..."
$processes = Get-CimInstance Win32_Process |
    Where-Object {
        $_.ProcessId -ne $PID -and
        (
            $_.Name -eq "Atlas.Worker.exe" -or
            ($_.CommandLine -and $_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*") -or
            ($_.CommandLine -and $_.Name -like "powershell*.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*")
        )
    }

foreach ($process in $processes) {
    Stop-AtlasWorkerProcess -Process $process
}

Start-Sleep -Seconds 1

$escapedProjectRoot = $ProjectRoot.Replace("'", "''")
$escapedProjectPath = $projectPath.Replace("'", "''")
$escapedLogPath = $logPath.Replace("'", "''")
$escapedEnvironment = $Environment.Replace("'", "''")
$noBuildArg = ""
if ($NoBuild) {
    $noBuildArg = "--no-build "
}

Write-Host "Starting Atlas.Worker with DOTNET_ENVIRONMENT='$Environment'..."
$command = @"
Set-Location -LiteralPath '$escapedProjectRoot'
`$env:DOTNET_ENVIRONMENT = '$escapedEnvironment'
`$env:ASPNETCORE_ENVIRONMENT = '$escapedEnvironment'
if ([string]::IsNullOrWhiteSpace(`$env:DEEPSEEK_API_KEY)) {
    `$env:DEEPSEEK_API_KEY = [Environment]::GetEnvironmentVariable('DEEPSEEK_API_KEY', 'User')
}
if ([string]::IsNullOrWhiteSpace(`$env:DEEPSEEK_API_KEY)) {
    `$env:DEEPSEEK_API_KEY = [Environment]::GetEnvironmentVariable('DEEPSEEK_API_KEY', 'Machine')
}
dotnet run $noBuildArg--project '$escapedProjectPath' *> '$escapedLogPath'
"@

$encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

Start-Process powershell.exe `
    -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedCommand `
    -WindowStyle Hidden `
    -WorkingDirectory $ProjectRoot

Start-Sleep -Seconds $StartupWaitSeconds

$started = Get-CimInstance Win32_Process |
    Where-Object {
        (
            $_.Name -eq "Atlas.Worker.exe" -or
            ($_.CommandLine -and $_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*") -or
            ($_.CommandLine -and $_.Name -like "powershell*.exe" -and $_.CommandLine -like "*Atlas.Worker.csproj*")
        )
    } |
    Select-Object ProcessId, Name, CommandLine

Write-Host "Atlas.Worker restart command submitted."
Write-Host "Environment: $Environment"
Write-Host "Log: $logPath"
$started | Format-Table -AutoSize
