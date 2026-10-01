@echo off
rem ------------------------------------------------------------------
rem  OPTIONAL: run the portal as a Windows Service so it starts
rem  automatically when the server PC boots (no one needs to log in).
rem  Right-click > "Run as administrator".
rem  Use start-server.bat first to make sure everything works.
rem ------------------------------------------------------------------
net session >nul 2>&1
if errorlevel 1 (
  echo Please right-click install-service.bat and choose "Run as administrator".
  pause
  exit /b 1
)
cd /d "%~dp0"
set SVC=MikroTekArchiving

rem The service runs as "Local System", which needs its own access to SQL Server Express.
where sqlcmd >nul 2>&1
if errorlevel 1 (
  echo NOTE: sqlcmd was not found. Run sql\grant-service-access.sql in SSMS
  echo       so the service can use the database. See DEPLOY.md, step 7.
) else (
  sqlcmd -S .\SQLEXPRESS -E -b -i "%~dp0sql\grant-service-access.sql"
)

sc query %SVC% >nul 2>&1
if not errorlevel 1 (
  sc stop %SVC% >nul 2>&1
  timeout /t 3 >nul
  sc delete %SVC% >nul
  timeout /t 2 >nul
)
sc create %SVC% binPath= "\"%~dp0MikroTek.Archiving.exe\"" start= delayed-auto DisplayName= "MikroTek Archiving Portal"
sc description %SVC% "Disbursement Archiving Portal and Hotspot - Leonardo NHS (http port 5000)"
rem Restart automatically if it ever crashes.
sc failure %SVC% reset= 86400 actions= restart/5000/restart/10000/restart/30000
sc start %SVC%
echo.
echo Done. Check it at http://localhost:5000/health
pause
