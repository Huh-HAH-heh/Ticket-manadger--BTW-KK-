@echo off
setlocal
set "ROOT=%~dp0..\..\"
set "READY=%LOCALAPPDATA%\TicketManager-Native\ready.flag"

if exist "%READY%" (
  echo Starting the legacy native PostgreSQL installation...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Start-TicketManager-Native.ps1"
) else (
  echo First run: installing PostgreSQL as a Windows service.
  echo This legacy mode is not the default portfolio setup.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Install-TicketManager-Native.ps1"
)

if errorlevel 1 (
  echo.
  echo The legacy native installation did not start. Read the error above.
  pause
)
endlocal
