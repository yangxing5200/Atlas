param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [int]$StartupWaitSeconds = 8,
    [switch]$SkipBuild,
    [switch]$SkipWebApi,
    [switch]$SkipWorker,
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"

$webApiScript = Join-Path $PSScriptRoot "restart-webapi.ps1"
$workerScript = Join-Path $PSScriptRoot "restart-worker.ps1"
$webApiProjectPath = Join-Path $ProjectRoot "src\Atlas.WebApi\Atlas.WebApi.csproj"
$workerProjectPath = Join-Path $ProjectRoot "src\Atlas.Worker\Atlas.Worker.csproj"
$frontendRoot = Join-Path $ProjectRoot "frontend\atlas-admin"
$logDir = Join-Path $ProjectRoot "var\logs"
$frontendLogPath = Join-Path $logDir "frontend-local.log"

function Stop-LocalProcess {
    param(
        [Parameter(Mandatory = $true)]
        $Process
    )

    if ($Process.ProcessId -eq $PID) {
        return
    }

    if (-not (Get-Process -Id $Process.ProcessId -ErrorAction SilentlyContinue)) {
        return
    }

    Write-Host "Stopping PID $($Process.ProcessId): $($Process.Name)"
    try {
        Stop-Process -Id $Process.ProcessId -Force -ErrorAction Stop
        return
    }
    catch {
        if (-not (Get-Process -Id $Process.ProcessId -ErrorAction SilentlyContinue)) {
            return
        }

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

function Stop-AtlasBackendProcesses {
    Write-Host "Stopping existing Atlas.WebApi and Atlas.Worker processes before build..."

    $processes = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ProcessId -ne $PID -and
            (
                $_.Name -eq "Atlas.WebApi.exe" -or
                $_.Name -eq "Atlas.Worker.exe" -or
                ($_.CommandLine -and $_.Name -eq "dotnet.exe" -and ($_.CommandLine -like "*Atlas.WebApi.csproj*" -or $_.CommandLine -like "*Atlas.Worker.csproj*")) -or
                ($_.CommandLine -and $_.Name -like "powershell*.exe" -and ($_.CommandLine -like "*Atlas.WebApi.csproj*" -or $_.CommandLine -like "*Atlas.Worker.csproj*"))
            )
        }

    foreach ($process in $processes) {
        Stop-LocalProcess -Process $process
    }
}

function Stop-FrontendProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FrontendRoot
    )

    Write-Host "Stopping existing Atlas Admin frontend processes..."

    $processesById = @{}
    $frontendRootAlt = $FrontendRoot.Replace("\", "/")

    $matchingProcesses = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ProcessId -ne $PID -and
            $_.CommandLine -and
            (
                $_.CommandLine -like "*$FrontendRoot*" -or
                $_.CommandLine -like "*$frontendRootAlt*" -or
                $_.CommandLine -like "*frontend-local.log*"
            )
        }

    foreach ($process in $matchingProcesses) {
        $processesById[$process.ProcessId] = $process
    }

    if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
        try {
            $listeners = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
            foreach ($listener in $listeners) {
                if ($listener.OwningProcess -and $listener.OwningProcess -ne $PID) {
                    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)" -ErrorAction SilentlyContinue
                    if ($process -and ($process.Name -eq "node.exe" -or $process.Name -eq "npm.cmd" -or $process.Name -like "powershell*.exe")) {
                        $processesById[$process.ProcessId] = $process
                    }
                }
            }
        }
        catch {
            Write-Warning "Could not inspect port 5173: $($_.Exception.Message)"
        }
    }

    foreach ($process in ($processesById.Values | Sort-Object ProcessId)) {
        Stop-LocalProcess -Process $process
    }
}

function Stop-DotNetBuildServers {
    Write-Host "Shutting down .NET build servers..."
    & dotnet build-server shutdown
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE."
    }
}

function Invoke-DotNetProjectBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    if (-not (Test-Path $ProjectPath)) {
        throw "Project not found: $ProjectPath"
    }

    Write-Host "Building $ProjectPath..."
    & dotnet build $ProjectPath --no-restore --nologo --verbosity minimal /nodeReuse:false /m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $ProjectPath with exit code $LASTEXITCODE."
    }
}

function Invoke-PowerShellFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    if (-not (Test-Path $ScriptPath)) {
        throw "Script not found: $ScriptPath"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Script failed: $ScriptPath exited with code $LASTEXITCODE."
    }
}

