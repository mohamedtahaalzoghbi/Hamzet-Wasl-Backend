@echo off
REM Stops the running Hamzet Wasl API.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-api.ps1" -Stop