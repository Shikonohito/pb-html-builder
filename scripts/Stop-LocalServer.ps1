<#
.SYNOPSIS
Stops the PbHtmlBuilder Blazor dev server started by Start-LocalServer.ps1.

.EXAMPLE
.\scripts\Stop-LocalServer.ps1

Stops the saved PID for the URL configured for the project's http launch profile and removes the state file.

.EXAMPLE
.\scripts\Stop-LocalServer.ps1 -Force

Also allows stopping a process found on the configured port even if its command line cannot be matched to this project.

.NOTES
Paths are resolved relative to this script. The default URL is read from
src\PbHtmlBuilder.Host\Properties\launchSettings.json profile "http", with a fallback to
PbHtmlBuilder:Server:PreferredPort in appsettings.json.
#>

[CmdletBinding()]
param(
    [string]$Url,
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

function Write-Status {
    param([string]$Message)

    [Console]::Out.WriteLine($Message)
    [Console]::Out.Flush()
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
        [string]$ProjectDirectory,
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
    $normalizedProjectDirectory = $ProjectDirectory.ToLowerInvariant()

    return (
        $normalizedCommandLine.Contains($normalizedProjectPath) -or
        $normalizedCommandLine.Contains($normalizedProjectDirectory) -or
        $normalizedCommandLine.Contains('pbhtmlbuilder.host')
    )
}

function Get-ShortCommandLine {
    param(
        [string]$CommandLine,
        [int]$MaxLength = 180
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $null
    }

    if ($CommandLine.Length -le $MaxLength) {
        return $CommandLine
    }

    return "$($CommandLine.Substring(0, $MaxLength - 3))..."
}

function Get-TargetProcessDescription {
    param(
        [System.Diagnostics.Process]$Process,
        [string[]]$Sources,
        [string]$CommandLine,
        [int]$Port
    )

    $roles = @($Sources | ForEach-Object {
            switch -Wildcard ($_) {
                'state:start' { 'launcher process saved as StartProcessId'; break }
                'state' { 'saved server process from state file'; break }
                'port:*' { "process listening on port $Port"; break }
                default { "source: $_" }
            }
        })

    $parts = @($Process.ProcessName)
    if ($roles.Count -gt 0) {
        $parts += ($roles -join '; ')
    }

    $shortCommandLine = Get-ShortCommandLine -CommandLine $CommandLine
    if ($shortCommandLine) {
        $parts += "command: $shortCommandLine"
    }

    return ($parts -join '; ')
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
$projectPath = (Resolve-Path (Join-Path $repoRoot 'src\PbHtmlBuilder.Host\PbHtmlBuilder.Host.csproj')).Path
$projectDirectory = Split-Path -Parent $projectPath
if ([string]::IsNullOrWhiteSpace($Url)) {
    $Url = Get-ConfiguredLocalUrl -ProjectDirectory $projectDirectory
}
$port = Get-UrlPort -Value $Url
$stateFile = Join-Path (Join-Path $repoRoot 'temp') "local-server-$port.json"

$targets = @{}

if (Test-Path $stateFile) {
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        $processIdProperty = $state.PSObject.Properties['ProcessId']
        if ($processIdProperty -and $null -ne $processIdProperty.Value) {
            Add-TargetProcess -Targets $targets -ProcessId ([int]$processIdProperty.Value) -Source 'state'
        }

        $startProcessIdProperty = $state.PSObject.Properties['StartProcessId']
        if ($startProcessIdProperty -and $null -ne $startProcessIdProperty.Value) {
            Add-TargetProcess -Targets $targets -ProcessId ([int]$startProcessIdProperty.Value) -Source 'state:start'
        }

        $projectPathProperty = $state.PSObject.Properties['ProjectPath']
        if ($projectPathProperty -and -not [string]::IsNullOrWhiteSpace([string]$projectPathProperty.Value)) {
            $projectPath = [string]$projectPathProperty.Value
            $projectDirectory = Split-Path -Parent $projectPath
        }
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
    Write-Status "No local server process found for $Url."
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
    $isStateProcess = @($sources | Where-Object { $_ -like 'state*' }).Count -gt 0
    $commandLine = Get-ProcessCommandLine -ProcessId $processId
    $isExpected = Test-ExpectedServerProcess -CommandLine $commandLine -ProjectPath $projectPath -ProjectDirectory $projectDirectory -IsStateProcess $isStateProcess

    if (-not $isExpected) {
        Write-Warning "Skipping PID $processId because it does not look like this project's server. Use -Force to stop it anyway."
        continue
    }

    $processDescription = Get-TargetProcessDescription -Process $process -Sources $sources -CommandLine $commandLine -Port $port
    Write-Status "Stopping PID $processId - $processDescription..."
    try {
        Stop-Process -Id $processId -Force -ErrorAction Stop
        $stoppedAny = $true
    }
    catch {
        if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
            throw
        }

        continue
    }

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
    Write-Status "No local server process found for $Url."
    exit 0
}

if ($stoppedAny) {
    Write-Status "Local server stopped for $Url."
    exit 0
}

Write-Warning "No matching local server process was stopped for $Url."
exit 1
