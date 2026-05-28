@echo off
setlocal

set "ROOT=%~dp0"
set "URL=http://localhost:5021"

cd /d "%ROOT%" || exit /b 1

echo Starting PbHtmlEditor local server...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Start-LocalServer.ps1" -Restore

if errorlevel 1 (
    echo.
    echo Failed to start the local server.
    echo Check logs in "%ROOT%logs".
    echo.
    pause
    exit /b 1
)

echo.
echo Opening %URL%
start "" "%URL%"
echo.
echo Server is running in the background.
echo To stop it, run: stop.bat
pause
