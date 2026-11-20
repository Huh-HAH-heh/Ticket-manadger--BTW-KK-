@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\scripts\Start-TicketManager-Native.ps1"
if errorlevel 1 (
  echo.
  echo The legacy native installation did not start. Read the error above.
  pause
)
endlocal
