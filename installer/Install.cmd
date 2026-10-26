@echo off
REM Atkins MEP Toolkit installer. Double-click this file.
REM No administrator rights are needed: everything goes into your own
REM Windows profile, under %AppData%.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"

echo.
pause
