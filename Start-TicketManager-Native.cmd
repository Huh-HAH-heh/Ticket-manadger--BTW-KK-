@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-TicketManager-Native.ps1"
if errorlevel 1 (
  echo.
  echo Ticket Manager did not start. Read the error above.
  pause
)
endlocal
