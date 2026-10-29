@echo off
setlocal DisableDelayedExpansion
cd /d "%~dp0"
rem Prefer the system 64-bit Windows PowerShell, not a different program on PATH.
set "REDMOON_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "REDMOON_POWERSHELL=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
rem The final dot avoids a quoted argument ending in a backslash.
"%REDMOON_POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\Install-ClientConfig.ps1" -Root "%~dp0." -ProfilePath "%~dp0setup\owner-profile.json"
set "REDMOON_EXIT=%ERRORLEVEL%"
if not "%REDMOON_EXIT%"=="0" (
  echo Install-ClientConfig did not complete. Read the error above. Do not delete any database or retry blindly.
)
pause
exit /b %REDMOON_EXIT%