function Start-Frontend {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FrontendRoot,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    $packageJsonPath = Join-Path $FrontendRoot "package.json"
    if (-not (Test-Path $packageJsonPath)) {
        throw "Atlas Admin package.json not found: $packageJsonPath"
    }

    Stop-FrontendProcesses -FrontendRoot $FrontendRoot

    $escapedFrontendRoot = $FrontendRoot.Replace("'", "''")
    $escapedLogPath = $LogPath.Replace("'", "''")

    Write-Host "Starting Atlas Admin frontend..."
    $command = @"
Set-Location -LiteralPath '$escapedFrontendRoot'
`$env:VITE_DEV_API_PROXY_TARGET = 'http://localhost:5260'
npm run dev *> '$escapedLogPath'
"@

    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    Start-Process powershell.exe `
        -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedCommand `
        -WindowStyle Hidden `
        -WorkingDirectory $FrontendRoot
}

function Test-HttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter(Mandatory = $true)]
        [int[]]$ExpectedStatusCodes
    )

    $statusCode = $null
    $errorMessage = $null

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
        $statusCode = [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        else {
            $errorMessage = $_.Exception.Message
        }
    }

    if ($null -ne $statusCode) {
        if ($ExpectedStatusCodes -contains $statusCode) {
            Write-Host "$Name health: HTTP $statusCode (ok)"
        }
        else {
            Write-Warning "$Name health: HTTP $statusCode (expected $($ExpectedStatusCodes -join '/'))."
        }

        return
    }

    Write-Warning "$Name health: $errorMessage"
}

function Get-AtlasLocalProcesses {
    Get-CimInstance Win32_Process |
        Where-Object {
            $_.CommandLine -and
            (
                $_.Name -eq "Atlas.WebApi.exe" -or
                $_.Name -eq "Atlas.Worker.exe" -or
                ($_.Name -eq "dotnet.exe" -and ($_.CommandLine -like "*Atlas.WebApi.csproj*" -or $_.CommandLine -like "*Atlas.Worker.csproj*")) -or
                ($_.Name -like "powershell*.exe" -and ($_.CommandLine -like "*Atlas.WebApi.csproj*" -or $_.CommandLine -like "*Atlas.Worker.csproj*" -or $_.CommandLine -like "*frontend-local.log*")) -or
                ($_.Name -eq "node.exe" -and $_.CommandLine -like "*vite*")
            )
        } |
        Select-Object ProcessId, Name, CommandLine
}

if (-not (Test-Path $webApiScript)) {
    throw "restart-webapi.ps1 not found: $webApiScript"
}

if (-not (Test-Path $workerScript)) {
    throw "restart-worker.ps1 not found: $workerScript"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host "BidOps local restart"
Write-Host "Project root: $ProjectRoot"
Write-Host "WebApi:  $(-not $SkipWebApi)"
Write-Host "Worker:  $(-not $SkipWorker)"
Write-Host "Frontend: $(-not $SkipFrontend)"
Write-Host "Build:   $(-not $SkipBuild)"

if (-not $SkipBuild -and (-not $SkipWebApi -or -not $SkipWorker)) {
    Stop-AtlasBackendProcesses
    Start-Sleep -Seconds 1
    Stop-DotNetBuildServers

    if (-not $SkipWebApi) {
        Invoke-DotNetProjectBuild -ProjectPath $webApiProjectPath
    }

    if (-not $SkipWorker) {
        Invoke-DotNetProjectBuild -ProjectPath $workerProjectPath
    }

    Stop-DotNetBuildServers
}

if (-not $SkipWebApi) {
    $webApiArgs = @(
        "-ProjectRoot", $ProjectRoot,
        "-Profile", "bidops-local",
        "-StartupWaitSeconds", "1",
        "-NoBuild"
    )
    Invoke-PowerShellFile -ScriptPath $webApiScript -Arguments $webApiArgs
}

if (-not $SkipWorker) {
    $workerArgs = @(
        "-ProjectRoot", $ProjectRoot,
        "-Environment", "BidOpsLocal",
        "-StartupWaitSeconds", "1",
        "-NoBuild"
    )
    Invoke-PowerShellFile -ScriptPath $workerScript -Arguments $workerArgs
}

if (-not $SkipFrontend) {
    Start-Frontend -FrontendRoot $frontendRoot -LogPath $frontendLogPath
}

if ($StartupWaitSeconds -gt 0) {
    Start-Sleep -Seconds $StartupWaitSeconds
}

Write-Host "BidOps local restart command submitted."
Write-Host "Frontend URL: http://localhost:5173"
Write-Host "WebApi URL:   http://localhost:5260"
Write-Host "Logs:         $logDir"

Test-HttpEndpoint -Name "Frontend" -Url "http://localhost:5173/" -ExpectedStatusCodes @(200)
Test-HttpEndpoint -Name "WebApi auth" -Url "http://localhost:5260/api/auth/context" -ExpectedStatusCodes @(401)

Get-AtlasLocalProcesses | Format-Table -AutoSize
