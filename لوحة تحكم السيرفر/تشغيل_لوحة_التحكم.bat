@echo off
chcp 65001 >nul
title MoonQueen Control Studio (127.0.0.1:8099) - by ahmedzahran
cd /d "%~dp0"

set "PHP_EXE="
if exist "C:\xampp\php\php.exe" set "PHP_EXE=C:\xampp\php\php.exe"
if "%PHP_EXE%"=="" (
    where php >nul 2>nul && set "PHP_EXE=php"
)

if "%PHP_EXE%"=="" (
    echo [!] لم يتم العثور على C:\xampp\php\php.exe
    echo يرجى التأكد من تثبيت XAMPP في المسار الافتراضي أو نسخ المجلد إلى htdocs.
    pause
    exit /b 1
)

echo ============================================================
echo   MoonQueen Control Studio - Live Rates ^& Server Control
echo   Engineered by ahmedzahran
echo   Listening strictly on http://127.0.0.1:8099 (Local Only)
echo ============================================================
echo.
start "" "http://127.0.0.1:8099"
"%PHP_EXE%" -S 127.0.0.1:8099 router.php
pause
