@echo off
REM Stops the running Hamzet Wasl API.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-api.ps1" -Stop