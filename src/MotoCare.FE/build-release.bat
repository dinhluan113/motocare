@echo off
setlocal
cd /d "%~dp0"

if /I "%~1"=="init-key" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\release-windows.ps1" -InitKey
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\release-windows.ps1" -Version "%~1" -Notes "%~2"
)

if errorlevel 1 (
  echo.
  echo [ERROR] Release failed.
  exit /b 1
)

echo.
echo [OK] Release completed.
exit /b 0
