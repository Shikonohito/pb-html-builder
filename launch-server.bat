@echo off
setlocal
set "LAUNCH_SERVER_PATH=%PATH%"
set "PATH="
set "Path=%LAUNCH_SERVER_PATH%"
set "LAUNCH_SERVER_BAT=%~f0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $marker = '# POWERSHELL'; $path = $env:LAUNCH_SERVER_BAT; $content = Get-Content -LiteralPath $path -Raw; $index = $content.LastIndexOf($marker); if ($index -lt 0) { throw 'PowerShell marker not found.' }; $script = $content.Substring($index + $marker.Length); Invoke-Expression $script"
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
# POWERSHELL

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $env:LAUNCH_SERVER_BAT
Set-Location -LiteralPath $repoRoot

$logsDirectory = Join-Path $repoRoot 'logs'
$tempDirectory = Join-Path $repoRoot 'temp'
New-Item -ItemType Directory -Path $logsDirectory, $tempDirectory -Force | Out-Null

$url = 'http://localhost:5021'
$startScript = Join-Path $repoRoot 'scripts\Start-LocalServer.ps1'
$stopScript = Join-Path $repoRoot 'scripts\Stop-LocalServer.ps1'
$launcherLog = Join-Path $logsDirectory 'launch-server.log'
$watchdogLog = Join-Path $logsDirectory 'launch-server-watchdog.log'
$startStdoutLog = Join-Path $logsDirectory 'launch-server-start.out.log'
$startStderrLog = Join-Path $logsDirectory 'launch-server-start.err.log'
$watchdogScript = Join-Path $tempDirectory 'launch-server-watchdog.ps1'
$powerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

if (-not (Test-Path -LiteralPath $powerShell)) {
    $powerShell = 'powershell.exe'
}

function Add-LauncherLog {
    param([string]$Message)

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$timestamp] $Message"
    Add-Content -LiteralPath $launcherLog -Value $line
    Write-Host $Message
}

function Invoke-LoggedPowerShell {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,

        [string[]]$Arguments = @()
    )

    $commandArguments = @(
        '-NoLogo',
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $ScriptPath
    ) + $Arguments

    & $powerShell @commandArguments 2>&1 | ForEach-Object {
        $text = $_.ToString()
        Add-Content -LiteralPath $launcherLog -Value $text
        Write-Host $text
    }

    return $LASTEXITCODE
}

function Quote-ProcessArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    return '"' + ($Value -replace '`', '``' -replace '"', '`"') + '"'
}

function Start-LoggedStartScript {
    Remove-Item -LiteralPath $startStdoutLog, $startStderrLog -Force -ErrorAction SilentlyContinue

    $startArguments = @(
        '-NoLogo',
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        (Quote-ProcessArgument $startScript),
        '-Url',
        (Quote-ProcessArgument $url),
        '-LaunchBrowser'
    )

    Add-LauncherLog "Start helper stdout: $startStdoutLog"
    Add-LauncherLog "Start helper stderr: $startStderrLog"

    return Start-Process `
        -FilePath $powerShell `
        -ArgumentList ($startArguments -join ' ') `
        -RedirectStandardOutput $startStdoutLog `
        -RedirectStandardError $startStderrLog `
        -WindowStyle Hidden `
        -PassThru
}

