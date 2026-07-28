@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
if errorlevel 1 (
  echo.
  echo [ERROR] MotoCare deployment failed.
  exit /b 1
)

echo.
echo [OK] MotoCare deployment completed.
exit /b 0
