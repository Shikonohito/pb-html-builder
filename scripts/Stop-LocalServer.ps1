<#
.SYNOPSIS
Stops the PbHtmlEditor Blazor dev server started by Start-LocalServer.ps1.

.EXAMPLE
.\scripts\Stop-LocalServer.ps1

Stops the saved PID for http://localhost:5021 and removes the state file.

.EXAMPLE
.\scripts\Stop-LocalServer.ps1 -Force

Also allows stopping a process found on the configured port even if its command line cannot be matched to this project.
#>

[CmdletBinding()]
param(
    [string]$Url = 'http://localhost:5021',
    [int]$TimeoutSeconds = 10,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

function Get-ProcessCommandLine {
    param([int]$ProcessId)

    try {
        return (Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop).CommandLine
    }
    catch {
        return $null
    }
}

function Test-ExpectedServerProcess {
    param(
        [string]$CommandLine,
        [string]$ProjectPath,
        [bool]$IsStateProcess
    )

    if ($Force) {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $IsStateProcess
    }

    $normalizedCommandLine = $CommandLine.ToLowerInvariant()
    $normalizedProjectPath = $ProjectPath.ToLowerInvariant()

    return (
        $normalizedCommandLine.Contains($normalizedProjectPath) -or
        $normalizedCommandLine.Contains('pbhtmleditor')
    )
}

function Add-TargetProcess {
    param(
        [hashtable]$Targets,
        [int]$ProcessId,
        [string]$Source
    )

    if ($ProcessId -le 0) {
        return
    }

    if (-not $Targets.ContainsKey($ProcessId)) {
        $Targets[$ProcessId] = [System.Collections.Generic.List[string]]::new()
    }

    $Targets[$ProcessId].Add($Source)
}

$scriptDirectory = Get-ScriptDirectory
$repoRoot = Resolve-Path (Join-Path $scriptDirectory '..')
$projectPath = (Resolve-Path (Join-Path $repoRoot 'src\PbHtmlEditor\PbHtmlEditor.csproj')).Path
$port = Get-UrlPort -Value $Url
$stateFile = Join-Path (Join-Path $repoRoot 'temp') "local-server-$port.json"

$targets = @{}

if (Test-Path $stateFile) {
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        Add-TargetProcess -Targets $targets -ProcessId ([int]$state.ProcessId) -Source 'state'
    }
    catch {
        Write-Warning "Ignoring unreadable state file: $stateFile"
    }
}

foreach ($processId in @(Get-ListeningProcessIds -Port $port)) {
    Add-TargetProcess -Targets $targets -ProcessId $processId -Source "port:$port"
}

if ($targets.Count -eq 0) {
    Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    Write-Host "No local server process found for $Url."
    exit 0
}

$stoppedAny = $false
$liveTargetFound = $false
foreach ($entry in $targets.GetEnumerator()) {
    $processId = [int]$entry.Key
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if (-not $process) {
        continue
    }

    $liveTargetFound = $true
    $sources = @($entry.Value)
    $isStateProcess = $sources -contains 'state'
    $commandLine = Get-ProcessCommandLine -ProcessId $processId
    $isExpected = Test-ExpectedServerProcess -CommandLine $commandLine -ProjectPath $projectPath -IsStateProcess $isStateProcess

    if (-not $isExpected) {
        Write-Warning "Skipping PID $processId because it does not look like this project's server. Use -Force to stop it anyway."
        continue
    }

    Write-Host "Stopping PID $processId ($($sources -join ', '))..."
    Stop-Process -Id $processId -Force -ErrorAction Stop
    $stoppedAny = $true

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
        Write-Warning "PID $processId is still running after $TimeoutSeconds seconds."
    }
}

Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue

if (-not $liveTargetFound) {
    Write-Host "No local server process found for $Url."
    exit 0
}

if ($stoppedAny) {
    Write-Host "Local server stopped for $Url."
    exit 0
}

Write-Warning "No matching local server process was stopped for $Url."
exit 1
