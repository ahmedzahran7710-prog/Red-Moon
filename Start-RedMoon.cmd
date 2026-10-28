@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\Start-RedMoon.ps1"
if errorlevel 1 (
  echo Start did not complete. Read the error above. No existing database was reset.
  pause
  exit /b 1
)
pause
