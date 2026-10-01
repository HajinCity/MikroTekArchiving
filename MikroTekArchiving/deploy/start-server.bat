@echo off
rem ------------------------------------------------------------------
rem  MikroTek Archiving Portal - start the server (keep this window open)
rem  Other PCs open:  http://THIS-PC-IP:5000/Portal
rem  Close this window or press Ctrl+C to stop the server.
rem ------------------------------------------------------------------
cd /d "%~dp0"
title MikroTek Archiving Portal - SERVER (do not close)
set ASPNETCORE_ENVIRONMENT=Production

if not exist "MikroTek.Archiving.exe" (
  echo MikroTek.Archiving.exe was not found in %~dp0
  echo Copy this file into the published folder and run it from there.
  pause
  exit /b 1
)

echo Starting... the addresses for the other PCs will be listed below.
echo.
MikroTek.Archiving.exe
echo.
echo The server has stopped.
pause
