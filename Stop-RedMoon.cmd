@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\Stop-RedMoon.ps1"
if errorlevel 1 (
  echo Stop did not complete. Read the error above. No existing database was reset.
  pause
  exit /b 1
)
pause
