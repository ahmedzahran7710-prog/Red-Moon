@echo off
chcp 65001 >nul
title MoonQueen Switch to Hamachi 25.21.212.130 - by ahmedzahran
pushd "%~dp0" >nul 2>nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\MoonQueen-Manager.ps1" -Mode Hamachi
pause
