param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$exitCode = 0
$removePidFile = $false
$pidFile = $null

try {
    $rootPath = Resolve-Path $Root -ErrorAction Stop

    $pidDir = Join-Path $rootPath.Path 'temp'
    $pidFile = Join-Path $pidDir 'pb-html-5021.pid'

    if (-not (Test-Path -LiteralPath $pidFile -PathType Leaf)) {
        Write-Host "[WARN] Server is not running. PID file not found: $pidFile"
        exit 2
    }

    $removePidFile = $true
    $serverPid = Get-Content -LiteralPath $pidFile -ErrorAction Stop | Select-Object -First 1

    if (-not $serverPid) {
        Write-Host "[ERROR] PID file is empty: $pidFile"
        $exitCode = 3
    }
    elseif (-not ($serverPid -as [int])) {
        Write-Host "[ERROR] PID file contains an invalid process id: $serverPid"
        $exitCode = 3
    }
    else {
        $serverPid = [int]$serverPid
        $process = Get-Process -Id $serverPid -ErrorAction SilentlyContinue

        if ($process) {
            Stop-Process -Id $serverPid -Force -ErrorAction Stop

            if (-not $process.WaitForExit(10000)) {
                throw "Process did not exit within 10 seconds: $serverPid ($($process.ProcessName))"
            }

            Write-Host "[SUCCESS] Stopped process $serverPid ($($process.ProcessName))"
        }
        else {
            Write-Host "[INFO] Process already stopped: $serverPid"
        }
    }
}
catch {
    Write-Host "[ERROR] Failed to stop server:"
    Write-Host $_
    $removePidFile = $false
    $exitCode = 4
}

if ($removePidFile -and $pidFile) {
    try {
        Remove-Item -LiteralPath $pidFile -ErrorAction Stop

        if (-not (Test-Path -LiteralPath $pidFile)) {
            Write-Host "[INFO] PID file removed: $pidFile"
        }
        else {
            Write-Host "[WARN] PID file was not removed: $pidFile"
        }
    }
    catch {
        Write-Host "[WARN] Failed to remove PID file: $pidFile"
        Write-Host $_

        if ($exitCode -eq 0) {
            $exitCode = 5
        }
    }
}

exit $exitCode
