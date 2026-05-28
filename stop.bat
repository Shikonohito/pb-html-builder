@echo off
setlocal

set "ROOT=%~dp0"
set "URL=http://localhost:5021"

cd /d "%ROOT%" || exit /b 1

echo Stopping PbHtmlEditor local server...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Stop-LocalServer.ps1"

if errorlevel 1 (
    echo.
    echo Failed to stop the local server.
    echo Check whether another process is using %URL%.
    echo.
    pause
    exit /b 1
)

echo.
echo Stop command completed for %URL%.
echo.
pause
