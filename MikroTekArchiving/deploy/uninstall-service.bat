@echo off
rem Removes the MikroTek Archiving Windows Service. Right-click > "Run as administrator".
net session >nul 2>&1
if errorlevel 1 (
  echo Please right-click uninstall-service.bat and choose "Run as administrator".
  pause
  exit /b 1
)
sc stop MikroTekArchiving
timeout /t 3 >nul
sc delete MikroTekArchiving
pause
