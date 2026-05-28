<#
.SYNOPSIS
Starts the PbHtmlEditor Blazor dev server in the background.

.EXAMPLE
.\scripts\Start-LocalServer.ps1

Builds the project without restore, starts http://localhost:5021, waits until it responds, and writes PID/log paths.

.EXAMPLE
.\scripts\Start-LocalServer.ps1 -NoBuild

Starts the last built output without building. Useful for sandboxed test agents.

.EXAMPLE
.\scripts\Start-LocalServer.ps1 -Restore

Allows dotnet run to restore packages before building.
#>

[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$Url = 'http://localhost:5021',
    [int]$TimeoutSeconds = 90,
    [switch]$Restore,
    [switch]$NoBuild,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-ScriptDirectory {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    if ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    return (Get-Location).Path
}

function Get-UrlPort {
    param([string]$Value)

    $uri = [Uri]$Value
    if ($uri.Port -gt 0) {
        return $uri.Port
    }

    if ($uri.Scheme -eq 'https') {
        return 443
    }

    return 80
}

function Test-HttpReady {
    param([string]$Value)

    try {
        $response = Invoke-WebRequest -Uri $Value -UseBasicParsing -TimeoutSec 2
        return ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500)
    }
    catch {
        return $false
    }
}

function Get-ListeningProcessIds {
    param([int]$Port)

    try {
        return @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop |
            Select-Object -ExpandProperty OwningProcess -Unique |
            Where-Object { $_ -gt 0 })
    }
    catch {
        return @()
    }
}

$scriptDirectory = Get-ScriptDirectory
$repoRoot = Resolve-Path (Join-Path $scriptDirectory '..')
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\PbHtmlEditor\PbHtmlEditor.csproj'
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFullPath
$port = Get-UrlPort -Value $Url

$tempDirectory = Join-Path $repoRoot 'temp'
$logsDirectory = Join-Path $repoRoot 'logs'
$stateFile = Join-Path $tempDirectory "local-server-$port.json"
$stdoutLog = Join-Path $logsDirectory "pb-html-$port.out.log"
$stderrLog = Join-Path $logsDirectory "pb-html-$port.err.log"

New-Item -ItemType Directory -Path $tempDirectory, $logsDirectory -Force | Out-Null

if (Test-HttpReady -Value $Url) {
    Write-Host "Local server is already ready: $Url"
    if (Test-Path $stateFile) {
        Write-Host "State file: $stateFile"
    }

    exit 0
}

$stateProcessId = $null
if (Test-Path $stateFile) {
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        $stateProcessId = [int]$state.ProcessId
    }
    catch {
        Write-Warning "Ignoring unreadable state file: $stateFile"
    }
}

if ($stateProcessId) {
    $stateProcess = Get-Process -Id $stateProcessId -ErrorAction SilentlyContinue
    if ($stateProcess) {
    if (-not $Force) {
        Write-Error "A local server process from $stateFile is still running as PID $stateProcessId, but $Url is not ready. Run scripts\Stop-LocalServer.ps1 or rerun with -Force."
        exit 1
    }

        & (Join-Path $scriptDirectory 'Stop-LocalServer.ps1') -Url $Url -Force
    }
    else {
        Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    }
}

$portProcessIds = @(Get-ListeningProcessIds -Port $port)
if ($portProcessIds.Count -gt 0) {
    if (-not $Force) {
        Write-Error "Port $port is already in use by PID(s): $($portProcessIds -join ', '). Stop that process or rerun with -Force."
        exit 1
    }

    & (Join-Path $scriptDirectory 'Stop-LocalServer.ps1') -Url $Url -Force
}

Remove-Item -LiteralPath $stdoutLog, $stderrLog -Force -ErrorAction SilentlyContinue

$dotnetArguments = @(
    'run',
    '--project', $projectFullPath,
    '--launch-profile', 'http'
)

if (-not $Restore) {
    $dotnetArguments += '--no-restore'
}

if ($NoBuild) {
    $dotnetArguments += '--no-build'
}

$dotnetArguments += @(
    '--',
    '--urls', $Url,
    '--LaunchBrowser=false'
)

Write-Host "Starting local server..."
Write-Host "Project: $projectFullPath"
Write-Host "URL: $Url"
Write-Host "Logs: $stdoutLog"

$process = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList $dotnetArguments `
    -WorkingDirectory $projectDirectory `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -WindowStyle Hidden `
    -PassThru

$stateObject = [ordered]@{
    ProcessId = $process.Id
    Url = $Url
    Port = $port
    ProjectPath = $projectFullPath
    StartedAt = (Get-Date).ToString('o')
    StdoutLog = $stdoutLog
    StderrLog = $stderrLog
}

$stateObject | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding UTF8

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if ($process.HasExited) {
        Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
        Write-Error "Local server exited early with code $($process.ExitCode). See logs: $stdoutLog and $stderrLog"
        exit 1
    }

    if (Test-HttpReady -Value $Url) {
        $serverProcessId = $process.Id
        $listenerProcessIds = @(Get-ListeningProcessIds -Port $port)
        if ($listenerProcessIds.Count -gt 0) {
            $serverProcessId = [int]$listenerProcessIds[0]
            $stateObject['ProcessId'] = $serverProcessId
            $stateObject['StartProcessId'] = $process.Id
            $stateObject | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding UTF8
        }

        Write-Host "Local server is ready: $Url"
        Write-Host "PID: $serverProcessId"
        Write-Host "State file: $stateFile"
        exit 0
    }

    Start-Sleep -Milliseconds 500
}

Write-Error "Local server did not become ready within $TimeoutSeconds seconds. PID $($process.Id) is still running; see $stdoutLog and $stderrLog."
exit 2
