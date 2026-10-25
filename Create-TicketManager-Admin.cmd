@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create-TicketManager-Admin.ps1"
if errorlevel 1 echo First administrator setup did not complete. Read the error above.
echo.
pause
endlocal
