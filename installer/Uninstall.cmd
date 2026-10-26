@echo off
REM Removes the Atkins MEP Toolkit from every Revit version it was installed into.
REM Only files this installer wrote are deleted - other add-ins sharing
REM the same folder are left alone.
REM
REM Your settings (ACC model list, duct insulation rules) are kept.
REM To delete those as well, add -RemoveSettings to the line below.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -Uninstall

echo.
pause
