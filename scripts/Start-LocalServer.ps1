<#
.SYNOPSIS
Starts the PbHtmlBuilder Blazor dev server in the background.

.EXAMPLE
.\scripts\Start-LocalServer.ps1

Builds the project without restore, starts the URL configured for the project's http launch profile, waits until it responds, and writes PID/log paths.

.EXAMPLE
.\scripts\Start-LocalServer.ps1 -LaunchBrowser

Starts the configured project URL and opens it after the server is ready.

.EXAMPLE
.\scripts\Start-LocalServer.ps1 -NoBuild

Starts the last built output without building. Useful for sandboxed test agents.

.EXAMPLE
.\scripts\Start-LocalServer.ps1 -Restore

Allows dotnet run to restore packages before building.

.NOTES
The default project path is resolved relative to this script. The default URL is read from
src\PbHtmlBuilder.Host\Properties\launchSettings.json profile "http", with a fallback to
PbHtmlBuilder:Server:PreferredPort in appsettings.json.
#>

[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$Url,
    [int]$TimeoutSeconds = 90,
    [switch]$Restore,
    [switch]$NoBuild,
    [switch]$Force,
    [switch]$LaunchBrowser
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

function Write-Status {
    param([string]$Message)

    [Console]::Out.WriteLine($Message)
    [Console]::Out.Flush()
}

function ConvertTo-CmdArgument {
    param([string]$Value)

    return '"' + ($Value -replace '"', '\"') + '"'
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

function Get-JsonPropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-ConfiguredLocalUrl {
    param(
        [string]$ProjectDirectory,
        [string]$LaunchProfile = 'http'
    )

    $launchSettingsPath = Join-Path $ProjectDirectory 'Properties\launchSettings.json'
    if (Test-Path -LiteralPath $launchSettingsPath) {
        try {
            $launchSettings = Get-Content -LiteralPath $launchSettingsPath -Raw | ConvertFrom-Json
            $profiles = Get-JsonPropertyValue -Object $launchSettings -Name 'profiles'
            $profile = Get-JsonPropertyValue -Object $profiles -Name $LaunchProfile
            $applicationUrl = [string](Get-JsonPropertyValue -Object $profile -Name 'applicationUrl')

            if (-not [string]::IsNullOrWhiteSpace($applicationUrl)) {
                $urls = @($applicationUrl -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                if ($urls.Count -gt 0) {
                    return $urls[0]
                }
            }
        }
        catch {
            Write-Warning "Could not read launch profile URL from ${launchSettingsPath}: $($_.Exception.Message)"
        }
    }

    $appSettingsPath = Join-Path $ProjectDirectory 'appsettings.json'
    if (Test-Path -LiteralPath $appSettingsPath) {
        try {
            $appSettings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
            $pbHtmlBuilder = Get-JsonPropertyValue -Object $appSettings -Name 'PbHtmlBuilder'
            $server = Get-JsonPropertyValue -Object $pbHtmlBuilder -Name 'Server'
            $preferredPort = Get-JsonPropertyValue -Object $server -Name 'PreferredPort'

            if ($null -ne $preferredPort) {
                return "http://localhost:$([int]$preferredPort)"
            }
        }
        catch {
            Write-Warning "Could not read preferred port from ${appSettingsPath}: $($_.Exception.Message)"
        }
    }

    throw "No local server URL is configured in $launchSettingsPath or $appSettingsPath."
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

function Open-LocalBrowser {
    param([string]$Value)

    try {
        Start-Process -FilePath $Value | Out-Null
    }
    catch {
        Write-Warning "Local server is ready, but the browser could not be opened automatically: $($_.Exception.Message)"
        Write-Status "Open this URL manually: $Value"
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
        [string]$ProjectDirectory
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $false
    }

    $normalizedCommandLine = $CommandLine.ToLowerInvariant()
    $normalizedProjectPath = $ProjectPath.ToLowerInvariant()
    $normalizedProjectDirectory = $ProjectDirectory.ToLowerInvariant()

    return (
        $normalizedCommandLine.Contains($normalizedProjectPath) -or
        $normalizedCommandLine.Contains($normalizedProjectDirectory) -or
        $normalizedCommandLine.Contains('pbhtmlbuilder.host')
    )
}

function Write-StateFile {
    param(
        [string]$Path,
        [object]$State
    )

    $State | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

$scriptDirectory = Get-ScriptDirectory
$repoRoot = Resolve-Path (Join-Path $scriptDirectory '..')
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\PbHtmlBuilder.Host\PbHtmlBuilder.Host.csproj'
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFullPath
if ([string]::IsNullOrWhiteSpace($Url)) {
    $Url = Get-ConfiguredLocalUrl -ProjectDirectory $projectDirectory
}
$port = Get-UrlPort -Value $Url

$tempDirectory = Join-Path $repoRoot 'temp'
$logsDirectory = Join-Path $repoRoot 'logs'
$stateFile = Join-Path $tempDirectory "local-server-$port.json"
$runnerFile = Join-Path $tempDirectory "local-server-$port.cmd"
$stdoutLog = Join-Path $logsDirectory "pb-html-$port.out.log"
$stderrLog = Join-Path $logsDirectory "pb-html-$port.err.log"

New-Item -ItemType Directory -Path $tempDirectory, $logsDirectory -Force | Out-Null

$portProcessIds = @(Get-ListeningProcessIds -Port $port)
if (Test-HttpReady -Value $Url) {
    $expectedProcessIds = @($portProcessIds | Where-Object {
            Test-ExpectedServerProcess `
                -CommandLine (Get-ProcessCommandLine -ProcessId $_) `
                -ProjectPath $projectFullPath `
                -ProjectDirectory $projectDirectory
        })

    if ($expectedProcessIds.Count -eq 0 -and $portProcessIds.Count -gt 0) {
        if (-not $Force) {
            Write-Error "Port $port is already serving $Url, but PID(s) $($portProcessIds -join ', ') do not look like this project's server. Stop that process or rerun with -Force."
            exit 1
        }

        & (Join-Path $scriptDirectory 'Stop-LocalServer.ps1') -Url $Url -Force
        $portProcessIds = @()
    }
    else {
        Write-Status "Local server is already ready: $Url"

        if ($expectedProcessIds.Count -gt 0) {
            $stateObject = @{
                ProcessId = [int]$expectedProcessIds[0]
                Url = $Url
                Port = $port
                ProjectPath = $projectFullPath
                StartedAt = (Get-Date).ToString('o')
                StdoutLog = $stdoutLog
                StderrLog = $stderrLog
            }
            Write-StateFile -Path $stateFile -State $stateObject
            Write-Status "PID: $($expectedProcessIds[0])"
        }

        if (Test-Path $stateFile) {
            Write-Status "State file: $stateFile"
        }

        if ($LaunchBrowser) {
            Open-LocalBrowser -Value $Url
        }

        exit 0
    }
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
    '--LocalApp:LaunchBrowser=false'
)

Write-Status "Starting local server..."
Write-Status "Project: $projectFullPath"
Write-Status "URL: $Url"
Write-Status "Logs: $stdoutLog"

$dotnetCommand = @((ConvertTo-CmdArgument -Value 'dotnet')) +
    @($dotnetArguments | ForEach-Object { ConvertTo-CmdArgument -Value $_ }) +
    @('>', (ConvertTo-CmdArgument -Value $stdoutLog), '2>', (ConvertTo-CmdArgument -Value $stderrLog))

Set-Content -LiteralPath $runnerFile -Encoding ASCII -Value @(
    '@echo off',
    "cd /d $(ConvertTo-CmdArgument -Value $projectDirectory)",
    ($dotnetCommand -join ' ')
)

$process = Start-Process `
    -FilePath 'cmd.exe' `
    -ArgumentList @('/d', '/c', 'call', $runnerFile) `
    -WorkingDirectory $projectDirectory `
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

Write-StateFile -Path $stateFile -State $stateObject

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
            Write-StateFile -Path $stateFile -State $stateObject
        }

        Write-Status "Local server is ready: $Url"
        Write-Status "PID: $serverProcessId"
        Write-Status "State file: $stateFile"
        if ($LaunchBrowser) {
            Open-LocalBrowser -Value $Url
        }

        exit 0
    }

    Start-Sleep -Milliseconds 500
}

Write-Error "Local server did not become ready within $TimeoutSeconds seconds. PID $($process.Id) is still running; see $stdoutLog and $stderrLog."
exit 2