function Start-Watchdog {
    param([int]$StartProcessId)

    @'
param(
    [Parameter(Mandatory = $true)]
    [int]$ParentProcessId,

    [Parameter(Mandatory = $true)]
    [int]$StartProcessId,

    [Parameter(Mandatory = $true)]
    [string]$StopScript,

    [Parameter(Mandatory = $true)]
    [string]$Url,

    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    [Parameter(Mandatory = $true)]
    [string]$PowerShellPath
)

$ErrorActionPreference = 'SilentlyContinue'
$watchdogScriptPath = $PSCommandPath
if ([string]::IsNullOrWhiteSpace($watchdogScriptPath)) {
    $watchdogScriptPath = $MyInvocation.MyCommand.Path
}

function Add-WatchdogLog {
    param([string]$Message)

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $LogPath -Value "[$timestamp] $Message"
}

try {
    Add-WatchdogLog "Watching launcher PID $ParentProcessId."

    while (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 1
    }

    Add-WatchdogLog 'Launcher exited. Stopping local server.'

    $stoppedStartHelper = $false
    if (Get-Process -Id $StartProcessId -ErrorAction SilentlyContinue) {
        Add-WatchdogLog "Stopping start helper PID $StartProcessId."
        Stop-Process -Id $StartProcessId -Force
        $stoppedStartHelper = $true
    }

    & $PowerShellPath -NoLogo -NoProfile -ExecutionPolicy Bypass -File $StopScript -Url $Url 2>&1 | ForEach-Object {
        Add-WatchdogLog $_.ToString()
    }

    if ($stoppedStartHelper) {
        Start-Sleep -Seconds 1
        & $PowerShellPath -NoLogo -NoProfile -ExecutionPolicy Bypass -File $StopScript -Url $Url 2>&1 | ForEach-Object {
            Add-WatchdogLog $_.ToString()
        }
    }
}
finally {
    Add-WatchdogLog 'Watchdog finished.'

    if (-not [string]::IsNullOrWhiteSpace($watchdogScriptPath)) {
        $escapedWatchdogScriptPath = $watchdogScriptPath.Replace("'", "''")
        $cleanupCommand = "`$path = '$escapedWatchdogScriptPath'; for (`$i = 0; `$i -lt 30; `$i++) { Start-Sleep -Seconds 1; Remove-Item -LiteralPath `$path -Force -ErrorAction SilentlyContinue; if (-not (Test-Path -LiteralPath `$path)) { break } }"
        $encodedCleanupCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($cleanupCommand))
        Start-Process -FilePath $PowerShellPath -ArgumentList @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encodedCleanupCommand) -WindowStyle Hidden
    }
}
'@ | Set-Content -LiteralPath $watchdogScript -Encoding UTF8

    $watchdogArguments = @(
        '-NoLogo',
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        (Quote-ProcessArgument $watchdogScript),
        '-ParentProcessId',
        $PID,
        '-StartProcessId',
        $StartProcessId,
        '-StopScript',
        (Quote-ProcessArgument $stopScript),
        '-Url',
        (Quote-ProcessArgument $url),
        '-LogPath',
        (Quote-ProcessArgument $watchdogLog),
        '-PowerShellPath',
        (Quote-ProcessArgument $powerShell)
    )

    Start-Process -FilePath $powerShell -ArgumentList ($watchdogArguments -join ' ') -WindowStyle Hidden | Out-Null
}

$serverStarted = $false
$startProcess = $null

try {
    Add-LauncherLog "Starting local server at $url."
    Add-LauncherLog "Logs directory: $logsDirectory"

    $startProcess = Start-LoggedStartScript
    $serverStarted = $true
    Start-Watchdog -StartProcessId $startProcess.Id

    Add-LauncherLog "Start helper PID: $($startProcess.Id)"
    Add-LauncherLog 'Local server is starting or already running. Type Y and press Enter to stop it.'
    Add-LauncherLog 'Closing this console window also stops the local server.'

    while ($true) {
        Write-Host -NoNewline 'Stop server? [Y] '
        $answer = [Console]::ReadLine()
        if ($null -eq $answer) {
            break
        }

        if ($answer.Trim().Equals('Y', [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
    }
}
finally {
    if ($serverStarted) {
        Add-LauncherLog 'Stopping local server.'
        $stoppedStartHelper = $false

        if ($null -ne $startProcess -and -not $startProcess.HasExited) {
            Add-LauncherLog "Stopping start helper PID $($startProcess.Id)."
            Stop-Process -Id $startProcess.Id -Force -ErrorAction SilentlyContinue
            $stoppedStartHelper = $true
        }

        $stopExitCode = Invoke-LoggedPowerShell -ScriptPath $stopScript -Arguments @('-Url', $url)
        Add-LauncherLog "Stop script finished with exit code $stopExitCode."

        if ($stoppedStartHelper) {
            Start-Sleep -Seconds 1
            $stopExitCode = Invoke-LoggedPowerShell -ScriptPath $stopScript -Arguments @('-Url', $url)
            Add-LauncherLog "Stop verification finished with exit code $stopExitCode."
        }
    }
}
