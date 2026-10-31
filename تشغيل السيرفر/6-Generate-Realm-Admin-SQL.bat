@echo off
chcp 65001 >nul
title MoonQueen First-Boot SQL Generator - by ahmedzahran
pushd "%~dp0" >nul 2>nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\MoonQueen-Manager.ps1" -Mode CryptoHelper
pause
