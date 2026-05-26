@echo off
setlocal

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found. Install .NET 10 SDK and run this file again.
    pause
    exit /b 1
)

dotnet run --project "src\PbHtmlEditor\PbHtmlEditor.csproj" --no-launch-profile -- --urls "http://localhost:5021"

pause
