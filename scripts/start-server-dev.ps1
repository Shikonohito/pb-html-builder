param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [Parameter(Mandatory = $true)]
    [string]$Root,

    [int]$TimeoutSeconds = 60
)

$p = $null

try {
    $rootPath = Resolve-Path $Root -ErrorAction Stop

    $logDir = Join-Path $rootPath.Path 'logs'
    New-Item -ItemType Directory -Force -Path $logDir -ErrorAction Stop | Out-Null

    $out = Join-Path $logDir 'pb-html-5021.out.log'
    $err = Join-Path $logDir 'pb-html-5021.err.log'

    $pidDir = Join-Path $rootPath.Path 'temp'
    New-Item -ItemType Directory -Force -Path $pidDir -ErrorAction Stop | Out-Null

    $pidFile = Join-Path $pidDir 'pb-html-5021.pid'
    $project = Join-Path $rootPath.Path 'src\PbHtmlEditor\PbHtmlEditor.csproj'

    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Project file not found: $project"
    }

    Remove-Item -LiteralPath $out,$err,$pidFile -ErrorAction SilentlyContinue

    $p = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @(
            'run',
            '--project', $project,
            '--urls', $Url
        ) `
        -WorkingDirectory $rootPath.Path `
        -RedirectStandardOutput $out `
        -RedirectStandardError $err `
        -WindowStyle Hidden `
        -PassThru `
        -Environment @{
            ASPNETCORE_ENVIRONMENT = 'Development'
            LaunchBrowser = 'false'
        }

    $p.Id | Set-Content -LiteralPath $pidFile

    Write-Host "[INFO] Server process started: $Url"
    Write-Host "[INFO] PID: $($p.Id)"
    Write-Host "[INFO] OutLog: $out"
    Write-Host "[INFO] ErrLog: $err"
    Write-Host "[INFO] Waiting for server to become ready..."

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastReadinessError = $null

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500

        $runningProcess = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        if (-not $runningProcess) {
            $p.Refresh()
            throw "Server process exited before it became ready. ExitCode: $($p.ExitCode). See logs: $out, $err"
        }

        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop

            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-Host "[SUCCESS] Server is ready: $Url"
                exit 0
            }
        }
        catch {
            $lastReadinessError = $_.Exception.Message

            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -lt 500) {
                Write-Host "[SUCCESS] Server is responding: $Url"
                exit 0
            }
        }
    }

    throw "Server process started but did not become ready within $TimeoutSeconds seconds. Last readiness check error: $lastReadinessError. See logs: $out, $err"
}
catch {
    if ($p) {
        $runningProcess = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        if ($runningProcess) {
            try {
                Stop-Process -Id $p.Id -Force -ErrorAction Stop
                Write-Host "[INFO] Stopped server process after failed startup: $($p.Id)"
            }
            catch {
                Write-Host "[WARN] Failed to stop server process after failed startup: $($p.Id)"
                Write-Host $_
            }
        }

        if ($pidFile -and (Test-Path -LiteralPath $pidFile)) {
            Remove-Item -LiteralPath $pidFile -ErrorAction SilentlyContinue
        }
    }

    Write-Host "[ERROR] Server start failed:"
    Write-Host $_
    exit 1
}
