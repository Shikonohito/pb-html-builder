$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$url = 'http://localhost:5021'

$startScript = Join-Path $PSScriptRoot 'start-server-dev.ps1'
$stopScript  = Join-Path $PSScriptRoot 'stop-server-dev.ps1'

& $startScript -Url $url -Root $root.Path
$startExitCode = $LASTEXITCODE

if ($startExitCode -ne 0) {
    Write-Host "[ERROR] Server start failed. ExitCode: $startExitCode"
    Write-Host "Press any key to exit..."
    [void][Console]::ReadKey($true)
    exit $startExitCode
}

try {
    Start-Process $url -ErrorAction Stop
    Write-Host "[INFO] Browser opened: $url"
}
catch {
    Write-Host "[WARN] Failed to open browser: $url"
    Write-Host $_
}

Write-Host "Press any key to stop the server..."
[void][Console]::ReadKey($true)

& $stopScript -Root $root.Path
$stopExitCode = $LASTEXITCODE

if ($stopExitCode -ne 0) {
    Write-Host "[ERROR] Server stop failed. ExitCode: $stopExitCode"
}
else {
    Write-Host "[INFO] Server stop completed."
}

Write-Host "Press any key to exit..."
[void][Console]::ReadKey($true)

exit $stopExitCode
