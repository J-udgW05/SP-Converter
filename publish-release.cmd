@echo off
setlocal

where pwsh.exe >nul 2>nul
if errorlevel 1 (
    echo PowerShell 7 ^(pwsh.exe^) was not found. Install it from https://aka.ms/powershell
    pause
    exit /b 1
)

pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-release.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo Publishing failed with exit code %EXIT_CODE%.
) else (
    echo Release published successfully.
)

pause
exit /b %EXIT_CODE%
