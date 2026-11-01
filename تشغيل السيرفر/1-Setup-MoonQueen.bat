@echo off
chcp 65001 >nul
title MoonQueen V6192 - [1] Full Sequential Setup (by ahmedzahran)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0MoonQueen-Manager.ps1" -Mode SetupAll
echo.
pause
