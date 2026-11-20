@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-TicketManager.ps1"
set "APP_EXIT=%ERRORLEVEL%"
if not "%APP_EXIT%"=="0" (
  echo.
  echo Ticket Manager did not start. Read the error above.
  pause
)
endlocal & exit /b %APP_EXIT%
