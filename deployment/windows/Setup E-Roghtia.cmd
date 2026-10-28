@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-E-Roghtia.ps1"
if errorlevel 1 (
  echo.
  pause
  exit /b 1
)
pause
