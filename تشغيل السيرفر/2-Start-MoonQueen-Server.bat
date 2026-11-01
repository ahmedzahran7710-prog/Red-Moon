@echo off
chcp 65001 >nul
title MoonQueen V6192 - [2] Server Launcher (by ahmedzahran)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0MoonQueen-Manager.ps1" -Mode ServerLauncher
echo.
pause
