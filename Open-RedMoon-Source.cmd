@echo off
setlocal DisableDelayedExpansion
rem Open source for editing only; do not start servers, build or run tests.
if not exist "%~dp0src\RedMoon.sln" (
  echo Source solution not found. Extract this update into the existing source folder.
  pause
  exit /b 1
)
start "" "%~dp0src\RedMoon.sln"
if errorlevel 1 (
  echo Open src\RedMoon.sln from a Visual Studio version that supports .NET 10.
  pause
  exit /b 1
)
exit /b 0
