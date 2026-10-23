@echo off
setlocal
set "ROOT=%~dp0"
set "READY=%LOCALAPPDATA%\TicketManager-Native\ready.flag"

if exist "%READY%" (
  echo Starting Ticket Manager with the local PostgreSQL service...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Start-TicketManager-Native.ps1"
) else (
  echo First run: installing PostgreSQL, applying migrations, building API and client.
  echo Windows may ask for administrator approval.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Install-TicketManager-Native.ps1"
)

if errorlevel 1 (
  echo.
  echo Ticket Manager did not start. Read the error above.
  pause
)
endlocal
