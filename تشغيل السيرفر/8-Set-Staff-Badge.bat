@echo off
chcp 65001 >nul
title MoonQueen Staff [PM]/[GM] Badge Helper (V6192) - by ahmedzahran
pushd "%~dp0" >nul 2>nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\MoonQueen-Manager.ps1" -Mode StaffBadge
pause
