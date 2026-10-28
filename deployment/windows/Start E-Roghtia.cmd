@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-E-Roghtia.ps1"
if errorlevel 1 (
  echo.
  pause
  exit /b 1
)
