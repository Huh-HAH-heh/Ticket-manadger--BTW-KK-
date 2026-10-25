@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-TicketManager-Docker.ps1"
if errorlevel 1 echo Ticket Manager Docker stack did not start. Read the error above.
echo.
pause
endlocal
